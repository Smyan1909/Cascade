"""LangGraph wiring for Explorer agent."""

from __future__ import annotations

from typing import Any, Dict, List, Optional, TypedDict

from langgraph.graph import END, StateGraph

from cascade_client.auth.context import CascadeContext
from cascade_client.grpc_client import CascadeGrpcClient

from storage.firestore_client import FirestoreClient

from clients.llm_client import load_llm_client_from_env
from .api_discovery import ApiDiscovery
from .api_evaluator import ApiEvaluator
from .code_generator import CodeGenerator
from .embeddings import load_embedding_fn
from .instruction_parser import InstructionParser
from .manual_reader import ManualReader
from .observer import Observer
from .planner import Planner
from .skill_map import SkillEvidence, SkillMap, SkillMetadata
from .verifier import Verifier


class ExplorerState(TypedDict, total=False):
    context: CascadeContext
    run_id: str
    app_name: str
    instructions: Dict[str, Any]
    tasks: List[str]
    capabilities: List[str]
    missing_capabilities: List[str]
    discovered_apis: List[Dict[str, Any]]
    steps: List[Any]
    skill_map: Optional[SkillMap]
    code_artifact_id: Optional[str]
    confidence_scores: Dict[str, float]


def _node_analyze_instructions(state: ExplorerState, parser: InstructionParser) -> ExplorerState:
    instructions = parser.parse(state.get("instructions") or {})
    state["instructions"] = instructions
    return state


def _node_discover_capabilities(state: ExplorerState, manual_reader: ManualReader) -> ExplorerState:
    caps = state.get("capabilities") or []
    if caps:
        return state
    tasks = state.get("tasks") or []
    if not tasks and state.get("instructions", {}).get("manual_text"):
        chunks = manual_reader.chunk_text(state["instructions"]["manual_text"])
        tasks = manual_reader.extract_tasks(chunks)
    # Treat tasks as capabilities for now
    state["capabilities"] = tasks
    return state


def _node_check_coverage(state: ExplorerState, fs: FirestoreClient) -> ExplorerState:
    caps = state.get("capabilities") or []
    if not caps:
        state["missing_capabilities"] = []
        return state
    existing = fs.list_skill_maps()
    existing_caps = []
    for data in existing.values():
        try:
            sm = SkillMap.model_validate(data)
            if sm.metadata.capability:
                existing_caps.append(sm.metadata.capability)
        except Exception:
            continue
    missing = [c for c in caps if c not in existing_caps]
    state["missing_capabilities"] = missing
    return state


def _node_select_missing(state: ExplorerState) -> ExplorerState:
    # Set tasks to missing capabilities so planner focuses on gaps
    missing = state.get("missing_capabilities") or []
    state["tasks"] = missing
    return state


def _node_discover_apis(state: ExplorerState, discovery: ApiDiscovery) -> ExplorerState:
    app_name = state.get("app_name") or state.get("instructions", {}).get("app_name", "app")
    state["discovered_apis"] = discovery.discover_via_web(app_name)
    return state


def _node_plan(state: ExplorerState, planner: Planner, manual_reader: ManualReader) -> ExplorerState:
    tasks = state.get("tasks") or []
    if not tasks and state.get("instructions", {}).get("manual_text"):
        chunks = manual_reader.chunk_text(state["instructions"]["manual_text"])
        tasks = manual_reader.extract_tasks(chunks)
    state["tasks"] = tasks
    state["steps"] = planner.plan_from_manual_tasks(tasks, api_docs=state.get("discovered_apis"))
    return state


def _node_observe(state: ExplorerState, observer: Observer) -> ExplorerState:
    if not state.get("steps"):
        return state
    tree = observer.fetch_semantic_tree()
    for step in state["steps"]:
        if step.selector is None and step.api_endpoint is None:
            candidates = observer.propose_selectors(tree, text_hint=state.get("app_name", ""))
            if candidates:
                step.selector = candidates[0]
    return state


def _node_verify(state: ExplorerState, verifier: Verifier) -> ExplorerState:
    if not state.get("steps"):
        return state
    for step in state["steps"]:
        if step.api_endpoint:
            result = verifier.verify_api(step)
            if result and result.ok:
                step.confidence = 0.8
        if step.selector:
            if verifier.verify_selector(step):
                step.confidence = max(step.confidence, 0.6)
    return state


def _node_skill_map(state: ExplorerState, ctx: CascadeContext) -> ExplorerState:
    metadata = SkillMetadata(
        skill_id=state.get("instructions", {}).get("skill_id", state.get("run_id", "skill")),
        app_id=ctx.app_id,
        user_id=ctx.user_id,
        preferred_method=state.get("instructions", {}).get("preferred_method", "api"),
    )
    evidence = SkillEvidence()
    skill_map = SkillMap(metadata=metadata, steps=state.get("steps", []), assets=evidence)  # type: ignore[arg-type]
    state["skill_map"] = skill_map
    return state


def _node_generate_code(state: ExplorerState, generator: CodeGenerator) -> ExplorerState:
    if not state.get("skill_map"):
        return state
    artifact_id, _ = generator.generate(state["skill_map"])
    state["code_artifact_id"] = artifact_id
    return state


def _node_persist(state: ExplorerState, fs: FirestoreClient) -> ExplorerState:
    if state.get("skill_map"):
        fs.upsert_skill_map(state["skill_map"])
    if state.get("run_id"):
        fs.save_checkpoint(fs.explorer_checkpoint_path(state["run_id"]), dict(state))
    return state


def build_explorer_graph(context: CascadeContext, grpc_client: CascadeGrpcClient) -> Any:
    """Wire the Explorer LangGraph."""
    llm_client = None
    try:
        llm_client = load_llm_client_from_env()
    except Exception:
        llm_client = None  # allow graph to function without LLM

    fs = FirestoreClient(context)
    parser = InstructionParser()
    discovery = ApiDiscovery(llm=llm_client)
    planner = Planner(llm=llm_client)
    embed_fn = load_embedding_fn()
    manual_reader = ManualReader(embed_fn=embed_fn)
    observer = Observer(grpc_client)
    verifier = Verifier(grpc_client)
    evaluator = ApiEvaluator()
    generator = CodeGenerator(fs, llm=llm_client)

    graph = StateGraph(ExplorerState)
    graph.add_node("analyze_instructions", lambda state: _node_analyze_instructions(state, parser))
    graph.add_node("discover_capabilities", lambda state: _node_discover_capabilities(state, manual_reader))
    graph.add_node("check_coverage", lambda state: _node_check_coverage(state, fs))
    graph.add_node("select_missing", lambda state: _node_select_missing(state))
    graph.add_node("discover_apis", lambda state: _node_discover_apis(state, discovery))
    graph.add_node("plan", lambda state: _node_plan(state, planner, manual_reader))
    graph.add_node("observe", lambda state: _node_observe(state, observer))
    graph.add_node("verify", lambda state: _node_verify(state, verifier))
    graph.add_node("skill_map", lambda state: _node_skill_map(state, context))
    graph.add_node("generate_code", lambda state: _node_generate_code(state, generator))
    graph.add_node("persist", lambda state: _node_persist(state, fs))

    graph.set_entry_point("analyze_instructions")
    graph.add_edge("analyze_instructions", "discover_capabilities")
    graph.add_edge("discover_capabilities", "check_coverage")
    graph.add_edge("check_coverage", "select_missing")
    graph.add_edge("discover_apis", "plan")
    graph.add_edge("plan", "observe")
    graph.add_edge("observe", "verify")
    graph.add_edge("verify", "skill_map")
    graph.add_edge("skill_map", "generate_code")
    graph.add_edge("generate_code", "persist")

    def _route_after_select(state: ExplorerState) -> str:
        missing = state.get("missing_capabilities") or []
        if not missing:
            return "end"
        return "discover_apis"

    graph.add_conditional_edges(
        "select_missing",
        _route_after_select,
        {
            "discover_apis": "discover_apis",
            "end": END,
        },
    )

    graph.add_edge("persist", END)

    return graph.compile()


"""Autonomous Explorer: LangGraph-based exploration agent."""

from __future__ import annotations

import json
import uuid
from typing import Any, Dict, List, Optional, TypedDict

from langgraph.graph import StateGraph, END

from cascade_client.auth.context import CascadeContext
from cascade_client.grpc_client import CascadeGrpcClient

from mcp_server.tool_registry import ToolRegistry
from mcp_server.body_tools import register_body_tools
from mcp_server.explorer_tools import register_explorer_tools

from agents.core.autonomous_agent import (
    AgentConfig, AgentResult, AgentStatus, ReActVerifier
)
from agents.core.verify_prompts import EXPLORER_VERIFY_PROMPT, get_explorer_verify_task
from .prompts_autonomous import EXPLORER_SYSTEM_PROMPT
from .skill_map import SkillMap, SkillMetadata, SkillStep
from .api_discovery import ApiDiscovery
from .observer import Observer


# ============================================================
# State Definition
# ============================================================

class ExplorerState(TypedDict, total=False):
    """State for the Explorer LangGraph."""
    # Input
    run_id: str
    app_name: str
    instructions: Dict[str, Any]
    
    # Discovery phase
    tasks: List[Dict[str, Any]]
    existing_skills: Dict[str, Any]
    missing_capabilities: List[Dict[str, Any]]
    discovered_apis: List[Dict[str, Any]]
    
    # Observation phase
    ui_elements: List[Dict[str, Any]]
    app_launched: bool
    
    # Generation phase
    steps: List[SkillStep]
    skill_map: Optional[SkillMap]
    
    # Verification phase
    verification_result: Optional[Dict[str, Any]]
    
    # Final
    status: str
    error: Optional[str]


# ============================================================
# Node Functions
# ============================================================

def node_discover(state: ExplorerState, context: CascadeContext) -> ExplorerState:
    """Discovery phase: Parse instructions and find missing capabilities."""
    instructions = state.get("instructions", {})
    
    # Extract tasks from instructions
    tasks = []
    if "coverage_data" in instructions:
        for item in instructions["coverage_data"]:
            tasks.append({
                "name": item.get("capability", item.get("name", "")),
                "constraints": item.get("constraints", []),
                "expected_outputs": item.get("expected_outputs", []),
            })
    elif "tasks" in instructions:
        for task in instructions["tasks"]:
            tasks.append(task if isinstance(task, dict) else {"name": task})
    
    print(f"[Explorer] Discovered {len(tasks)} tasks/capabilities")
    
    # Get existing skills
    existing = {}
    try:
        from storage.firestore_client import FirestoreClient
        fs = FirestoreClient(context)
        existing = fs.list_skill_maps()
    except Exception as e:
        print(f"[Explorer] Could not list skill maps: {e}")
    
    print(f"[Explorer] Found {len(existing)} existing skills")
    
    # Find missing capabilities
    existing_caps = set()
    for skill_data in existing.values():
        if "metadata" in skill_data:
            cap = skill_data["metadata"].get("capability", "")
            existing_caps.add(cap.lower())
    
    missing = [t for t in tasks if t.get("name", "").lower() not in existing_caps]
    print(f"[Explorer] Missing capabilities: {len(missing)}")
    
    return {
        **state,
        "tasks": tasks,
        "existing_skills": existing,
        "missing_capabilities": missing,
    }


def node_discover_apis(state: ExplorerState) -> ExplorerState:
    """Discover APIs for the application."""
    app_name = state.get("app_name", "")
    
    print(f"[Explorer] Discovering APIs for: {app_name}")
    api_discovery = ApiDiscovery()
    apis = api_discovery.discover_via_web(app_name)
    print(f"[Explorer] Found {len(apis)} APIs")
    
    return {**state, "discovered_apis": apis}


def node_launch_app(state: ExplorerState, grpc_client: CascadeGrpcClient) -> ExplorerState:
    """Launch the target application."""
    app_name = state.get("app_name", "")
    
    print(f"[Explorer] Launching: {app_name}")
    try:
        status = grpc_client.start_app(app_name)
        print(f"[Explorer] Launch result: {status.success}, message: {status.message}")
        
        import time
        print("[Explorer] Waiting for app to stabilize...")
        time.sleep(3)
        
        return {**state, "app_launched": status.success}
    except Exception as e:
        print(f"[Explorer] Launch warning: {e}")
        return {**state, "app_launched": False}


def node_observe(state: ExplorerState, grpc_client: CascadeGrpcClient) -> ExplorerState:
    """Observe the UI and get semantic tree."""
    print("[Explorer] Observing UI...")
    
    observer = Observer(grpc_client)
    semantic_tree = observer.get_semantic_tree()
    ui_elements = semantic_tree.get("elements", []) if semantic_tree else []
    
    print(f"[Explorer] Got semantic tree with {len(ui_elements)} elements")
    return {**state, "ui_elements": ui_elements}


def node_generate_skills(state: ExplorerState) -> ExplorerState:
    """Generate skill map from observations."""
    missing = state.get("missing_capabilities", [])
    ui_elements = state.get("ui_elements", [])
    app_name = state.get("app_name", "")
    
    print(f"[Explorer] Generating steps for {len(missing)} capabilities")
    
    steps = []
    for i, task in enumerate(missing):
        name = task.get("name", f"task_{i}")
        
        # Try to find matching UI element
        matching_element = None
        for elem in ui_elements:
            elem_name = elem.get("name", "").lower()
            if name.lower() in elem_name or elem_name in name.lower():
                matching_element = elem
                break
        
        if matching_element:
            steps.append(SkillStep(
                action="click",
                selector=matching_element.get("automation_id") or matching_element.get("name", ""),
                description=f"Execute: {name}",
            ))
        else:
            steps.append(SkillStep(
                action="ui_action",
                selector=name,
                description=f"TODO: Implement {name}",
            ))
    
    print(f"[Explorer] Generated {len(steps)} steps")
    
    # Create skill map
    skill_map = SkillMap(
        metadata=SkillMetadata(
            skill_id=str(uuid.uuid4()),
            app_name=app_name,
            capability=", ".join(t.get("name", "unknown") for t in missing[:3]),
            description=f"Automation skills for {app_name}",
        ),
        steps=steps,
    )
    print(f"[Explorer] Skill map created: {skill_map.metadata.skill_id}")
    
    return {**state, "steps": steps, "skill_map": skill_map}


def node_save_skill(state: ExplorerState, context: CascadeContext) -> ExplorerState:
    """Save skill map to Firestore."""
    skill_map = state.get("skill_map")
    if not skill_map:
        return state
    
    try:
        from storage.firestore_client import FirestoreClient
        fs = FirestoreClient(context)
        fs.upsert_skill_map(skill_map)
        print(f"[Explorer] Skill map saved to Firestore")
    except Exception as e:
        print(f"[Explorer] Could not save skill map: {e}")
        return {**state, "error": str(e)}
    
    return state


def should_continue_to_verify(state: ExplorerState) -> str:
    """Decide whether to continue to verification or end early."""
    if not state.get("missing_capabilities"):
        return "end_early"
    if not state.get("skill_map"):
        return "end_early"
    return "verify"


# ============================================================
# Graph Builder
# ============================================================

def build_explorer_graph(
    context: CascadeContext,
    grpc_client: CascadeGrpcClient,
) -> StateGraph:
    """Build the LangGraph for autonomous exploration."""
    
    graph = StateGraph(ExplorerState)
    
    # Add nodes with bound dependencies
    graph.add_node("discover", lambda s: node_discover(s, context))
    graph.add_node("discover_apis", node_discover_apis)
    graph.add_node("launch_app", lambda s: node_launch_app(s, grpc_client))
    graph.add_node("observe", lambda s: node_observe(s, grpc_client))
    graph.add_node("generate_skills", node_generate_skills)
    graph.add_node("save_skill", lambda s: node_save_skill(s, context))
    
    # Define flow
    graph.set_entry_point("discover")
    graph.add_edge("discover", "discover_apis")
    graph.add_edge("discover_apis", "launch_app")
    graph.add_edge("launch_app", "observe")
    graph.add_edge("observe", "generate_skills")
    graph.add_edge("generate_skills", "save_skill")
    graph.add_conditional_edges(
        "save_skill",
        should_continue_to_verify,
        {"verify": END, "end_early": END}  # Verification handled externally
    )
    
    return graph.compile()


# ============================================================
# HybridExplorer Class
# ============================================================

class HybridExplorer:
    """
    Autonomous Explorer using LangGraph for Plan/Execute and ReAct for Verify.
    
    The agent:
    1. Discovers capabilities from instructions (LangGraph)
    2. Launches and observes the target application (LangGraph)
    3. Generates skill maps based on UI elements (LangGraph)
    4. Verifies the generated skills work correctly (ReAct)
    """

    def __init__(
        self,
        context: CascadeContext,
        grpc_client: CascadeGrpcClient,
        max_verify_iterations: int = 10,
        verbose: bool = True,
    ):
        self._context = context
        self._grpc = grpc_client
        self._max_verify_iterations = max_verify_iterations
        self._verbose = verbose
        
        # Build LangGraph
        self._graph = build_explorer_graph(context, grpc_client)
        
        # Setup tool registry for verification phase
        self._registry = ToolRegistry()
        register_body_tools(self._registry, grpc_client)
        register_explorer_tools(self._registry)
        self._add_skill_tools()

    def _add_skill_tools(self) -> None:
        """Add skill saving tool for verification phase."""
        from storage.firestore_client import FirestoreClient
        fs = FirestoreClient(self._context)
        
        def save_skill_map(skill_map_json: str) -> Dict[str, Any]:
            try:
                skill_data = json.loads(skill_map_json)
                skill_map = SkillMap.model_validate(skill_data)
                fs.upsert_skill_map(skill_map)
                return {"content": [{"type": "text", "text": f"Saved skill: {skill_map.metadata.skill_id}"}]}
            except Exception as e:
                return {"content": [{"type": "text", "text": f"Error: {str(e)}"}], "isError": True}
        
        self._registry.register_tool(
            name="save_skill_map",
            description="Save a skill map to storage",
            input_schema={"type": "object", "properties": {"skill_map_json": {"type": "string"}}, "required": ["skill_map_json"]},
            handler=lambda skill_map_json: save_skill_map(skill_map_json),
        )

    def _log(self, msg: str) -> None:
        if self._verbose:
            print(f"[HybridExplorer] {msg}")

    def explore(
        self,
        app_name: str,
        instructions: Optional[Dict[str, Any]] = None,
        run_id: Optional[str] = None,
        skip_verify: bool = False,
    ) -> AgentResult:
        """
        Run autonomous exploration using LangGraph + ReAct verification.
        """
        run_id = run_id or uuid.uuid4().hex[:8]
        
        # =====================================================
        # PHASES 1-3: LangGraph Flow (Discover → Execute → Generate)
        # =====================================================
        self._log("=== PHASE 1-3: LANGGRAPH FLOW ===")
        
        initial_state: ExplorerState = {
            "run_id": run_id,
            "app_name": app_name,
            "instructions": instructions or {},
            "tasks": [],
            "existing_skills": {},
            "missing_capabilities": [],
            "discovered_apis": [],
            "ui_elements": [],
            "app_launched": False,
            "steps": [],
            "skill_map": None,
            "verification_result": None,
            "status": "running",
            "error": None,
        }
        
        try:
            final_state = self._graph.invoke(initial_state)
            skill_map = final_state.get("skill_map")
            
            if not skill_map:
                self._log("No skill map generated")
                return AgentResult(
                    status=AgentStatus.COMPLETED,
                    final_response="Exploration complete but no new skills discovered",
                    iterations=0,
                    tool_calls=[],
                )
            
            self._log(f"Generated skill map: {skill_map.metadata.skill_id}")
            
        except Exception as e:
            self._log(f"LangGraph flow failed: {e}")
            return AgentResult(
                status=AgentStatus.FAILED,
                final_response="",
                iterations=0,
                error=str(e),
            )
        
        if skip_verify:
            self._log("Skipping verification phase")
            return AgentResult(
                status=AgentStatus.COMPLETED,
                final_response=f"Skill map created: {skill_map.metadata.skill_id}",
                iterations=0,
                tool_calls=[],
            )
        
        # =====================================================
        # PHASE 4: ReAct Verification
        # =====================================================
        self._log("=== PHASE 4: REACT VERIFICATION ===")
        
        verifier = ReActVerifier(
            tool_registry=self._registry,
            system_prompt=EXPLORER_VERIFY_PROMPT,
            max_iterations=self._max_verify_iterations,
            verbose=self._verbose,
        )
        
        skill_summary = self._build_skill_summary(skill_map)
        verify_task = get_explorer_verify_task(skill_summary, app_name)
        
        verify_result = verifier.verify(
            verification_task=verify_task,
            context={"skill_id": skill_map.metadata.skill_id},
            thread_id=f"verify_{run_id}",
        )
        
        if verify_result.success:
            self._log("Verification PASSED")
            return AgentResult(
                status=AgentStatus.COMPLETED,
                final_response=f"Skill verified: {skill_map.metadata.skill_id}\n{verify_result.feedback}",
                iterations=verify_result.iterations,
                tool_calls=[],
            )
        else:
            self._log(f"Verification FAILED: {verify_result.feedback[:100]}...")
            return AgentResult(
                status=AgentStatus.COMPLETED,
                final_response=f"Skill created but verification found issues: {verify_result.feedback}",
                iterations=verify_result.iterations,
                tool_calls=[],
            )

    def _build_skill_summary(self, skill_map: SkillMap) -> str:
        """Build a text summary of the skill map for verification."""
        lines = [
            f"Skill ID: {skill_map.metadata.skill_id}",
            f"Capability: {skill_map.metadata.capability or 'N/A'}",
            f"Description: {skill_map.metadata.description or 'N/A'}",
            f"Steps ({len(skill_map.steps)}):",
        ]
        for i, step in enumerate(skill_map.steps, 1):
            action = getattr(step, 'action', 'unknown')
            selector = getattr(step, 'selector', None)
            lines.append(f"  {i}. {action}: {selector}")
        return "\n".join(lines)


# Backward compatibility alias
AutonomousExplorer = HybridExplorer


def build_autonomous_explorer(
    context: CascadeContext,
    grpc_client: CascadeGrpcClient,
) -> HybridExplorer:
    """Factory function to build explorer."""
    return HybridExplorer(context, grpc_client)

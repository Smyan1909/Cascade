"""LangGraph wiring for the Orchestrator supervisor."""

from __future__ import annotations

import asyncio
import json
import time
import uuid
from typing import Any, Dict, Iterable, List, Optional, TypedDict

from langgraph.graph import END, StateGraph

from cascade_client.a2a import AgentA2AClient
from cascade_client.auth.context import CascadeContext
from cascade_client.grpc_client import CascadeGrpcClient

from orchestrator.router import OrchestratorRouter
from orchestrator.state import ExecutionRecord, OrchestratorCheckpoint, RoutingDecision, Subgoal
from orchestrator.storage import OrchestratorStorage


class OrchestratorState(TypedDict, total=False):
    context: CascadeContext
    run_id: str
    goal: str
    requested_skill_ids: List[str]
    subgoals: List[Subgoal]
    decisions: List[RoutingDecision]
    records: List[ExecutionRecord]
    current_index: int
    dry_run: bool
    trace: bool
    resume: bool
    last_error: Optional[str]
    failed: bool
    max_dispatch_retries: int


def _ensure_model_list(items: Iterable[Any], model_cls):
    resolved: List[Any] = []
    for item in items:
        if isinstance(item, model_cls):
            resolved.append(item)
        else:
            resolved.append(model_cls.model_validate(item))
    return resolved


def _node_load_checkpoint(state: OrchestratorState, storage: OrchestratorStorage) -> OrchestratorState:
    if not state.get("resume"):
        return state
    run_id = state.get("run_id")
    if not run_id:
        state["last_error"] = "resume requested but run_id missing"
        return state
    checkpoint = storage.load_checkpoint(run_id)
    if not checkpoint:
        return state
    state["goal"] = checkpoint.goal
    state["subgoals"] = checkpoint.subgoals
    state["decisions"] = checkpoint.decisions
    state["records"] = checkpoint.records
    state["current_index"] = checkpoint.current_index
    state["last_error"] = checkpoint.last_error
    return state


def _node_plan(state: OrchestratorState, router: OrchestratorRouter) -> OrchestratorState:
    if state.get("subgoals"):
        return state
    subgoals = router.plan_subgoals(
        state.get("goal", ""),
        requested_skill_ids=state.get("requested_skill_ids"),
    )
    state["subgoals"] = subgoals
    state["current_index"] = 0
    return state


def _node_route(state: OrchestratorState, router: OrchestratorRouter) -> OrchestratorState:
    subgoals = _ensure_model_list(state.get("subgoals", []), Subgoal)
    state["subgoals"] = subgoals
    decisions = _ensure_model_list(state.get("decisions", []), RoutingDecision)
    state["decisions"] = decisions

    idx = state.get("current_index", 0)
    if idx >= len(subgoals):
        return state

    subgoal = subgoals[idx]
    decision = router.choose_executor(subgoal)
    if len(decisions) > idx:
        decisions[idx] = decision
    else:
        decisions.append(decision)
    state["decisions"] = decisions
    return state


def _make_payload(state: OrchestratorState, subgoal: Subgoal, decision: RoutingDecision) -> Dict[str, Any]:
    return {
        "subgoal_id": subgoal.subgoal_id,
        "goal": state.get("goal"),
        "subgoal": subgoal.model_dump(mode="json"),
        "decision": decision.model_dump(mode="json"),
        "run_id": state.get("run_id"),
        "trace": state.get("trace", False),
    }


async def _send_a2a_message(a2a_client: AgentA2AClient, payload: Dict[str, Any], decision: RoutingDecision) -> None:
    target_role = "worker" if decision.executor_type == "worker" else "explorer"
    await a2a_client.send(
        payload,
        target_role=target_role,
        headers={"subgoal_id": decision.subgoal_id, "skill_id": decision.skill_id or ""},
    )


def _node_dispatch(
    state: OrchestratorState,
    a2a_client: Optional[AgentA2AClient],
) -> OrchestratorState:
    decisions = _ensure_model_list(state.get("decisions", []), RoutingDecision)
    subgoals = _ensure_model_list(state.get("subgoals", []), Subgoal)
    records = _ensure_model_list(state.get("records", []), ExecutionRecord)
    state["decisions"] = decisions
    state["subgoals"] = subgoals
    state["records"] = records

    idx = state.get("current_index", 0)
    if idx >= len(decisions) or idx >= len(subgoals):
        return state

    decision = decisions[idx]
    subgoal = subgoals[idx]
    record = ExecutionRecord(subgoal_id=subgoal.subgoal_id, routed_to=decision)

    if state.get("dry_run", False):
        record.status = "completed"
        record.output = {"message": "dry-run: planning only"}
        state.setdefault("records", []).append(record)
        state["current_index"] = idx + 1
        return state

    if not a2a_client:
        record.status = "failed"
        record.error = "A2A client unavailable"
        state.setdefault("records", []).append(record)
        state["last_error"] = record.error
        state["failed"] = True
        return state

    payload = _make_payload(state, subgoal, decision)
    attempts = max(1, state.get("max_dispatch_retries", 2) + 1)
    last_exc: Optional[Exception] = None
    for attempt in range(attempts):
        try:
            asyncio.run(_send_a2a_message(a2a_client, payload, decision))
            record.status = "completed"
            record.output = {"dispatched": True, "payload": payload, "attempt": attempt + 1}
            last_exc = None
            break
        except Exception as exc:  # pylint: disable=broad-except
            last_exc = exc
            time.sleep(0.1 * (attempt + 1))

    if last_exc:
        record.status = "failed"
        record.error = str(last_exc)
        state["failed"] = True
        state["last_error"] = str(last_exc)

    # Upsert record and advance
    updated_records = list(records)
    found = False
    for i, existing in enumerate(updated_records):
        if existing.subgoal_id == record.subgoal_id:
            updated_records[i] = record
            found = True
            break
    if not found:
        updated_records.append(record)
    state["records"] = updated_records
    if record.status != "failed":
        state["current_index"] = idx + 1
    return state


def _node_checkpoint(state: OrchestratorState, storage: OrchestratorStorage) -> OrchestratorState:
    checkpoint = OrchestratorCheckpoint(
        run_id=state.get("run_id", str(uuid.uuid4())),
        goal=state.get("goal", ""),
        subgoals=_ensure_model_list(state.get("subgoals", []), Subgoal),
        decisions=_ensure_model_list(state.get("decisions", []), RoutingDecision),
        records=_ensure_model_list(state.get("records", []), ExecutionRecord),
        current_index=state.get("current_index", 0),
        completed=state.get("current_index", 0) >= len(state.get("subgoals", [])),
        failed=state.get("failed", False),
        last_error=state.get("last_error"),
    )
    storage.save_checkpoint(checkpoint)
    return state


def build_orchestrator_graph(
    context: CascadeContext,
    grpc_client: CascadeGrpcClient,
    router: OrchestratorRouter,
    storage: OrchestratorStorage,
    *,
    a2a_client: Optional[AgentA2AClient] = None,
) -> Any:
    """
    Build the LangGraph for orchestrator planning/routing/dispatch.

    The graph is intentionally simple to keep determinism and testability while
    still emitting checkpoints after each dispatch.
    """
    graph = StateGraph(OrchestratorState)

    # Default A2A client binds to orchestrator role
    client = a2a_client or AgentA2AClient(context, grpc_client, role="orchestrator")

    graph.add_node("load_checkpoint", lambda s: _node_load_checkpoint(s, storage))
    graph.add_node("plan", lambda s: _node_plan(s, router))
    graph.add_node("route", lambda s: _node_route(s, router))
    graph.add_node("dispatch", lambda s: _node_dispatch(s, client))
    graph.add_node("checkpoint", lambda s: _node_checkpoint(s, storage))

    graph.set_entry_point("load_checkpoint")
    graph.add_edge("load_checkpoint", "plan")
    graph.add_edge("plan", "route")
    graph.add_edge("route", "dispatch")
    graph.add_edge("dispatch", "checkpoint")

    def _route_after_checkpoint(state: OrchestratorState) -> str:
        if state.get("failed"):
            return "end"
        idx = state.get("current_index", 0)
        total = len(state.get("subgoals", []))
        if idx < total:
            return "route"
        return "end"

    graph.add_conditional_edges(
        "checkpoint",
        _route_after_checkpoint,
        {
            "route": "route",
            "end": END,
        },
    )

    return graph.compile()



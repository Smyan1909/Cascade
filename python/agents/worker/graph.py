"""LangGraph wiring for the Worker agent with planning and verification loops."""

from __future__ import annotations

import time
from typing import Any, Dict, List, Optional, TypedDict

from langgraph.graph import END, StateGraph

from cascade_client.grpc_client import CascadeGrpcClient
from cascade_client.models import (
    Action,
    ActionType,
    Selector,
    Status,
    WorkerEvent,
    WorkerEventType,
)

from agents.explorer.skill_map import SkillMap, SkillStep
from agents.worker.planner import PlannedSkill
from agents.worker.storage import WorkerStorage
from agents.worker.verifier import verify_task_completion


class WorkerState(TypedDict, total=False):
    context: Any
    run_id: str
    task: str
    available_skills: List[SkillMap]
    execution_plan: List[PlannedSkill]
    current_skill_index: int
    execution_history: List[Dict[str, Any]]
    last_status: Optional[Status]
    last_error: Optional[str]
    dry_run: bool
    pending_events: List[WorkerEvent]
    metadata: Dict[str, str]
    failed: bool
    replan_count: int
    max_replans: int
    verification_result: Optional[Dict[str, Any]]


class StepExecutor:
    """Executes a single SkillStep via the Body gRPC client."""

    def __init__(self, grpc_client: CascadeGrpcClient, dry_run: bool = False):
        self._grpc = grpc_client
        self._dry_run = dry_run

    def execute_step(self, step: SkillStep) -> Status:
        if self._dry_run:
            return Status(success=True, message="dry-run: skipped action")

        if step.selector:
            action = self._build_action(step)
            return self._grpc.perform_action(action)

        # API path not yet implemented; return non-retryable status
        return Status(success=False, message="API execution not implemented")

    def execute_skill(self, skill: SkillMap) -> List[Status]:
        """Execute all steps of a skill sequentially."""
        statuses: List[Status] = []
        for step in skill.steps:
            # Handle waits inline
            for wait in step.waits:
                time.sleep(wait.timeout_seconds)
            status = self.execute_step(step)
            statuses.append(status)
            if not status.success:
                break
        return statuses

    def _build_action(self, step: SkillStep) -> Action:
        action_type = self._map_action(step.action)
        selector = step.selector or Selector(
            platform_source=0, path=[]
        )  # placeholder; should not happen
        # Map inputs for type text
        text_payload = None
        if action_type == ActionType.TYPE_TEXT and step.inputs:
            # Pick first text-like payload
            text_payload = str(step.inputs.get("text") or step.inputs.get("value") or "")
        return Action(action_type=action_type, selector=selector, text=text_payload)

    def _map_action(self, action_name: str) -> ActionType:
        normalized = action_name.lower()
        mapping = {
            "click": ActionType.CLICK,
            "press": ActionType.CLICK,
            "tap": ActionType.CLICK,
            "type": ActionType.TYPE_TEXT,
            "type_text": ActionType.TYPE_TEXT,
            "typetext": ActionType.TYPE_TEXT,
            "hover": ActionType.HOVER,
            "focus": ActionType.FOCUS,
            "scroll": ActionType.SCROLL,
            "wait": ActionType.WAIT_VISIBLE,
            "wait_visible": ActionType.WAIT_VISIBLE,
        }
        return mapping.get(normalized, ActionType.CLICK)


def _node_plan(state: WorkerState, planner_fn, llm_client=None, mcp_registry=None) -> WorkerState:
    events = state.setdefault("pending_events", [])
    execution_plan = planner_fn(
        task=state["task"],
        available_skills=state.get("available_skills", []),
        llm_client=llm_client,
        mcp_registry=mcp_registry,
    )
    state["execution_plan"] = execution_plan
    state["current_skill_index"] = 0
    state["replan_count"] = state.get("replan_count", 0)
    events.append(
        WorkerEvent(
            run_id=state["run_id"],
            skill_id="",
            event_type=WorkerEventType.LOG,
            message="Planning complete",
            metadata={"plan_size": str(len(execution_plan))},
            planning_phase=True,
            selected_skills=[p.skill_id for p in execution_plan],
        )
    )
    return state


def _node_execute_skill(state: WorkerState, executor: StepExecutor) -> WorkerState:
    events = state.setdefault("pending_events", [])
    plan = state.get("execution_plan") or []
    idx = state.get("current_skill_index", 0)

    if idx >= len(plan):
        return state

    # Locate skill
    skill_id = plan[idx].skill_id
    skill = _find_skill(state.get("available_skills", []), skill_id)
    if not skill:
        state["failed"] = True
        state["last_error"] = f"Skill {skill_id} not found"
        events.append(
            WorkerEvent(
                run_id=state["run_id"],
                skill_id=skill_id,
                event_type=WorkerEventType.STEP_FAILED,
                step_index=idx,
                message=f"Skill {skill_id} missing",
                error=state["last_error"],
            )
        )
        return state

    events.append(
        WorkerEvent(
            run_id=state["run_id"],
            skill_id=skill_id,
            event_type=WorkerEventType.STEP_STARTED,
            step_index=idx,
            message=f"Starting skill {skill_id}",
        )
    )

    try:
        statuses = executor.execute_skill(skill)
        last_status = statuses[-1] if statuses else Status(success=True, message="")
        state["last_status"] = last_status
        success = all(st.success for st in statuses)
        state.setdefault("execution_history", []).append(
            {
                "skill_id": skill_id,
                "success": success,
                "statuses": [st.model_dump() for st in statuses],
            }
        )
        if success:
            events.append(
                WorkerEvent(
                    run_id=state["run_id"],
                    skill_id=skill_id,
                    event_type=WorkerEventType.STEP_COMPLETED,
                    step_index=idx,
                    message=last_status.message or "Skill completed",
                )
            )
            state["current_skill_index"] = idx + 1
        else:
            events.append(
                WorkerEvent(
                    run_id=state["run_id"],
                    skill_id=skill_id,
                    event_type=WorkerEventType.STEP_FAILED,
                    step_index=idx,
                    message="Skill failed",
                    error=last_status.message,
                )
            )
            state["failed"] = True
    except Exception as exc:  # pylint: disable=broad-except
        state["last_error"] = str(exc)
        state["failed"] = True
        events.append(
            WorkerEvent(
                run_id=state["run_id"],
                skill_id=skill_id,
                event_type=WorkerEventType.STEP_FAILED,
                step_index=idx,
                message="Exception while executing skill",
                error=str(exc),
            )
        )

    return state


def _node_checkpoint(state: WorkerState, storage: WorkerStorage) -> WorkerState:
    events = state.setdefault("pending_events", [])
    checkpoint_payload = {
        "run_id": state["run_id"],
        "task": state.get("task"),
        "execution_plan": [p.model_dump() for p in state.get("execution_plan", [])],
        "current_skill_index": state.get("current_skill_index", 0),
        "failed": state.get("failed", False),
        "last_status": state.get("last_status").model_dump()
        if state.get("last_status")
        else None,
        "last_error": state.get("last_error"),
        "execution_history": state.get("execution_history", []),
        "replan_count": state.get("replan_count", 0),
        "max_replans": state.get("max_replans", 0),
    }

    storage.save_checkpoint(state["run_id"], checkpoint_payload)
    events.append(
        WorkerEvent(
            run_id=state["run_id"],
            skill_id="",
            event_type=WorkerEventType.CHECKPOINT_SAVED,
            step_index=state.get("current_skill_index", 0),
            message="Checkpoint saved",
            checkpoint=checkpoint_payload,
        )
    )
    return state


def _node_verify(state: WorkerState, llm_client=None, mcp_registry=None) -> WorkerState:
    events = state.setdefault("pending_events", [])
    result = verify_task_completion(
        task=state.get("task", ""),
        execution_history=state.get("execution_history", []),
        observations={},  # hook for semantic tree / OCR later
        llm_client=llm_client,
        mcp_registry=mcp_registry,
    )
    state["verification_result"] = result
    events.append(
        WorkerEvent(
            run_id=state["run_id"],
            skill_id="",
            event_type=WorkerEventType.LOG,
            message="Verification complete",
            metadata={"complete": str(result.get("complete", False))},
            verification_result=result.get("reason", ""),
        )
    )
    if not result.get("complete"):
        state["replan_count"] = state.get("replan_count", 0) + 1
    return state


def _find_skill(skills: List[SkillMap], skill_id: str) -> Optional[SkillMap]:
    for skill in skills:
        if skill.metadata.skill_id == skill_id:
            return skill
    return None


def build_worker_graph(
    storage: WorkerStorage,
    executor: StepExecutor,
    planner_fn,
    llm_client=None,
    mcp_registry=None,
) -> Any:
    """Build the Worker LangGraph with plan/execute/verify loop."""
    graph = StateGraph(WorkerState)

    graph.add_node("plan", lambda state: _node_plan(state, planner_fn, llm_client, mcp_registry))
    graph.add_node("execute_skill", lambda state: _node_execute_skill(state, executor))
    graph.add_node("checkpoint", lambda state: _node_checkpoint(state, storage))
    graph.add_node("verify", lambda state: _node_verify(state, llm_client, mcp_registry))

    graph.set_entry_point("plan")
    graph.add_edge("plan", "execute_skill")
    graph.add_edge("execute_skill", "checkpoint")

    def _route_after_checkpoint(state: WorkerState) -> str:
        if state.get("failed"):
            return "end"
        plan = state.get("execution_plan") or []
        idx = state.get("current_skill_index", 0)
        if idx < len(plan):
            return "execute_skill"
        return "verify"

    graph.add_conditional_edges(
        "checkpoint",
        _route_after_checkpoint,
        {
            "execute_skill": "execute_skill",
            "verify": "verify",
            "end": END,
        },
    )

    def _route_after_verify(state: WorkerState) -> str:
        if state.get("verification_result", {}).get("complete"):
            return "end"
        if state.get("replan_count", 0) >= state.get("max_replans", 0):
            state["failed"] = True
            return "end"
        return "plan"

    graph.add_conditional_edges(
        "verify",
        _route_after_verify,
        {
            "plan": "plan",
            "end": END,
        },
    )

    return graph.compile()


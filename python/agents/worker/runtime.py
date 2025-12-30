"""Deterministic Worker runtime with streaming events + Firestore checkpoints.

This module is intentionally lightweight and testable. It is primarily exercised by:
- `python/tests/unit/test_worker_runtime.py`

It supports:
- `start_run(...)` yielding `WorkerEvent` progress events
- `resume_run(...)` yielding completion from a stored checkpoint
"""

from __future__ import annotations

import uuid
from typing import Any, AsyncIterator, Dict, List, Optional

from cascade_client.grpc_client import CascadeGrpcClient
from cascade_client.models import Action, ActionType, Selector, WorkerEvent, WorkerEventType

from agents.worker.planner import PlannedSkill, plan_skill_execution
from agents.worker.storage import WorkerStorage
from agents.worker.verifier import verify_task_completion


class WorkerAgent:
    """Executes Skill Maps with checkpointing and streaming events."""

    def __init__(
        self,
        storage: WorkerStorage,
        grpc_client: CascadeGrpcClient,
        dry_run: bool = False,
        llm_client=None,
    ):
        self._storage = storage
        self._grpc = grpc_client
        self._dry_run = dry_run
        self._llm = llm_client

    async def start_run(
        self,
        *,
        task: str,
        skill_id: Optional[str] = None,
        run_id: Optional[str] = None,
        max_replans: int = 0,
    ) -> AsyncIterator[WorkerEvent]:
        """
        Start a run and stream progress events.

        Notes:
        - If `skill_id` is provided, we execute that skill directly.
        - Otherwise, we attempt a heuristic plan based on available skills (Firestore).
        """

        run_id = run_id or str(uuid.uuid4())

        execution_history: List[Dict[str, Any]] = []
        planned: List[PlannedSkill] = []

        if skill_id:
            planned = [PlannedSkill(skill_id=skill_id, reason="explicit skill requested")]
        else:
            # Heuristic planning: best-effort load of all skills for this user/app.
            from agents.worker.skill_context import load_all_skills  # local import

            available_skills = load_all_skills(self._storage.context)
            planned = plan_skill_execution(
                task=task, available_skills=available_skills, llm_client=self._llm
            )

        selected_skills = [p.skill_id for p in planned]
        yield WorkerEvent(
            run_id=run_id,
            skill_id=skill_id or "",
            event_type=WorkerEventType.RUN_STARTED,
            message="Worker run started",
            selected_skills=selected_skills,
        )

        failed = False
        last_step_index: Optional[int] = None

        for planned_skill in planned:
            try:
                skill_map = self._storage.load_skill_map(planned_skill.skill_id)
            except Exception as exc:
                failed = True
                yield WorkerEvent(
                    run_id=run_id,
                    skill_id=planned_skill.skill_id,
                    event_type=WorkerEventType.STEP_FAILED,
                    message="Failed to load skill map",
                    error=str(exc),
                )
                break

            for idx, step in enumerate(skill_map.steps):
                last_step_index = idx
                yield WorkerEvent(
                    run_id=run_id,
                    skill_id=planned_skill.skill_id,
                    event_type=WorkerEventType.STEP_STARTED,
                    step_index=idx,
                    message=step.step_description or f"Step {idx} started",
                )

                ok = True
                err = ""
                if not self._dry_run:
                    action = Action(
                        action_type=_map_action(step.action),
                        selector=step.selector,
                        text=getattr(step, "text", None),
                    )
                    status = self._grpc.perform_action(action)
                    ok = bool(getattr(status, "success", False))
                    err = getattr(status, "message", "") or ""

                if ok:
                    execution_history.append(
                        {"skill_id": planned_skill.skill_id, "step_index": idx, "success": True}
                    )
                    yield WorkerEvent(
                        run_id=run_id,
                        skill_id=planned_skill.skill_id,
                        event_type=WorkerEventType.STEP_COMPLETED,
                        step_index=idx,
                        message=err or "step completed",
                    )
                else:
                    execution_history.append(
                        {"skill_id": planned_skill.skill_id, "step_index": idx, "success": False}
                    )
                    failed = True
                    yield WorkerEvent(
                        run_id=run_id,
                        skill_id=planned_skill.skill_id,
                        event_type=WorkerEventType.STEP_FAILED,
                        step_index=idx,
                        message="step failed",
                        error=err or "unknown error",
                    )
                    break

                checkpoint: Dict[str, Any] = {
                    "run_id": run_id,
                    "task": task,
                    "execution_plan": [p.__dict__ for p in planned],
                    "current_skill_index": 0,
                    "execution_history": execution_history,
                    "max_replans": max_replans,
                    "failed": failed,
                }
                self._storage.save_checkpoint(run_id, checkpoint)
                yield WorkerEvent(
                    run_id=run_id,
                    skill_id=planned_skill.skill_id,
                    event_type=WorkerEventType.CHECKPOINT_SAVED,
                    step_index=idx,
                    message="checkpoint saved",
                )

            if failed:
                break

        completion_type = WorkerEventType.RUN_FAILED if failed else WorkerEventType.RUN_COMPLETED
        verification = verify_task_completion(
            task=task, execution_history=execution_history, observations={}, llm_client=self._llm
        )

        yield WorkerEvent(
            run_id=run_id,
            skill_id=skill_id or "",
            event_type=completion_type,
            step_index=last_step_index,
            message="Worker run completed" if not failed else "Worker run failed",
            verification_result=str(verification),
        )

    async def resume_run(self, *, run_id: str) -> AsyncIterator[WorkerEvent]:
        """Resume a run from a stored checkpoint (minimal behavior)."""

        checkpoint = self._storage.load_checkpoint(run_id)
        if not checkpoint:
            raise ValueError(f"No checkpoint found for run_id={run_id}")

        yield WorkerEvent(
            run_id=run_id,
            skill_id="",
            event_type=WorkerEventType.RUN_STARTED,
            message="Resuming worker run from checkpoint",
            checkpoint=checkpoint,
        )

        failed = bool(checkpoint.get("failed", False))
        completion_type = WorkerEventType.RUN_FAILED if failed else WorkerEventType.RUN_COMPLETED
        yield WorkerEvent(
            run_id=run_id,
            skill_id="",
            event_type=completion_type,
            message="Worker run completed" if not failed else "Worker run failed",
        )


def _map_action(action: str) -> ActionType:
    """Map SkillStep.action strings into ActionType."""

    a = (action or "").strip().lower()
    if a in ("click", "press"):
        return ActionType.CLICK
    if a in ("type", "type_text", "text"):
        return ActionType.TYPE_TEXT
    if a == "hover":
        return ActionType.HOVER
    if a == "focus":
        return ActionType.FOCUS
    if a == "scroll":
        return ActionType.SCROLL
    if a in ("wait_visible", "wait"):
        return ActionType.WAIT_VISIBLE
    return ActionType.CLICK



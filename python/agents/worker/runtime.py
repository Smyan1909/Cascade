"""Worker runtime that loads Skill Maps and executes them with LangGraph."""

from __future__ import annotations

import asyncio
import uuid
from typing import Any, AsyncIterator, Dict, Optional

from cascade_client.auth.context import CascadeContext
from cascade_client.a2a import AgentA2AClient
from cascade_client.grpc_client import CascadeGrpcClient
from cascade_client.models import AgentMessage, WorkerEvent, WorkerEventType

from agents.worker.graph import StepExecutor, WorkerState, build_worker_graph
from agents.worker.planner import PlannedSkill, plan_skill_execution
from agents.worker.skill_loader import get_skill_descriptions, load_all_skills
from agents.worker.storage import WorkerStorage
from agents.worker.verifier import verify_task_completion
from clients.llm_client import load_llm_client_from_env
from mcp_server.body_tools import register_body_tools
from mcp_server.explorer_tools import register_explorer_tools
from mcp_server.skill_tools import register_skill_tools
from mcp_server.tool_registry import ToolRegistry


class WorkerAgent:
    """Executes Explorer-generated Skill Maps with checkpointing and streaming."""

    def __init__(
        self,
        storage: WorkerStorage,
        grpc_client: CascadeGrpcClient,
        dry_run: bool = False,
        llm_client=None,
        mcp_registry: Optional[ToolRegistry] = None,
        a2a_client: Optional[AgentA2AClient] = None,
    ):
        self._storage = storage
        self._grpc = grpc_client
        self._dry_run = dry_run
        try:
            self._llm = llm_client or load_llm_client_from_env()
        except Exception:
            self._llm = None

        self._a2a_client = a2a_client or AgentA2AClient(
            storage.context, grpc_client, role="worker"
        )
        self._a2a_task: Optional[asyncio.Task] = None

        # Setup MCP registry if not provided
        if mcp_registry is None:
            self._mcp_registry = ToolRegistry()
            register_body_tools(self._mcp_registry, grpc_client)
            register_explorer_tools(self._mcp_registry)
            register_skill_tools(self._mcp_registry, storage.context, grpc_client)
        else:
            self._mcp_registry = mcp_registry

    async def start_run(
        self,
        task: str,
        run_id: Optional[str] = None,
        max_replans: int = 2,
        skill_id: Optional[str] = None,
    ) -> AsyncIterator[WorkerEvent]:
        """Start a new worker run and stream events."""
        run_id = run_id or str(uuid.uuid4())
        all_skills = load_all_skills(self._storage.context)
        if skill_id:
            # Filter to specific skill if requested
            all_skills = [s for s in all_skills if s.metadata.skill_id == skill_id]

        executor = StepExecutor(self._grpc, dry_run=self._dry_run)
        graph = build_worker_graph(
            self._storage,
            executor,
            planner_fn=plan_skill_execution,
            llm_client=self._llm,
        )

        initial_state: WorkerState = {
            "context": self._storage.context,
            "run_id": run_id,
            "task": task,
            "available_skills": all_skills,
            "execution_plan": [],
            "current_skill_index": 0,
            "execution_history": [],
            "pending_events": [],
            "dry_run": self._dry_run,
            "metadata": {},
            "replan_count": 0,
            "max_replans": max_replans,
        }

        yield WorkerEvent(
            run_id=run_id,
            skill_id=skill_id or "",
            event_type=WorkerEventType.RUN_STARTED,
            message="Worker run started",
            selected_skills=[s.metadata.skill_id for s in all_skills],
        )

        last_step_index: Optional[int] = None
        failed = False
        await self._ensure_a2a_listener(run_id)
        try:
            async for event in self._stream_graph(graph, initial_state):
                if event.step_index is not None:
                    last_step_index = event.step_index
                if event.event_type == WorkerEventType.STEP_FAILED:
                    failed = True
                yield event
        finally:
            await self._stop_a2a_listener()

        completion_type = (
            WorkerEventType.RUN_COMPLETED if not failed else WorkerEventType.RUN_FAILED
        )
        yield WorkerEvent(
            run_id=run_id,
            skill_id=skill_id or "",
            event_type=completion_type,
            step_index=last_step_index,
            message="Worker run completed" if not failed else "Worker run failed",
        )

    async def resume_run(self, run_id: str) -> AsyncIterator[WorkerEvent]:
        """Resume from a checkpoint if available."""
        checkpoint = self._storage.load_checkpoint(run_id)
        if not checkpoint:
            raise ValueError(f"No checkpoint found for run_id={run_id}")

        all_skills = load_all_skills(self._storage.context)
        executor = StepExecutor(self._grpc, dry_run=self._dry_run)
        # Refresh skill tools before building graph
        from mcp_server.skill_tools import refresh_skill_tools

        refresh_skill_tools(self._mcp_registry, self._storage.context, self._grpc)
        graph = build_worker_graph(
            self._storage,
            executor,
            planner_fn=plan_skill_execution,
            llm_client=self._llm,
            mcp_registry=self._mcp_registry,
        )

        initial_state: WorkerState = {
            "context": self._storage.context,
            "run_id": run_id,
            "task": checkpoint.get("task", ""),
            "available_skills": all_skills,
            "execution_plan": [
                PlannedSkill.model_validate(p)
                if isinstance(p, dict)
                else p
                for p in checkpoint.get("execution_plan", [])
            ],
            "current_skill_index": checkpoint.get("current_skill_index", 0),
            "execution_history": checkpoint.get("execution_history", []),
            "pending_events": [],
            "dry_run": self._dry_run,
            "metadata": {},
            "failed": checkpoint.get("failed", False),
            "replan_count": checkpoint.get("replan_count", 0),
            "max_replans": checkpoint.get("max_replans", 0),
        }

        yield WorkerEvent(
            run_id=run_id,
            skill_id="",
            event_type=WorkerEventType.RUN_STARTED,
            message="Resuming worker run from checkpoint",
            checkpoint=checkpoint,
        )

        last_step_index: Optional[int] = checkpoint.get("step_index")
        failed = checkpoint.get("failed", False)
        await self._ensure_a2a_listener(run_id)
        try:
            async for event in self._stream_graph(graph, initial_state):
                if event.step_index is not None:
                    last_step_index = event.step_index
                if event.event_type == WorkerEventType.STEP_FAILED:
                    failed = True
                yield event
        finally:
            await self._stop_a2a_listener()

        completion_type = (
            WorkerEventType.RUN_COMPLETED
            if not failed
            else WorkerEventType.RUN_FAILED
        )
        yield WorkerEvent(
            run_id=run_id,
            skill_id="",
            event_type=completion_type,
            step_index=last_step_index,
            message="Worker run completed" if not failed else "Worker run failed",
        )

    async def _stream_graph(
        self, graph: Any, initial_state: WorkerState
    ) -> AsyncIterator[WorkerEvent]:
        """Stream LangGraph state transitions as WorkerEvents."""
        loop = asyncio.get_event_loop()

        # LangGraph's stream is synchronous; run in executor to avoid blocking.
        def _run_stream():
            for state in graph.stream(initial_state, stream_mode="values"):
                yield state

        queue: asyncio.Queue = asyncio.Queue()

        def _pump():
            try:
                for state in _run_stream():
                    loop.call_soon_threadsafe(queue.put_nowait, state)
            finally:
                loop.call_soon_threadsafe(queue.put_nowait, None)

        pump_future = loop.run_in_executor(None, _pump)

        while True:
            state = await queue.get()
            if state is None:
                break
            events = state.get("pending_events", [])
            for event in events:
                yield event
            events.clear()

        await pump_future

    async def _ensure_a2a_listener(self, run_id: str) -> None:
        """Start A2A listener with idempotent handler."""
        if self._a2a_task and not self._a2a_task.done():
            return

        # Scope registration to this run for routing.
        self._a2a_client._run_id = run_id  # internal mutation to reuse client

        async def _listener():
            await self._a2a_client.listen(self._handle_a2a_message)

        self._a2a_task = asyncio.create_task(_listener())

    async def _stop_a2a_listener(self) -> None:
        """Stop A2A listener gracefully."""
        if self._a2a_task:
            await self._a2a_client.stop()
            try:
                await asyncio.wait_for(self._a2a_task, timeout=1.0)
            except asyncio.TimeoutError:
                self._a2a_task.cancel()
        self._a2a_task = None

    async def _handle_a2a_message(self, message: AgentMessage) -> None:
        """
        Idempotent handler for inbound A2A messages.

        Currently logs receipt; extend to route commands or share checkpoints.
        """
        print(
            f"[A2A][worker] received message {message.message_id} "
            f"from {message.sender_role or message.sender_agent_id}"
        )


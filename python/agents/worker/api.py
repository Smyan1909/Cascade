"""Public API surface and gRPC servicer for the Worker agent."""

from __future__ import annotations

from typing import AsyncIterator, Optional

from cascade_client.auth.context import CascadeContext
from cascade_client.grpc_client import CascadeGrpcClient
from cascade_client.models import WorkerEvent, WorkerResumeRequest, WorkerRunRequest

from agents.worker.runtime import WorkerAgent
from agents.worker.storage import WorkerStorage


class WorkerAPI:
    """Python-friendly API facade for starting/resuming worker runs."""

    def __init__(self, context: CascadeContext, grpc_client: CascadeGrpcClient):
        self._storage = WorkerStorage(context)
        self._agent = WorkerAgent(self._storage, grpc_client)

    async def start(
        self,
        task: str,
        run_id: Optional[str] = None,
        max_replans: int = 2,
        skill_id: Optional[str] = None,
    ) -> AsyncIterator[WorkerEvent]:
        """Start a worker run using skill map pulled from Firestore."""
        async for event in self._agent.start_run(
            task=task, run_id=run_id, max_replans=max_replans, skill_id=skill_id
        ):
            yield event

    async def resume(self, run_id: str) -> AsyncIterator[WorkerEvent]:
        """Resume a worker run from checkpoint."""
        async for event in self._agent.resume_run(run_id=run_id):
            yield event


def build_worker_servicer(api: WorkerAgent):
    """
    Create a gRPC servicer bound to the provided WorkerAgent.

    The caller is responsible for adding it to a grpc.aio server:
        server = grpc.aio.server()
        cascade_pb2_grpc.add_WorkerServiceServicer_to_server(servicer, server)
    """
    import cascade_client.proto.cascade_pb2 as cascade_pb2
    import cascade_client.proto.cascade_pb2_grpc as cascade_pb2_grpc

    class _WorkerServicer(cascade_pb2_grpc.WorkerServiceServicer):
        async def StartWorkerRun(self, request, context):  # type: ignore[override]
            worker_request = WorkerRunRequest.from_proto(request)
            async for event in api.start_run(
                task=worker_request.task or "",
                run_id=worker_request.run_id or None,
                max_replans=worker_request.max_replans,
                skill_id=worker_request.skill_id or None,
            ):
                yield event.to_proto()

        async def ResumeWorkerRun(self, request, context):  # type: ignore[override]
            resume_request = WorkerResumeRequest.from_proto(request)
            async for event in api.resume_run(resume_request.run_id):
                yield event.to_proto()

    return _WorkerServicer()


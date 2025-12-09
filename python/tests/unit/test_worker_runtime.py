import asyncio
import pytest

pytest.importorskip("langgraph")

from cascade_client.auth.context import CascadeContext
from cascade_client.grpc_client import CascadeGrpcClient
from cascade_client.models import PlatformSource, Selector, Status, WorkerEventType

from agents.explorer.skill_map import SkillMap, SkillMetadata, SkillStep
from agents.worker.runtime import WorkerAgent
from agents.worker.storage import WorkerStorage


class FakeGrpc(CascadeGrpcClient):
    def __init__(self):
        # Skip parent init; not calling real stubs
        pass

    def perform_action(self, action):
        return Status(success=True, message="ok")


class FakeFirestore:
    def __init__(self, skill_map_data):
        self.skill_map_data = skill_map_data
        self.checkpoints = {}

    def get_document(self, path):
        if "skill_maps" in path:
            return self.skill_map_data
        return self.checkpoints.get(path)

    def save_checkpoint(self, path, state):
        self.checkpoints[path] = state

    def load_checkpoint(self, path):
        return self.checkpoints.get(path)


def _build_skill_map():
    meta = SkillMetadata(skill_id="skill-1", app_id="app-1", user_id="user-1")
    step = SkillStep(
        action="Click",
        selector=Selector(platform_source=PlatformSource.WINDOWS, path=["Button"]),
    )
    return SkillMap(metadata=meta, steps=[step])


@pytest.mark.asyncio
async def test_worker_run_streams_events():
    skill_map = _build_skill_map()
    fake_fs = FakeFirestore(skill_map.model_dump(mode="json"))
    ctx = CascadeContext(user_id="user-1", app_id="app-1", auth_token="token")
    storage = WorkerStorage(ctx, firestore_client=fake_fs)  # type: ignore[arg-type]
    agent = WorkerAgent(storage, grpc_client=FakeGrpc())  # type: ignore[arg-type]

    events = []
    async for event in agent.start_run(task="do something", skill_id=skill_map.metadata.skill_id, run_id="run-1"):
        events.append(event)

    event_types = [e.event_type for e in events]
    assert event_types[0] == WorkerEventType.RUN_STARTED
    assert WorkerEventType.STEP_STARTED in event_types
    assert WorkerEventType.STEP_COMPLETED in event_types
    assert WorkerEventType.CHECKPOINT_SAVED in event_types
    assert event_types[-1] == WorkerEventType.RUN_COMPLETED


@pytest.mark.asyncio
async def test_worker_resume_uses_checkpoint():
    skill_map = _build_skill_map()
    fake_fs = FakeFirestore(skill_map.model_dump(mode="json"))
    ctx = CascadeContext(user_id="user-1", app_id="app-1", auth_token="token")
    storage = WorkerStorage(ctx, firestore_client=fake_fs)  # type: ignore[arg-type]
    agent = WorkerAgent(storage, grpc_client=FakeGrpc())  # type: ignore[arg-type]

    # Seed checkpoint indicating first step done
    checkpoint_path = ctx.get_worker_checkpoint_path("run-1")
    fake_fs.checkpoints[checkpoint_path] = {
        "run_id": "run-1",
        "task": "do something",
        "execution_plan": [],
        "current_skill_index": 0,
        "execution_history": [],
        "max_replans": 0,
        "failed": False,
    }

    events = []
    async for event in agent.resume_run(run_id="run-1"):
        events.append(event)

    assert events[0].event_type == WorkerEventType.RUN_STARTED
    assert events[-1].event_type == WorkerEventType.RUN_COMPLETED


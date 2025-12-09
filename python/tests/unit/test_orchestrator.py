"""Unit tests for orchestrator routing, storage, and graph flow."""

from __future__ import annotations

import uuid

from orchestrator.graph import build_orchestrator_graph
from orchestrator.router import OrchestratorRouter
from orchestrator.state import ExecutionRecord, OrchestratorCheckpoint
from orchestrator.storage import OrchestratorStorage

from cascade_client.auth.context import CascadeContext
from agents.explorer.skill_map import SkillMap, SkillMetadata


def _skill(skill_id: str, capability: str, description: str = "") -> SkillMap:
    meta = SkillMetadata(
        skill_id=skill_id,
        app_id="app",
        user_id="user",
        description=description or capability,
        capability=capability,
    )
    return SkillMap(metadata=meta, steps=[])


class FakeFirestoreClient:
    def __init__(self):
        self.saved_path = None
        self.saved_state = None

    def save_checkpoint(self, path, state):
        self.saved_path = path
        self.saved_state = state

    def load_checkpoint(self, path):
        return self.saved_state


class DummyGrpcClient:
    """Stub to satisfy AgentA2AClient creation; methods unused in dry-run tests."""

    async def register_agent_async(self, request):
        return type("Resp", (), {"agent_id": "agent"})

    async def send_agent_message_async(self, msg):
        return None

    async def stream_agent_inbox_async(self, req):
        if False:
            yield None

    async def ack_agent_message_async(self, ack):
        return None


def test_router_prefers_explicit_skill():
    router = OrchestratorRouter([_skill("s1", "login")])
    subgoal = router.plan_subgoals("Do login", ["s1"])[0]
    decision = router.choose_executor(subgoal)
    assert decision.executor_type == "worker"
    assert decision.skill_id == "s1"


def test_router_falls_back_to_explorer_when_no_match():
    router = OrchestratorRouter([_skill("s1", "billing")])
    subgoal = router.plan_subgoals("Reset password")[0]
    decision = router.choose_executor(subgoal)
    assert decision.executor_type == "explorer"


def test_storage_round_trip_checkpoint():
    ctx = CascadeContext(user_id="u", app_id="a", auth_token="t")
    fake_fs = FakeFirestoreClient()
    storage = OrchestratorStorage(ctx, client=fake_fs)  # type: ignore[arg-type]

    checkpoint = OrchestratorCheckpoint(run_id="r1", goal="g", subgoals=[], decisions=[])
    storage.save_checkpoint(checkpoint)
    loaded = storage.load_checkpoint("r1")
    assert loaded is not None
    assert loaded.run_id == "r1"
    assert fake_fs.saved_path.endswith("orchestrator_checkpoints/r1")


def test_graph_dry_run_checkpoint_and_progress():
    ctx = CascadeContext(user_id="u", app_id="a", auth_token="t")
    router = OrchestratorRouter([_skill("s1", "login")])
    fake_fs = FakeFirestoreClient()
    storage = OrchestratorStorage(ctx, client=fake_fs)  # type: ignore[arg-type]
    graph = build_orchestrator_graph(ctx, DummyGrpcClient(), router, storage, a2a_client=None)

    run_id = str(uuid.uuid4())
    result = graph.invoke({"context": ctx, "run_id": run_id, "goal": "login user", "dry_run": True})

    assert result.get("current_index") == 1
    decisions = result.get("decisions", [])
    assert len(decisions) == 1
    assert decisions[0].executor_type == "worker"
    records = result.get("records", [])
    assert len(records) == 1
    assert isinstance(records[0], ExecutionRecord)
    assert records[0].status == "completed"
    assert fake_fs.saved_state is not None


def test_resume_from_checkpoint_uses_saved_subgoals():
    ctx = CascadeContext(user_id="u", app_id="a", auth_token="t")
    router = OrchestratorRouter([_skill("s1", "login")])
    fake_fs = FakeFirestoreClient()
    storage = OrchestratorStorage(ctx, client=fake_fs)  # type: ignore[arg-type]
    graph = build_orchestrator_graph(ctx, DummyGrpcClient(), router, storage, a2a_client=None)

    run_id = "resume-run"
    first = graph.invoke({"context": ctx, "run_id": run_id, "goal": "login user", "dry_run": True})
    assert fake_fs.saved_state is not None

    resumed = graph.invoke({"context": ctx, "run_id": run_id, "goal": "ignored", "dry_run": True, "resume": True})
    assert resumed.get("subgoals")
    # Ensure resume did not create duplicate subgoals
    assert len(resumed["subgoals"]) == len(first["subgoals"])



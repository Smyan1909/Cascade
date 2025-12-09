import pytest

pytest.importorskip("langgraph")

from agents.explorer.skill_map import SkillMap, SkillMetadata, SkillStep
from agents.worker.planner import plan_skill_execution
from agents.worker.verifier import verify_task_completion
from cascade_client.models import PlatformSource, Selector


def _skill(skill_id: str, capability: str, description: str = "") -> SkillMap:
    meta = SkillMetadata(
        skill_id=skill_id,
        app_id="app",
        user_id="user",
        capability=capability,
        description=description or capability,
    )
    step = SkillStep(
        action="Click",
        selector=Selector(platform_source=PlatformSource.WINDOWS, path=["x"]),
    )
    return SkillMap(metadata=meta, steps=[step])


def test_plan_skill_execution_heuristic_selects_matching_capability():
    skills = [
        _skill("login", "login"),
        _skill("save", "save_file"),
        _skill("search", "search"),
    ]
    plan = plan_skill_execution(task="please search and find item", available_skills=skills, llm_client=None)
    assert plan, "expected a plan"
    assert plan[0].skill_id in {"search"}, "should pick relevant capability first"


def test_verify_task_completion_heuristic_passes_when_history_exists():
    history = [{"skill_id": "x", "success": True}]
    result = verify_task_completion(task="anything", execution_history=history, observations={}, llm_client=None)
    assert result["complete"] is True


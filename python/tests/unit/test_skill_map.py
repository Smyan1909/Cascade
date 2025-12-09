import datetime

from agents.explorer.skill_map import SkillMap, SkillMetadata, SkillStep, default_action_for_method


def test_skill_map_preferred_api_when_available():
    metadata = SkillMetadata(skill_id="s1", app_id="app", user_id="user", preferred_method="api")
    step = SkillStep(action="CallAPI", api_endpoint=None, selector=None)
    skill = SkillMap(metadata=metadata, steps=[step])
    assert skill.choose_method_for_step(step) == "api"


def test_default_action_for_method():
    assert default_action_for_method("api") == "CallAPI"


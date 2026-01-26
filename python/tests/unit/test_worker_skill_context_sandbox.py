from agents.explorer.skill_map import SkillMap, SkillMetadata, SkillStep
from agents.worker.skill_context import categorize_skill, format_skill_as_context


def test_categorize_skill_prefers_sandbox():
    meta = SkillMetadata(
        skill_id="s1",
        app_id="app",
        user_id="user",
        preferred_method="sandbox",
        sandbox={"provider": "e2b", "python_packages": ["openpyxl"]},
    )
    skill = SkillMap(metadata=meta, steps=[SkillStep(action="RunSandbox")])
    assert categorize_skill(skill) == "python_sandbox"


def test_format_skill_as_context_includes_packages():
    meta = SkillMetadata(
        skill_id="s1",
        app_id="app",
        user_id="user",
        preferred_method="sandbox",
        sandbox={"provider": "e2b", "python_packages": ["openpyxl"]},
    )
    skill = SkillMap(metadata=meta, steps=[SkillStep(action="RunSandbox")])
    txt = format_skill_as_context(skill)
    assert "Python Sandbox" in txt
    assert "openpyxl" in txt



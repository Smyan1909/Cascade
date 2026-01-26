from agents.explorer.skill_map import SkillMap, SkillMetadata, SkillStep


def test_skill_map_accepts_sandbox_metadata():
    meta = SkillMetadata(
        skill_id="s1",
        app_id="app",
        user_id="user",
        preferred_method="sandbox",
        sandbox={
            "provider": "e2b",
            "python_packages": ["openpyxl"],
            "functions": {
                "open_workbook": {
                    "module": "openpyxl",
                    "function": "load_workbook",
                    "description": "Load a workbook from disk",
                }
            },
            "file_io": {
                "inputs": [{"name": "workbook", "file_glob": "*.xlsx", "required": True}],
                "outputs": [{"name": "workbook", "file_glob": "*.xlsx", "required": True}],
            },
            "entrypoint": "open_workbook",
        },
    )
    skill = SkillMap(metadata=meta, steps=[SkillStep(action="RunSandbox")])
    dumped = skill.to_firestore()
    assert dumped["metadata"]["sandbox"]["provider"] == "e2b"



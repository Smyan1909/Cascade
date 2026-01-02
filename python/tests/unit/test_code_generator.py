from agents.explorer.code_generator import CodeGenerator
from storage.firestore_client import FirestoreClient
from agents.explorer.skill_map import SkillMap, SkillMetadata, SkillStep


class DummyFS(FirestoreClient):
    def __init__(self):
        pass

    def save_code_artifact(self, artifact_id, payload):
        self.last_payload = payload


def test_code_generator_outputs_worker_content():
    metadata = SkillMetadata(skill_id="s1", app_id="app", user_id="user")
    step = SkillStep(action="CLICK", selector=None, api_endpoint=None)
    skill = SkillMap(metadata=metadata, steps=[step])
    fs = DummyFS()
    gen = CodeGenerator(fs)
    artifact_id, artifact = gen.generate(skill)
    assert artifact.files[0].path.endswith("s1.py")
    assert "Step 1" in artifact.files[0].content


def test_code_generator_csharp_placeholder_is_not_success():
    metadata = SkillMetadata(skill_id="s_excel", app_id="excel", user_id="user")
    step = SkillStep(action="CLICK", selector=None, api_endpoint=None)
    skill = SkillMap(metadata=metadata, steps=[step])
    fs = DummyFS()
    gen = CodeGenerator(fs, llm=None)
    artifact_id, artifact = gen.generate(skill)
    assert artifact.files[0].path.endswith("s_excel.cs")
    # Ensure placeholder can't be mistaken as successful automation.
    assert "success = false" in artifact.files[0].content
    assert "NOT_IMPLEMENTED" in artifact.files[0].content
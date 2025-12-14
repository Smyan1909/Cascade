from storage.code_artifact import CodeArtifact, CodeFile


def test_code_artifact_to_firestore():
    artifact = CodeArtifact(skill_id="s1", files=[CodeFile(path="a.py", content="print(1)")])
    data = artifact.to_firestore()
    assert data["skill_id"] == "s1"
    assert data["files"][0]["path"] == "a.py"


import types

import pytest


def test_generate_python_code_for_skill_uses_llm(monkeypatch):
    # Import inside test to ensure monkeypatch applies.
    from mcp_server import sandbox_tools
    from agents.explorer.skill_map import SkillMap, SkillMetadata, SkillStep

    class DummyLLM:
        def generate(self, messages, **kwargs):
            # Return minimal valid python that defines main(payload).
            return types.SimpleNamespace(
                content="def main(payload: dict) -> dict:\n    return {'success': True}\n"
            )

    monkeypatch.setattr(
        sandbox_tools,
        "load_llm_client_from_env",
        lambda: DummyLLM(),
        raising=False,
    )

    # But sandbox_tools imports load_llm_client_from_env from clients.llm_client inside function,
    # so patch there too.
    import clients.llm_client as llm_client

    monkeypatch.setattr(llm_client, "load_llm_client_from_env", lambda: DummyLLM())

    meta = SkillMetadata(
        skill_id="s1",
        app_id="app",
        user_id="user",
        preferred_method="sandbox",
        sandbox={"provider": "e2b", "python_packages": ["openpyxl"], "functions": {}},
    )
    skill = SkillMap(metadata=meta, steps=[SkillStep(action="RunSandbox")])

    code = sandbox_tools._generate_python_code_for_skill(
        task="update cell A1 to 5",
        skill=skill,
        file_descriptors=[{"name": "workbook", "mode": "inout", "sandbox_path": "/home/user/workbook.xlsx"}],
    )
    assert "def main" in code



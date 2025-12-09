"""Generate worker/orchestrator code artifacts."""

from __future__ import annotations

import uuid
from typing import List, Tuple

from storage.code_artifact import CodeArtifact, CodeFile
from storage.firestore_client import FirestoreClient

from clients.llm_client import LlmClient, LlmMessage
from .skill_map import SkillMap


class CodeGenerator:
    """Produce code artifacts and store them in Firestore."""

    def __init__(self, fs: FirestoreClient, llm: LlmClient | None = None):
        self._fs = fs
        self._llm = llm

    def _build_worker_file(self, skill: SkillMap) -> CodeFile:
        lines = [
            "# Auto-generated worker derived from Skill Map",
            "from cascade_client.grpc_client import CascadeGrpcClient",
            "from cascade_client.models import Action, ActionType",
            "",
            "def run(client: CascadeGrpcClient, inputs: dict):",
            "    # inputs may contain runtime parameters referenced by steps",
        ]
        for idx, step in enumerate(skill.steps):
            prefix = f"    # Step {idx+1}: {step.action}"
            lines.append(prefix)
            if step.api_endpoint:
                lines.append(
                    f"    # API call to {step.api_endpoint.method} {step.api_endpoint.url}"
                )
                lines.append("    # TODO: integrate API call logic with auth/headers")
                lines.append("    # response = requests.request(...)")
            elif step.selector:
                lines.append("    action = Action(")
                lines.append(f"        action_type=ActionType.{step.action.upper()},")
                lines.append(f"        selector={step.selector!r},")
                if step.inputs and "text" in step.inputs:
                    lines.append(f"        text={step.inputs['text']!r},")
                lines.append("    )")
                lines.append("    client.perform_action(action)")
        lines.append(f"    return 'skill:{skill.metadata.skill_id}'")
        content = "\n".join(lines) + "\n"
        return CodeFile(path=f"worker_{skill.metadata.skill_id}.py", content=content, language="python")

    def _build_orchestrator_file(self, skill: SkillMap) -> CodeFile:
        content = (
            "# Auto-generated orchestrator hook\n"
            "def route(goal: str, skills: list):\n"
            "    return skills\n"
        )
        return CodeFile(path="orchestrator_hooks.py", content=content, language="python")

    def generate(self, skill: SkillMap) -> Tuple[str, CodeArtifact]:
        artifact_id = str(uuid.uuid4())
        files: List[CodeFile] = [
            self._build_worker_file(skill),
            self._build_orchestrator_file(skill),
        ]
        if self._llm:
            try:
                _ = self._llm.generate(
                    [LlmMessage(role="system", content="Review generated code for safety.")]
                )
            except Exception:
                pass
        artifact = CodeArtifact(skill_id=skill.metadata.skill_id, files=files)
        self._fs.save_code_artifact(artifact_id, artifact.to_firestore())
        return artifact_id, artifact


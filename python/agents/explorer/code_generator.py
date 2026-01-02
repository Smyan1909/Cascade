"""Generate executable code artifacts for skills (Python or C#)."""

from __future__ import annotations

import json
import uuid
from typing import Any, Dict, List, Optional, Tuple

from storage.code_artifact import CodeArtifact, CodeFile
from storage.firestore_client import FirestoreClient

from clients.llm_client import LlmClient, LlmMessage
from .skill_map import SkillMap


class CodeGenerator:
    """Produce code artifacts and store them in Firestore."""

    def __init__(self, fs: FirestoreClient, llm: LlmClient | None = None):
        self._fs = fs
        self._llm = llm

    def _build_python_file(self, skill: SkillMap) -> CodeFile:
        lines = [
            "# Auto-generated executable skill (Python)",
            "import os",
            "from cascade_client.grpc_client import CascadeGrpcClient",
            "from cascade_client.models import Action, ActionType",
            "",
            "def run(inputs: dict):",
            "    # Entrypoint executed by Brain Python executor. Uses CASCADE_GRPC_ENDPOINT.",
            "    endpoint = os.environ.get('CASCADE_GRPC_ENDPOINT')",
            "    if not endpoint:",
            "        raise ValueError('CASCADE_GRPC_ENDPOINT is required')",
            "    client = CascadeGrpcClient(endpoint=endpoint)",
            "    # inputs may contain runtime parameters referenced by steps",
        ]
        for idx, step in enumerate(skill.steps):
            prefix = f"    # Step {idx+1}: {step.action}"
            lines.append(prefix)
            if step.api_endpoint:
                lines.append(
                    f"    # API call to {step.api_endpoint.method} {step.api_endpoint.url}"
                )
                lines.append("    # NOTE: prefer call_http_api tool in Worker when possible.")
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
        return CodeFile(path=f"skill_{skill.metadata.skill_id}.py", content=content, language="python")

    def _csharp_placeholder(self) -> str:
        # IMPORTANT: This must return success=false so the agent cannot treat "compiled + executed" as task success.
        return """// Auto-generated executable skill (C#)
using System;
using System.Text.Json;

public static class SkillEntrypoint
{
    // Body executes this entrypoint via Roslyn.
    // This is a placeholder for native API automation.
    public static string Run(string inputsJson)
    {
        using var _ = JsonDocument.Parse(string.IsNullOrWhiteSpace(inputsJson) ? "{}" : inputsJson);
        return JsonSerializer.Serialize(new {
            success = false,
            error = new {
                code = "NOT_IMPLEMENTED",
                message = "No native implementation generated for this skill. Use UI automation steps instead."
            }
        });
    }
}
"""

    def _generate_csharp_via_llm(self, skill: SkillMap) -> Optional[str]:
        """Generate C# code for this skill via the configured LLM.

        Returns:
            Code string, or None if generation failed.
        """
        if not self._llm:
            return None

        # Provide the skill in a structured form. Avoid dumping huge trees.
        payload = skill.model_dump(mode="json")
        # Trim potentially large fields if they exist (defensive).
        payload.get("metadata", {}).pop("initial_state_tree", None)
        skill_json = json.dumps(payload, indent=2)

        system = """You generate a SINGLE C# source file implementing a Cascade code artifact for desktop automation.

Hard requirements:
- Output ONLY C# code (no markdown, no code fences).
- Must define: public static class SkillEntrypoint { public static string Run(string inputsJson) { ... } }
- Run must return a JSON string. Top-level must include: { "success": true|false }.
- If you cannot implement safely, return success=false with an error object.

Safety constraints (Body will reject or fail):
- Do NOT use System.IO, System.Net, Process/ProcessStartInfo.
- Avoid spawning processes, file reads/writes, or network.
- You MAY use COM automation via dynamic + Marshal.GetActiveObject/Activator.CreateInstance, and System.Runtime.InteropServices.

Behavior requirements:
- Parse inputsJson (JSON) and use it to drive the automation.
- Prefer attaching to existing app instance when possible.
- Be robust: validate required inputs; on missing/invalid inputs, return success=false with a helpful error message.
"""

        user = f"""Generate the code artifact for this SkillMap (JSON below). The artifact should implement THIS ONE skill only.

SkillMap JSON:
{skill_json}
"""

        try:
            resp = self._llm.generate(
                [LlmMessage(role="system", content=system), LlmMessage(role="user", content=user)],
                temperature=0.1,
                max_tokens=1200,
            )
            code = (resp.content or "").strip()
            if not code:
                return None
            # Defensive stripping if the model included fenced blocks anyway.
            if code.startswith("```"):
                code = code.strip("`")
                code = code.replace("csharp", "", 1).strip()
            if "class SkillEntrypoint" not in code or "Run(string inputsJson" not in code:
                return None
            return code
        except Exception:
            return None

    def _build_csharp_file(self, skill: SkillMap) -> CodeFile:
        content = self._generate_csharp_via_llm(skill) or self._csharp_placeholder()
        return CodeFile(path=f"Skill_{skill.metadata.skill_id}.cs", content=content, language="csharp")

    def _choose_language(self, skill: SkillMap, *, preferred: Optional[str]) -> str:
        if preferred:
            return preferred.strip().lower()
        # Heuristic fallback when LLM isn't present:
        app = (skill.metadata.app_id or "").lower()
        if any(x in app for x in ("excel", "outlook", "word", "powerpoint")):
            return "csharp"
        return "python"

    def _infer_capabilities(self, skill: SkillMap, language: str) -> List[Dict[str, Any]]:
        caps: List[Dict[str, Any]] = []
        for step in skill.steps:
            if step.api_endpoint and step.api_endpoint.url:
                url = step.api_endpoint.url
                host = ""
                try:
                    from urllib.parse import urlparse

                    host = (urlparse(url).hostname or "").lower()
                except Exception:
                    host = ""
                caps.append(
                    {"type": "network", "parameters": {"host": host or "unknown"}, "reason": "HTTP API call"}
                )
        if any(s.selector is not None for s in skill.steps):
            caps.append(
                {"type": "ui_action", "parameters": {"platform": "WINDOWS"}, "reason": "UI automation"}
            )
        if language == "csharp":
            app = (skill.metadata.app_id or "").lower()
            if "excel" in app:
                caps.append(
                    {"type": "com", "parameters": {"prog_id": "Excel.Application"}, "reason": "Excel COM automation"}
                )
        return caps

    def generate(
        self,
        skill: SkillMap,
        *,
        language: Optional[str] = None,
        notes: str = "",
    ) -> Tuple[str, CodeArtifact]:
        artifact_id = str(uuid.uuid4())
        chosen = self._choose_language(skill, preferred=language)
        files: List[CodeFile]
        dependencies: List[str] = []
        if chosen == "csharp":
            files = [self._build_csharp_file(skill)]
            entrypoint = "SkillEntrypoint.Run"
        else:
            files = [self._build_python_file(skill)]
            entrypoint = f"skill_{skill.metadata.skill_id}:run"

        # NOTE: C# code generation may use LLM directly in _build_csharp_file. We intentionally avoid a
        # separate "review pass" here to keep generation deterministic and testable in fallback mode.

        artifact = CodeArtifact(
            skill_id=skill.metadata.skill_id,
            files=files,
            dependencies=dependencies,
            capabilities=self._infer_capabilities(skill, chosen),
            notes=notes or "",
        )
        self._fs.save_code_artifact(artifact_id, artifact.to_firestore())

        # Link back to the skill metadata (stored alongside the Skill Map).
        skill.metadata.code_artifact_id = artifact_id
        skill.metadata.code_language = chosen
        skill.metadata.code_entrypoint = entrypoint
        skill.metadata.code_dependencies = dependencies

        return artifact_id, artifact


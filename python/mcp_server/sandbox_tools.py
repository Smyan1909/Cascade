"""Sandbox execution tools (E2B).

These tools enable programmatic (non-UI) automation by running Python in an isolated
cloud sandbox, copying files in/out to avoid executing arbitrary code on the host.
"""

from __future__ import annotations

import base64
import json
from pathlib import Path
from typing import Any, Dict, List, Optional
import os


def register_sandbox_tools(
    registry: Any,
    *,
    context: Optional[Any] = None,
    approval_manager: Optional[Any] = None,
) -> None:
    """Register sandbox tools with the MCP registry."""

    registry.register_tool(
        name="execute_sandbox_skill",
        description=(
            "Execute a Python-sandbox skill in E2B (copy-in/run/copy-out).\n\n"
            "Use when a Skill Map has metadata.sandbox describing required pip packages and key functions.\n"
            "You must provide the files to copy into the sandbox.\n"
            "If python_code is omitted, this tool will generate it using the configured Cascade LLM (env-driven)."
        ),
        input_schema={
            "type": "object",
            "properties": {
                "skill_id": {"type": "string", "description": "Skill ID to execute"},
                "task": {"type": "string", "description": "High-level task description to guide code generation"},
                "inputs": {
                    "type": "object",
                    "description": "Runtime inputs for the operation (JSON-serializable).",
                    "additionalProperties": True,
                },
                "files": {
                    "type": "array",
                    "description": "Files to copy in/out of the sandbox.",
                    "items": {
                        "type": "object",
                        "properties": {
                            "name": {"type": "string", "description": "Logical name for the file (e.g., workbook)"},
                            "local_path": {"type": "string", "description": "Host path to read/write"},
                            "sandbox_path": {"type": "string", "description": "Path inside sandbox (default: /home/user/<filename>)"},
                            "mode": {
                                "type": "string",
                                "enum": ["input", "output", "inout"],
                                "description": "Whether to copy file into sandbox, out of sandbox, or both.",
                            },
                        },
                        "required": ["name", "local_path", "mode"],
                    },
                },
                "local_path": {
                    "type": "string",
                    "description": "Convenience: a single primary host file path when files[] is omitted (treated as inout).",
                },
                "python_code": {
                    "type": "string",
                    "description": "Optional Python code executed in sandbox. Must define main(payload) and print JSON.",
                },
            },
            "required": ["skill_id", "task"],
        },
        handler=lambda skill_id, task="", files=None, inputs=None, python_code="", local_path="": _execute_sandbox_skill(
            skill_id=skill_id,
            task=task or "",
            files=files or [],
            inputs=inputs or {},
            python_code=python_code or "",
            local_path_hint=local_path or "",
            context=context,
            approval_manager=approval_manager,
        ),
    )


def _ensure_approved(approval_manager: Optional[Any], capability_type: str, parameters: Dict[str, str], reason: str) -> bool:
    if approval_manager is None:
        return True
    try:
        from agents.core.approvals import CapabilityRequest

        return bool(
            approval_manager.ensure_approved(
                CapabilityRequest(
                    capability_type=capability_type,
                    parameters={k: str(v) for k, v in (parameters or {}).items()},
                    reason=reason,
                )
            )
        )
    except Exception:
        return False


def _execute_sandbox_skill(
    *,
    skill_id: str,
    task: str,
    files: List[Dict[str, Any]],
    inputs: Dict[str, Any],
    python_code: str,
    local_path_hint: str,
    context: Optional[Any],
    approval_manager: Optional[Any],
) -> Dict[str, Any]:
    sandbox: Any = None
    try:
        # Load skill + sandbox spec from Firestore.
        from cascade_client.auth.context import CascadeContext
        from storage.firestore_client import FirestoreClient
        from agents.explorer.skill_map import SkillMap

        ctx = context or CascadeContext.from_env()
        fs = FirestoreClient(ctx)
        raw_skill = fs.get_skill_map(skill_id)
        if not raw_skill:
            return {"content": [{"type": "text", "text": f"Skill '{skill_id}' not found."}], "isError": True}
        skill = SkillMap.model_validate(raw_skill)
        sb = getattr(skill.metadata, "sandbox", None)
        if sb is None:
            return {
                "content": [{"type": "text", "text": f"Skill '{skill_id}' has no metadata.sandbox."}],
                "isError": True,
            }

        pkgs = list(getattr(sb, "python_packages", []) or [])

        # Normalize/validate file descriptors. LLMs often provide malformed `files` objects.
        files = _normalize_files(files=files, task=task, local_path_hint=local_path_hint)
        if not files:
            return {
                "content": [
                    {
                        "type": "text",
                        "text": (
                            "No usable files provided. Provide files=[{name, local_path, mode}] or local_path, "
                            "or include an absolute file path in the task."
                        ),
                    }
                ],
                "isError": True,
            }

        # Approvals: sandbox exec + local file I/O.
        if not _ensure_approved(
            approval_manager,
            "sandbox_exec",
            {"provider": getattr(sb, "provider", "e2b"), "packages": ",".join(pkgs)},
            reason="Execute programmatic file automation in sandbox",
        ):
            return {"content": [{"type": "text", "text": "Denied by approval policy."}], "isError": True}

        for f in files:
            mode = (f.get("mode") or "").lower()
            lp = str(f.get("local_path") or "")
            if mode in ("input", "inout") and lp:
                if not _ensure_approved(
                    approval_manager,
                    "file_read",
                    {"path": lp},
                    reason="Copy file into sandbox",
                ):
                    return {"content": [{"type": "text", "text": "Denied by approval policy."}], "isError": True}
            if mode in ("output", "inout") and lp:
                if not _ensure_approved(
                    approval_manager,
                    "file_write",
                    {"path": lp},
                    reason="Copy sandbox output file back to host",
                ):
                    return {"content": [{"type": "text", "text": "Denied by approval policy."}], "isError": True}

        generated = False
        # If the agent provided python_code that references host paths, ignore it and regenerate safely.
        if python_code.strip() and _looks_like_host_path_code(python_code):
            python_code = ""
        if not python_code.strip():
            python_code = _generate_python_code_for_skill(
                task=task,
                skill=skill,
                file_descriptors=files,
            )
            generated = True

        # Create sandbox (E2B) + install deps.
        sandbox = _create_e2b_sandbox()
        _install_packages(sandbox, pkgs)

        # Copy files in.
        file_bindings: Dict[str, str] = {}
        for f in files:
            name = str(f.get("name") or "")
            local_path = Path(str(f.get("local_path") or "")).expanduser()
            mode = (f.get("mode") or "").lower()
            sandbox_path = str(f.get("sandbox_path") or f"/home/user/{local_path.name}")
            file_bindings[name] = sandbox_path

            if mode in ("input", "inout"):
                if not local_path.exists():
                    return {
                        "content": [{"type": "text", "text": f"Local file not found: {str(local_path)}"}],
                        "isError": True,
                    }
                data = local_path.read_bytes()
                _sandbox_write_file_binary(sandbox, sandbox_path, data)

        # Write runner.
        runner_path = "/home/user/cascade_runner.py"
        payload = {"skill_id": skill_id, "task": task, "inputs": inputs, "files": file_bindings}
        runner = _build_runner(python_code=python_code, payload=payload)
        _sandbox_write_file(sandbox, runner_path, runner.encode("utf-8"))

        # Execute runner.
        exec_result = _sandbox_run_command(sandbox, f"python {runner_path}", check=False)

        # Copy outputs back.
        copied_back: List[str] = []
        for f in files:
            mode = (f.get("mode") or "").lower()
            if mode not in ("output", "inout"):
                continue
            name = str(f.get("name") or "")
            local_path = Path(str(f.get("local_path") or "")).expanduser()
            sandbox_path = str(f.get("sandbox_path") or f"/home/user/{local_path.name}")
            out = _sandbox_read_file_binary(sandbox, sandbox_path)
            local_path.write_bytes(out)
            copied_back.append(str(local_path))

        result_obj: Any = None
        try:
            # If the runner printed JSON, it'll be in stdout.
            result_obj = json.loads(str(exec_result.get("stdout", "")).strip())
        except Exception:
            result_obj = None

        # Surface sandbox execution failures as tool errors.
        if int(exec_result.get("exit_code", 0) or 0) != 0:
            return {
                "content": [
                    {
                        "type": "text",
                        "text": json.dumps(
                            {
                                "success": False,
                                "sandbox_provider": getattr(sb, "provider", "e2b"),
                                "packages": pkgs,
                                "generated_code_used": generated,
                                "copied_back": copied_back,
                                "exec": exec_result,
                                "exec_output_json": result_obj,
                            }
                        ),
                    }
                ],
                "isError": True,
            }

        return {
            "content": [
                {
                    "type": "text",
                    "text": json.dumps(
                        {
                            "success": True,
                            "sandbox_provider": getattr(sb, "provider", "e2b"),
                            "packages": pkgs,
                            "copied_back": copied_back,
                            "generated_code_used": generated,
                            "exec": exec_result,
                            "exec_output_json": result_obj,
                        }
                    ),
                }
            ],
            "isError": False,
        }
    except Exception as e:
        return {"content": [{"type": "text", "text": f"Error executing sandbox skill: {str(e)}"}], "isError": True}
    finally:
        try:
            if sandbox is not None:
                _sandbox_close(sandbox)
        except Exception:
            pass


def _generate_python_code_for_skill(*, task: str, skill: Any, file_descriptors: List[Dict[str, Any]]) -> str:
    """Generate Python code for a sandbox skill using the env-configured LLM."""
    from clients.llm_client import LlmMessage, load_llm_client_from_env

    sb = getattr(getattr(skill, "metadata", None), "sandbox", None)
    if sb is None:
        raise ValueError("metadata.sandbox is required to generate python_code")

    # Provide the LLM a compact, JSON-serializable spec.
    spec = {
        "task": task,
        "skill_id": getattr(getattr(skill, "metadata", None), "skill_id", ""),
        "capability": getattr(getattr(skill, "metadata", None), "capability", ""),
        "description": getattr(getattr(skill, "metadata", None), "description", ""),
        "sandbox": {
            "provider": getattr(sb, "provider", "e2b"),
            "python_packages": list(getattr(sb, "python_packages", []) or []),
            "functions": {
                k: {"module": v.module, "function": v.function, "description": v.description}
                for k, v in (getattr(sb, "functions", {}) or {}).items()
            },
            "file_io": getattr(sb, "file_io", None).model_dump(mode="json") if getattr(sb, "file_io", None) else {},
            "entrypoint": getattr(sb, "entrypoint", "") or "",
        },
        "files": [
            {
                "name": f.get("name"),
                "sandbox_path": f.get("sandbox_path"),
                "mode": f.get("mode"),
            }
            for f in file_descriptors
        ],
    }

    system = (
        "You generate Python code to run inside a sandbox. Output ONLY Python code (no markdown).\n"
        "Hard requirements:\n"
        "- Must define: def main(payload: dict) -> dict\n"
        "- Must use payload['files'] mapping of logical name -> sandbox path.\n"
        "- Must read/modify/write ONLY the provided sandbox file paths.\n"
        "- No network calls.\n"
        "- Should be robust: validate expected keys, raise helpful errors.\n"
        "- Return a small JSON-serializable dict (e.g., {'success': True, ...}).\n"
        "Notes:\n"
        "- You may import only the packages listed in sandbox.python_packages (plus stdlib).\n"
        "- Prefer using the functions specified in sandbox.functions when applicable.\n"
    )

    user = (
        "Generate the sandbox python_code for this skill.\n\n"
        "Spec JSON:\n"
        f"{json.dumps(spec, indent=2)}\n"
    )

    llm = load_llm_client_from_env()
    last_code = ""
    for attempt in range(2):
        extra = ""
        if attempt == 1:
            extra = (
                "\n\nCRITICAL FIX REQUIRED:\n"
                "- Do NOT use any Windows/local paths like C:\\\\... or :\\\\ in the code.\n"
                "- ONLY read/write files using payload['files'][<name>] (sandbox paths).\n"
            )

        resp = llm.generate(
            [LlmMessage(role="system", content=system), LlmMessage(role="user", content=user + extra)],
            temperature=0.1,
            max_tokens=1600,
        )
        code = (resp.content or "").strip()

        # Strip accidental fenced blocks.
        if code.startswith("```"):
            code = code.strip("`")
            code = code.replace("python", "", 1).strip()

        last_code = code
        if "def main" not in code:
            continue

        lowered = code.lower()
        if ":\\\\" in lowered or "c:\\\\" in lowered or "d:\\\\" in lowered:
            continue

        return code + ("\n" if not code.endswith("\n") else "")

    raise ValueError("LLM generated invalid python_code (missing main() or used host paths)")

def _build_runner(*, python_code: str, payload: Dict[str, Any]) -> str:
    # The user-provided python_code should define main(payload: dict) -> dict (recommended),
    # but we don't enforce a strict signature; we just call main(payload).
    payload_json = json.dumps(payload)
    return (
        "import json\n"
        "import traceback\n"
        "\n"
        f"PAYLOAD = json.loads('''{payload_json}''')\n"
        "\n"
        f"{python_code}\n"
        "\n"
        "def _run():\n"
        "    if 'main' not in globals():\n"
        "        raise RuntimeError('python_code must define main(payload)')\n"
        "    out = main(PAYLOAD)\n"
        "    print(json.dumps(out if out is not None else {'success': True}))\n"
        "\n"
        "try:\n"
        "    _run()\n"
        "except Exception as e:\n"
        "    print(json.dumps({'success': False, 'error': str(e), 'traceback': traceback.format_exc()}))\n"
    )


def _create_e2b_sandbox():
    # E2B v2.x: do NOT call Sandbox() directly. Use Sandbox.create() (often async).
    import asyncio
    import inspect

    # Prefer `e2b` package.
    try:
        from e2b import Sandbox  # type: ignore

        created = Sandbox.create()
        if inspect.isawaitable(created):
            return asyncio.run(created)
        return created
    except Exception:
        # Fallback: `e2b_code_interpreter` package (if installed).
        try:
            from e2b_code_interpreter import Sandbox as CiSandbox  # type: ignore

            created = CiSandbox.create()
            if inspect.isawaitable(created):
                return asyncio.run(created)
            return created
        except Exception as exc:
            raise ImportError(
                "E2B SDK not usable. Ensure `e2b` is installed and E2B_API_KEY is set. "
                "Sandbox must be created via Sandbox.create()."
            ) from exc


def _install_packages(sandbox: Any, packages: List[str]) -> None:
    if not packages:
        return
    cmd = "pip install " + " ".join(packages)
    _sandbox_run_command(sandbox, cmd, check=True)


def _sandbox_write_file(sandbox: Any, path: str, data: bytes) -> None:
    # Try common file APIs.
    if hasattr(sandbox, "files") and hasattr(sandbox.files, "write"):
        try:
            sandbox.files.write(path, data)  # type: ignore[attr-defined]
            return
        except Exception:
            pass
    if hasattr(sandbox, "filesystem") and hasattr(sandbox.filesystem, "write"):
        sandbox.filesystem.write(path, data)  # type: ignore[attr-defined]
        return
    # Fallback: write via shell heredoc (binary-safe not guaranteed).
    raise RuntimeError("Sandbox SDK does not support file write in this environment.")


def _sandbox_read_file(sandbox: Any, path: str) -> bytes:
    if hasattr(sandbox, "files") and hasattr(sandbox.files, "read"):
        out = sandbox.files.read(path)  # type: ignore[attr-defined]
        return out if isinstance(out, (bytes, bytearray)) else str(out).encode("utf-8")
    if hasattr(sandbox, "filesystem") and hasattr(sandbox.filesystem, "read"):
        out = sandbox.filesystem.read(path)  # type: ignore[attr-defined]
        return out if isinstance(out, (bytes, bytearray)) else str(out).encode("utf-8")
    raise RuntimeError("Sandbox SDK does not support file read in this environment.")


def _sandbox_write_file_binary(sandbox: Any, path: str, data: bytes) -> None:
    """Binary-safe write into sandbox.

    Avoids any SDK behavior that might coerce bytes into text by sending base64 and decoding in-sandbox.
    """
    b64 = base64.b64encode(data).decode("ascii")
    # Use python inside the sandbox to decode base64 to bytes.
    # Note: command length limits may apply for very large files; this is sufficient for typical test workbooks.
    py = (
        "python -c \"import base64; "
        f"open('{path}','wb').write(base64.b64decode('{b64}'))\""
    )
    _sandbox_run_command(sandbox, py, check=True)


def _sandbox_read_file_binary(sandbox: Any, path: str) -> bytes:
    """Binary-safe read from sandbox via base64."""
    py = (
        "python -c \"import base64; "
        f"print(base64.b64encode(open('{path}','rb').read()).decode('ascii'))\""
    )
    res = _sandbox_run_command(sandbox, py, check=True)
    out = (res.get("stdout") or "").strip()
    return base64.b64decode(out.encode("ascii"))


def _sandbox_run_command(sandbox: Any, command: str, *, check: bool) -> Dict[str, Any]:
    """Run a shell command inside the sandbox and return stdout/stderr/exit_code."""

    res: Any = None
    if hasattr(sandbox, "commands") and hasattr(sandbox.commands, "run"):
        res = sandbox.commands.run(command)  # type: ignore[attr-defined]
    elif hasattr(sandbox, "run"):
        res = sandbox.run(command)  # type: ignore[attr-defined]
    elif hasattr(sandbox, "exec"):
        res = sandbox.exec(command)  # type: ignore[attr-defined]
    elif hasattr(sandbox, "notebook") and hasattr(sandbox.notebook, "exec"):
        res = sandbox.notebook.exec(command)  # type: ignore[attr-defined]
    else:
        raise RuntimeError("Sandbox SDK does not support running commands in this environment.")

    stdout = getattr(res, "stdout", None) or getattr(res, "text", None) or ""
    stderr = getattr(res, "stderr", None) or ""
    exit_code = getattr(res, "exit_code", None)
    if exit_code is None:
        exit_code = getattr(res, "code", None)
    if exit_code is None:
        # Assume success if SDK doesn't expose a code.
        exit_code = 0

    payload = {"command": command, "exit_code": int(exit_code), "stdout": str(stdout), "stderr": str(stderr)}
    if check and int(exit_code) != 0:
        raise RuntimeError(f"Sandbox command failed: {payload}")
    return payload


def _sandbox_close(sandbox: Any) -> None:
    # Best-effort close to avoid leaking sandboxes.
    if not hasattr(sandbox, "close"):
        return
    res = sandbox.close()  # type: ignore[attr-defined]
    try:
        import inspect
        import asyncio

        if inspect.isawaitable(res):
            asyncio.run(res)
    except Exception:
        pass


def _infer_local_path_from_task(task: str) -> str:
    import re

    t = task or ""
    # Backslashes
    m = re.search(r'([A-Za-z]:\\\\[^\\n\\r"]+\\.(xlsx|xlsm|csv|pptx|docx))', t)
    if m:
        return m.group(1)
    # Forward slashes
    m = re.search(r'([A-Za-z]:/[^\\n\\r"]+\\.(xlsx|xlsm|csv|pptx|docx))', t)
    if m:
        return m.group(1)
    return ""


def _looks_like_host_path_code(code: str) -> bool:
    lowered = (code or "").lower()
    return (":\\\\" in lowered) or ("c:\\\\" in lowered) or ("d:\\\\" in lowered)


def _normalize_files(*, files: List[Dict[str, Any]], task: str, local_path_hint: str) -> List[Dict[str, Any]]:
    """Make tool robust to malformed `files` argument coming from LLMs."""
    normalized: List[Dict[str, Any]] = []
    for f in (files or []):
        if not isinstance(f, dict):
            continue
        mode = (f.get("mode") or "").lower().strip()
        if mode not in ("input", "output", "inout"):
            mode = "inout"
        lp = str(f.get("local_path") or "").strip()
        if not lp:
            continue
        name = str(f.get("name") or "workbook").strip() or "workbook"
        sp = str(f.get("sandbox_path") or "").strip() or None
        normalized.append({"name": name, "local_path": lp, "mode": mode, **({"sandbox_path": sp} if sp else {})})

    if normalized:
        return normalized

    inferred = (local_path_hint or "").strip() or _infer_local_path_from_task(task)
    if inferred:
        return [{"name": "workbook", "local_path": inferred, "mode": "inout"}]
    return []



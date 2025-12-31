"""Brain-side executor for generated Python code artifacts.

Runs code in a subprocess with:
- sandbox working directory
- file access audit hook (block outside sandbox)
- network guard (allowlist hosts)
"""

from __future__ import annotations

import json
import os
import subprocess
import sys
import uuid
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Dict, List, Optional, Sequence

from storage.code_artifact import CodeFile


@dataclass
class PythonExecutionPolicy:
    sandbox_root: Path
    allowed_net_hosts: List[str]
    allow_process_spawn: bool = False
    timeout_seconds: float = 20.0


class PythonArtifactExecutor:
    def __init__(self, policy: PythonExecutionPolicy):
        self._policy = policy

    def execute(
        self,
        *,
        files: Sequence[CodeFile],
        entrypoint: str,
        inputs: Optional[Dict[str, Any]] = None,
        extra_env: Optional[Dict[str, str]] = None,
    ) -> Dict[str, Any]:
        """
        Execute an artifact in a sandboxed subprocess.

        Args:
            files: Code files (paths are relative within sandbox).
            entrypoint: 'module:function' (module resolved from sandbox root).
            inputs: dict passed to entrypoint.
            extra_env: optional env vars to pass through.
        """

        run_id = uuid.uuid4().hex[:12]
        sandbox_dir = (self._policy.sandbox_root / f"run_{run_id}").resolve()
        sandbox_dir.mkdir(parents=True, exist_ok=True)

        # Write code files into sandbox directory.
        for f in files:
            rel = Path(f.path)
            if rel.is_absolute() or ".." in rel.parts:
                raise ValueError(f"Invalid code file path: {f.path}")
            target = (sandbox_dir / rel).resolve()
            if not str(target).startswith(str(sandbox_dir)):
                raise ValueError(f"Invalid code file path escapes sandbox: {f.path}")
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text(f.content, encoding="utf-8")

        env = os.environ.copy()
        env.update(extra_env or {})
        python_root = Path(__file__).resolve().parents[2]  # .../python
        # Ensure runner module is importable (agents.*) + artifact modules (sandbox_dir).
        existing = env.get("PYTHONPATH") or ""
        parts = [str(sandbox_dir), str(python_root)]
        if existing:
            parts.append(existing)
        env["PYTHONPATH"] = os.pathsep.join(parts)
        env["CASCADE_SANDBOX_DIR"] = str(sandbox_dir)
        env["CASCADE_CODE_ENTRYPOINT"] = entrypoint
        env["CASCADE_CODE_INPUTS_JSON"] = json.dumps(inputs or {})
        env["CASCADE_ALLOWED_NET_HOSTS"] = ",".join(self._policy.allowed_net_hosts or [])
        env["CASCADE_ALLOW_PROCESS_SPAWN"] = "1" if self._policy.allow_process_spawn else "0"

        cmd = [
            sys.executable,
            "-m",
            "agents.code_exec.python_runner",
        ]

        proc = subprocess.run(
            cmd,
            cwd=str(sandbox_dir),
            env=env,
            capture_output=True,
            text=True,
            timeout=self._policy.timeout_seconds,
        )

        stdout = (proc.stdout or "").strip()
        stderr = (proc.stderr or "").strip()

        # The runner prints a JSON object (even on failure).
        try:
            payload = json.loads(stdout) if stdout else None
        except Exception:
            payload = None

        if isinstance(payload, dict) and "success" in payload:
            # Attach stderr for debugging if present.
            if stderr:
                payload.setdefault("stderr", stderr)
            payload.setdefault("sandbox_dir", str(sandbox_dir))
            return payload

        # Fallback: return raw output.
        return {
            "success": False,
            "output": "",
            "error": "Python runner did not return valid JSON.",
            "stdout": stdout,
            "stderr": stderr,
            "sandbox_dir": str(sandbox_dir),
        }



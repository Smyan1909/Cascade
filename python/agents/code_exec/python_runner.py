"""Subprocess entrypoint for executing generated Python code artifacts.

This module is executed in a separate Python process to allow sandboxing,
timeouts, and controlled environment variables.
"""

from __future__ import annotations

import importlib
import json
import os
import socket
import sys
import traceback
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Callable, Dict, Optional, Set, Tuple


@dataclass(frozen=True)
class _Policy:
    sandbox_dir: Path
    allowed_net_hosts: Set[str]
    allow_process_spawn: bool


def _parse_policy_from_env() -> _Policy:
    sandbox_dir = Path(os.environ.get("CASCADE_SANDBOX_DIR", ".")).resolve()
    allowed = os.environ.get("CASCADE_ALLOWED_NET_HOSTS", "").strip()
    allowed_hosts = {h.strip().lower() for h in allowed.split(",") if h.strip()}
    allow_spawn = os.environ.get("CASCADE_ALLOW_PROCESS_SPAWN", "0").strip() == "1"
    return _Policy(
        sandbox_dir=sandbox_dir,
        allowed_net_hosts=allowed_hosts,
        allow_process_spawn=allow_spawn,
    )


def _is_path_within(base: Path, target: Path) -> bool:
    try:
        target.resolve().relative_to(base.resolve())
        return True
    except Exception:
        return False


def _install_audit_hook(policy: _Policy) -> None:
    """Block file I/O outside sandbox and optionally block process creation."""

    sandbox = policy.sandbox_dir

    def hook(event: str, args: Tuple[Any, ...]) -> None:
        # File access events (best-effort).
        if event in ("open", "os.open", "os.listdir", "os.scandir", "pathlib.Path.open"):
            if not args:
                return
            path = args[0]
            try:
                p = Path(path).expanduser()
            except Exception:
                return
            # Allow relative paths (implicitly sandbox CWD) and sandbox-contained paths.
            if not p.is_absolute():
                return
            if not _is_path_within(sandbox, p):
                raise PermissionError(
                    f"Blocked file access outside sandbox: {p} (sandbox={sandbox})"
                )

        if event in ("subprocess.Popen", "os.system", "os.spawn", "os.execv", "os.execve"):
            if not policy.allow_process_spawn:
                raise PermissionError("Blocked process spawn by policy")

    sys.addaudithook(hook)


def _install_network_guard(policy: _Policy) -> None:
    """Restrict outbound socket connects to allowed hosts (best-effort)."""

    allowed = policy.allowed_net_hosts
    if not allowed:
        # If empty, disallow all outbound network connects.
        allowed = set()

    orig_connect: Callable[..., Any] = socket.socket.connect

    def guarded_connect(self: socket.socket, address):  # type: ignore[no-untyped-def]
        host = None
        try:
            host = address[0]
        except Exception:
            host = None

        if host is None:
            raise PermissionError("Blocked network connect: unknown host")

        host_l = str(host).lower()
        if host_l not in allowed:
            raise PermissionError(f"Blocked network connect to host: {host_l}")

        return orig_connect(self, address)

    socket.socket.connect = guarded_connect  # type: ignore[assignment]


def _load_entrypoint() -> Callable[[Dict[str, Any]], Any]:
    """Load entrypoint from env CASCADE_CODE_ENTRYPOINT = 'module:function'."""

    spec = os.environ.get("CASCADE_CODE_ENTRYPOINT", "").strip()
    if ":" not in spec:
        raise ValueError(
            "Missing/invalid CASCADE_CODE_ENTRYPOINT. Expected 'module:function'."
        )
    mod_name, fn_name = spec.split(":", 1)
    mod = importlib.import_module(mod_name)
    fn = getattr(mod, fn_name, None)
    if fn is None or not callable(fn):
        raise ValueError(f"Entrypoint not found/callable: {spec}")
    return fn


def main() -> None:
    started = __import__("time").time()
    try:
        policy = _parse_policy_from_env()

        # Ensure sandbox exists and is current working directory.
        policy.sandbox_dir.mkdir(parents=True, exist_ok=True)
        os.chdir(policy.sandbox_dir)

        _install_audit_hook(policy)
        _install_network_guard(policy)

        inputs_json = os.environ.get("CASCADE_CODE_INPUTS_JSON", "{}")
        inputs: Dict[str, Any] = json.loads(inputs_json) if inputs_json else {}

        entry = _load_entrypoint()
        result = entry(inputs)

        elapsed_ms = int((__import__("time").time() - started) * 1000)
        print(
            json.dumps(
                {
                    "success": True,
                    "output": result if isinstance(result, (str, int, float, bool, dict, list)) else str(result),
                    "error": "",
                    "execution_time_ms": elapsed_ms,
                }
            )
        )
    except Exception:
        elapsed_ms = int((__import__("time").time() - started) * 1000)
        err = traceback.format_exc()
        print(
            json.dumps(
                {
                    "success": False,
                    "output": "",
                    "error": err,
                    "execution_time_ms": elapsed_ms,
                }
            )
        )
        sys.exit(1)


if __name__ == "__main__":
    main()



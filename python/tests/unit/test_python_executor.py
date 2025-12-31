import os
import tempfile
from pathlib import Path

from agents.code_exec.python_executor import PythonArtifactExecutor, PythonExecutionPolicy
from storage.code_artifact import CodeFile


def test_python_executor_runs_simple_entrypoint():
    with tempfile.TemporaryDirectory() as tmp:
        policy = PythonExecutionPolicy(
            sandbox_root=Path(tmp) / "sandbox",
            allowed_net_hosts=[],
            allow_process_spawn=False,
            timeout_seconds=5.0,
        )
        ex = PythonArtifactExecutor(policy)
        files = [
            CodeFile(
                path="m.py",
                language="python",
                content="""
def run(inputs: dict):
    return {"echo": inputs.get("x")}
""",
            )
        ]
        out = ex.execute(files=files, entrypoint="m:run", inputs={"x": "hi"})
        assert out["success"] is True
        assert out["output"]["echo"] == "hi"


def test_python_executor_blocks_file_outside_sandbox():
    with tempfile.TemporaryDirectory() as tmp:
        policy = PythonExecutionPolicy(
            sandbox_root=Path(tmp) / "sandbox",
            allowed_net_hosts=[],
            allow_process_spawn=False,
            timeout_seconds=5.0,
        )
        ex = PythonArtifactExecutor(policy)
        files = [
            CodeFile(
                path="m.py",
                language="python",
                content=r"""
def run(inputs: dict):
    # Attempt to read outside sandbox (absolute path).
    p = r"C:\Windows\win.ini"
    with open(p, "r", encoding="utf-8", errors="ignore") as f:
        return f.read(10)
""",
            )
        ]
        out = ex.execute(files=files, entrypoint="m:run", inputs={})
        assert out["success"] is False
        assert "Blocked file access outside sandbox" in out["error"]



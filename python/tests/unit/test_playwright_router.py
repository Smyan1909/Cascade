import os
import tempfile

from mcp_server.automation_router import AutomationRouter
from cascade_client.auth.context import CascadeContext
from cascade_client.grpc_client import CascadeGrpcClient


class _DummyPw:
    def __init__(self):
        self.started_web = None
        self.started_electron = None
        self.closed = False

    def start_web(self, url: str) -> None:
        self.started_web = url

    def start_electron(self, exe_path: str) -> None:
        self.started_electron = exe_path

    def close(self) -> None:
        self.closed = True


def test_router_start_app_url_routes_to_playwright(monkeypatch):
    # Avoid needing a real gRPC endpoint; we won't call it.
    grpc = object()
    router = AutomationRouter(grpc)  # type: ignore[arg-type]
    router._pw = _DummyPw()  # type: ignore[attr-defined]

    called = {"grpc": False}

    def _fake_start(*args, **kwargs):
        called["grpc"] = True
        return {"success": True}

    monkeypatch.setattr("mcp_server.automation_router.grpc_start_app", _fake_start)

    resp = router.start_app("https://example.com")
    assert resp["success"] is True
    assert router._pw.started_web == "https://example.com"  # type: ignore[attr-defined]
    assert called["grpc"] is False


def test_router_start_app_electron_exe_routes_to_playwright(monkeypatch):
    grpc = object()
    router = AutomationRouter(grpc)  # type: ignore[arg-type]
    router._pw = _DummyPw()  # type: ignore[attr-defined]

    called = {"grpc": False}
    monkeypatch.setattr("mcp_server.automation_router.grpc_start_app", lambda *a, **k: {"success": True})

    with tempfile.TemporaryDirectory() as d:
        exe_path = os.path.join(d, "MyElectronApp.exe")
        os.makedirs(os.path.join(d, "resources"), exist_ok=True)
        with open(exe_path, "wb") as f:
            f.write(b"")
        with open(os.path.join(d, "resources", "app.asar"), "wb") as f:
            f.write(b"")

        resp = router.start_app(exe_path)
        assert resp["success"] is True
        assert router._pw.started_electron == exe_path  # type: ignore[attr-defined]


def test_router_start_app_default_routes_to_grpc(monkeypatch):
    grpc = object()
    router = AutomationRouter(grpc)  # type: ignore[arg-type]
    router._pw = _DummyPw()  # type: ignore[attr-defined]

    called = {"grpc": False}

    def _fake_start(*args, **kwargs):
        called["grpc"] = True
        return {"success": True, "message": "grpc"}

    monkeypatch.setattr("mcp_server.automation_router.grpc_start_app", _fake_start)

    resp = router.start_app("notepad.exe")
    assert resp["success"] is True
    assert called["grpc"] is True



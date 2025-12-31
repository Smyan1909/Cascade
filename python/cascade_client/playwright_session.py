"""
Python Playwright session manager for Cascade.

This module provides a thin, stateful wrapper around Playwright (Python) so
the Brain can automate web pages and Electron apps (via CDP).
"""

from __future__ import annotations

import os
import socket
import subprocess
import tempfile
import time
from dataclasses import dataclass
from typing import List, Optional, Sequence, Tuple

from playwright.sync_api import Browser, BrowserContext, Page, Playwright, sync_playwright


def _pick_free_port() -> int:
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        s.bind(("127.0.0.1", 0))
        return int(s.getsockname()[1])


@dataclass
class ElectronLaunchInfo:
    exe_path: str
    cdp_port: int
    user_data_dir: str
    args: List[str]


class PlaywrightSession:
    """
    Stateful Playwright session.

    Notes:
    - This is intentionally synchronous; MCP tool calls are synchronous.
    - For Electron we connect via CDP; the Electron process is launched by Python.
    """

    def __init__(
        self,
        *,
        headless: bool = True,
        browser_channel: Optional[str] = None,
        action_timeout_ms: int = 8000,
        navigation_timeout_ms: int = 30000,
    ):
        self._headless = bool(headless)
        self._browser_channel = browser_channel
        self._action_timeout_ms = int(action_timeout_ms)
        self._navigation_timeout_ms = int(navigation_timeout_ms)

        self._pw: Optional[Playwright] = None
        self._browser: Optional[Browser] = None
        self._context: Optional[BrowserContext] = None
        self._page: Optional[Page] = None

        self._electron_proc: Optional[subprocess.Popen] = None
        self._electron_info: Optional[ElectronLaunchInfo] = None

    # ---------------------------------------------------------------------
    # Lifecycle
    # ---------------------------------------------------------------------
    def ensure_started(self) -> None:
        if self._pw is None:
            self._pw = sync_playwright().start()

    def close(self) -> None:
        # Try to close page/context/browser first (order matters for CDP)
        try:
            if self._context is not None:
                self._context.close()
        except Exception:
            pass
        try:
            if self._browser is not None:
                self._browser.close()
        except Exception:
            pass
        try:
            if self._pw is not None:
                self._pw.stop()
        except Exception:
            pass

        self._pw = None
        self._browser = None
        self._context = None
        self._page = None

        # Stop electron process last
        try:
            if self._electron_proc is not None and self._electron_proc.poll() is None:
                self._electron_proc.terminate()
                try:
                    self._electron_proc.wait(timeout=5)
                except Exception:
                    self._electron_proc.kill()
        except Exception:
            pass
        self._electron_proc = None
        self._electron_info = None

    # ---------------------------------------------------------------------
    # Accessors
    # ---------------------------------------------------------------------
    def get_page(self) -> Page:
        if self._page is None:
            raise RuntimeError("Playwright page is not initialized; call start_web(...) or start_electron(...) first.")
        return self._page

    # ---------------------------------------------------------------------
    # Web
    # ---------------------------------------------------------------------
    def start_web(self, url: str) -> None:
        """
        Start a browser session and navigate to the given URL.
        """
        self.ensure_started()
        assert self._pw is not None

        # Replace any existing session (fresh page for new app)
        self._reset_browser_state()

        launch_kwargs = {"headless": self._headless}
        if self._browser_channel:
            launch_kwargs["channel"] = self._browser_channel

        self._browser = self._pw.chromium.launch(**launch_kwargs)
        self._context = self._browser.new_context()
        self._context.set_default_timeout(self._action_timeout_ms)
        self._context.set_default_navigation_timeout(self._navigation_timeout_ms)
        self._page = self._context.new_page()
        self._page.goto(url, wait_until="networkidle")

    # ---------------------------------------------------------------------
    # Electron (CDP)
    # ---------------------------------------------------------------------
    def start_electron(
        self,
        exe_path: str,
        *,
        args: Optional[Sequence[str]] = None,
        remote_debugging_port: Optional[int] = None,
        user_data_dir: Optional[str] = None,
        startup_timeout_s: float = 15.0,
        extra_env: Optional[dict] = None,
    ) -> None:
        """
        Launch an Electron app with remote debugging enabled and connect over CDP.

        This requires the Electron app to honor Chromium's `--remote-debugging-port`.
        """
        self.ensure_started()
        assert self._pw is not None

        # Replace any existing session/process
        self.close()
        self.ensure_started()
        assert self._pw is not None

        port = int(remote_debugging_port or _pick_free_port())
        udd = user_data_dir or tempfile.mkdtemp(prefix="cascade-electron-")
        argv = list(args or [])

        # Ensure remote debugging flags exist
        argv = list(argv) + [
            f"--remote-debugging-port={port}",
            f"--user-data-dir={udd}",
        ]

        env = os.environ.copy()
        if extra_env:
            env.update({k: str(v) for k, v in extra_env.items()})

        self._electron_proc = subprocess.Popen(
            [exe_path, *argv],
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            env=env,
        )
        self._electron_info = ElectronLaunchInfo(
            exe_path=exe_path, cdp_port=port, user_data_dir=udd, args=list(argv)
        )

        endpoint = f"http://127.0.0.1:{port}"
        self._browser = self._connect_over_cdp_with_retry(self._pw, endpoint, startup_timeout_s)

        # CDP browsers can have existing contexts/pages
        contexts = self._browser.contexts
        if contexts:
            self._context = contexts[0]
        else:
            self._context = self._browser.new_context()

        self._context.set_default_timeout(self._action_timeout_ms)
        self._context.set_default_navigation_timeout(self._navigation_timeout_ms)

        # Pick first existing page if present; otherwise create one
        pages = self._context.pages
        if pages:
            self._page = pages[0]
        else:
            self._page = self._context.new_page()

        # Wait until DOM is at least ready
        try:
            self._page.wait_for_load_state("domcontentloaded", timeout=self._navigation_timeout_ms)
        except Exception:
            pass

    def _connect_over_cdp_with_retry(
        self, pw: Playwright, endpoint: str, timeout_s: float
    ) -> Browser:
        deadline = time.time() + float(timeout_s)
        last_exc: Optional[Exception] = None
        while time.time() < deadline:
            try:
                return pw.chromium.connect_over_cdp(endpoint)
            except Exception as exc:
                last_exc = exc
                time.sleep(0.2)
        raise RuntimeError(f"Failed to connect to Electron over CDP at {endpoint}: {last_exc}")

    def _reset_browser_state(self) -> None:
        # Close only browser pieces; keep playwright running
        try:
            if self._context is not None:
                self._context.close()
        except Exception:
            pass
        try:
            if self._browser is not None:
                self._browser.close()
        except Exception:
            pass
        self._browser = None
        self._context = None
        self._page = None


def looks_like_url(value: str) -> bool:
    v = (value or "").strip().lower()
    return v.startswith("http://") or v.startswith("https://")


def looks_like_electron_exe(exe_path: str) -> bool:
    """
    Heuristic: electron packaged apps usually ship resources/app.asar
    next to the executable.
    """
    p = (exe_path or "").strip().strip('"')
    if not p or not os.path.exists(p) or not p.lower().endswith(".exe"):
        return False
    base_dir = os.path.dirname(p)
    candidate = os.path.join(base_dir, "resources", "app.asar")
    return os.path.exists(candidate)



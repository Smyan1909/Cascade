"""
AutomationRouter for MCP tools.

Routes high-level automation calls to either:
- gRPC-backed Body (UIA) for native desktop apps
- Python Playwright session for web sites and Electron apps
"""

from __future__ import annotations

import os
from dataclasses import dataclass
from typing import Any, Dict, Optional

from cascade_client.grpc_client import CascadeGrpcClient
from cascade_client.models import Action, ActionType, PlatformSource, Selector
from cascade_client.tools import (
    click_element as grpc_click_element,
    focus_element as grpc_focus_element,
    get_semantic_tree as grpc_get_semantic_tree,
    get_screenshot as grpc_get_screenshot,
    hover_element as grpc_hover_element,
    reset_state as grpc_reset_state,
    scroll_element as grpc_scroll_element,
    start_app as grpc_start_app,
    type_text as grpc_type_text,
    wait_visible as grpc_wait_visible,
)

from cascade_client.playwright_session import PlaywrightSession, looks_like_electron_exe, looks_like_url
from cascade_client.playwright_semantics import (
    get_marked_screenshot,
    get_semantic_tree,
    screenshot_to_tool_payload,
    semantic_tree_to_tool_payload,
)


@dataclass
class ActiveBackend:
    kind: str  # "grpc" | "playwright"
    platform: PlatformSource


class AutomationRouter:
    def __init__(
        self,
        grpc_client: CascadeGrpcClient,
        *,
        headless: bool = True,
        browser_channel: Optional[str] = None,
        action_timeout_ms: int = 8000,
    ):
        self._grpc = grpc_client
        self._pw = PlaywrightSession(
            headless=headless,
            browser_channel=browser_channel,
            action_timeout_ms=action_timeout_ms,
        )
        self._active = ActiveBackend(kind="grpc", platform=PlatformSource.WINDOWS)

    @property
    def playwright_session(self) -> PlaywrightSession:
        return self._pw

    @property
    def active_backend(self) -> ActiveBackend:
        return self._active

    # ------------------------------------------------------------------
    # Session / routing
    # ------------------------------------------------------------------
    def start_app(self, app_name: str) -> Dict[str, Any]:
        name = (app_name or "").strip()
        if not name:
            return {"success": False, "message": "app_name is required"}

        # Web URLs -> Playwright
        if looks_like_url(name):
            self._pw.start_web(name)
            self._active = ActiveBackend(kind="playwright", platform=PlatformSource.WEB)
            return {"success": True, "message": f"Opened {name} (Playwright)"}

        # Electron EXE -> Playwright (CDP)
        cleaned = name.strip().strip('"')
        if looks_like_electron_exe(cleaned):
            self._pw.start_electron(cleaned)
            self._active = ActiveBackend(kind="playwright", platform=PlatformSource.WEB)
            return {"success": True, "message": f"Started Electron app {cleaned} (Playwright CDP)"}

        # Default -> gRPC/UIA
        self._active = ActiveBackend(kind="grpc", platform=PlatformSource.WINDOWS)
        return grpc_start_app(self._grpc, name)

    def reset_state(self) -> Dict[str, Any]:
        # Reset state should reset both, best-effort.
        try:
            self._pw.close()
        except Exception:
            pass
        self._active = ActiveBackend(kind="grpc", platform=PlatformSource.WINDOWS)
        return grpc_reset_state(self._grpc)

    # ------------------------------------------------------------------
    # Read operations
    # ------------------------------------------------------------------
    def get_semantic_tree(self) -> Dict[str, Any]:
        if self._active.kind == "playwright":
            page = self._pw.get_page()
            tree = get_semantic_tree(page)
            return semantic_tree_to_tool_payload(tree)
        return grpc_get_semantic_tree(self._grpc)

    def get_screenshot(self) -> Dict[str, Any]:
        if self._active.kind == "playwright":
            page = self._pw.get_page()
            tree = get_semantic_tree(page)
            shot = get_marked_screenshot(page, tree)
            return screenshot_to_tool_payload(shot)
        return grpc_get_screenshot(self._grpc)

    # ------------------------------------------------------------------
    # Unified action tools
    # ------------------------------------------------------------------
    def click_element(self, selector: Selector) -> Dict[str, Any]:
        return self._perform_unified_action(ActionType.CLICK, selector)

    def type_text(self, selector: Selector, text: str) -> Dict[str, Any]:
        return self._perform_unified_action(ActionType.TYPE_TEXT, selector, text=text)

    def hover_element(self, selector: Selector) -> Dict[str, Any]:
        return self._perform_unified_action(ActionType.HOVER, selector)

    def focus_element(self, selector: Selector) -> Dict[str, Any]:
        return self._perform_unified_action(ActionType.FOCUS, selector)

    def scroll_element(self, selector: Selector) -> Dict[str, Any]:
        return self._perform_unified_action(ActionType.SCROLL, selector)

    def wait_visible(self, selector: Selector) -> Dict[str, Any]:
        return self._perform_unified_action(ActionType.WAIT_VISIBLE, selector)

    def _perform_unified_action(
        self,
        action_type: ActionType,
        selector: Selector,
        *,
        text: Optional[str] = None,
    ) -> Dict[str, Any]:
        # Route based on selector platform when present.
        platform = selector.platform_source if selector else PlatformSource.PLATFORM_SOURCE_UNSPECIFIED
        if platform == PlatformSource.WEB or self._active.kind == "playwright":
            return self._pw_action(action_type, selector, text=text)

        # gRPC path
        if action_type == ActionType.CLICK:
            return grpc_click_element(self._grpc, selector)
        if action_type == ActionType.TYPE_TEXT:
            return grpc_type_text(self._grpc, selector, text or "")
        if action_type == ActionType.HOVER:
            return grpc_hover_element(self._grpc, selector)
        if action_type == ActionType.FOCUS:
            return grpc_focus_element(self._grpc, selector)
        if action_type == ActionType.SCROLL:
            return grpc_scroll_element(self._grpc, selector)
        if action_type == ActionType.WAIT_VISIBLE:
            return grpc_wait_visible(self._grpc, selector)
        return {"success": False, "message": f"Unsupported action: {action_type.name}"}

    # ------------------------------------------------------------------
    # Playwright action implementation for unified actions
    # ------------------------------------------------------------------
    def _pw_action(
        self,
        action_type: ActionType,
        selector: Selector,
        *,
        text: Optional[str] = None,
    ) -> Dict[str, Any]:
        page = self._pw.get_page()
        loc = _build_locator(page, selector)
        if loc is None:
            return {"success": False, "message": "Selector not provided"}

        if selector.index is not None and selector.index >= 0:
            loc = loc.nth(selector.index)

        try:
            if action_type == ActionType.CLICK:
                loc.wait_for(state="visible", timeout=8000)
                loc.click()
            elif action_type == ActionType.TYPE_TEXT:
                loc.wait_for(state="visible", timeout=8000)
                loc.fill(text or "")
            elif action_type == ActionType.HOVER:
                loc.wait_for(state="visible", timeout=8000)
                loc.hover()
            elif action_type == ActionType.FOCUS:
                loc.wait_for(state="visible", timeout=8000)
                loc.focus()
            elif action_type == ActionType.SCROLL:
                # Scroll element into view (best-effort)
                loc.evaluate("el => el.scrollIntoView({behavior:'auto',block:'center'})")
            elif action_type == ActionType.WAIT_VISIBLE:
                loc.wait_for(state="visible", timeout=8000)
            else:
                return {"success": False, "message": f"Unsupported action {action_type.name} for Playwright backend"}

            self._active = ActiveBackend(kind="playwright", platform=PlatformSource.WEB)
            return {"success": True, "message": "OK"}
        except Exception as exc:
            return {"success": False, "message": f"Playwright action failed: {exc}"}


def _build_locator(page, selector: Selector):
    if selector is None:
        return None

    if selector.id:
        sid = selector.id.strip()
        # If the id already looks like a Playwright selector (engine-prefixed), use it as-is.
        if sid.startswith(("css=", "xpath=", "text=", "role=")):
            return page.locator(sid)
        # Otherwise treat as DOM id.
        return page.locator(f"#{sid}")

    if selector.name:
        # Text-based target
        return page.get_by_text(selector.name)

    if selector.path:
        chained = " >> ".join(selector.path)
        return page.locator(chained)

    if selector.text_hint:
        return page.get_by_text(selector.text_hint)

    return None



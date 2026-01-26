"""
Extended Playwright tool pack (Python Playwright).

These tools provide richer web/Electron automation than the unified base tools,
while still sharing the same underlying Playwright session used by AutomationRouter.
"""

from __future__ import annotations

from typing import Any, Dict, List, Optional

from playwright.sync_api import Page

from mcp_server.automation_router import AutomationRouter


def register_playwright_tools(registry: Any, router: AutomationRouter) -> None:
    """
    Register pw_* tools on the MCP registry.

    Assumes the underlying Playwright session is started via `start_app` (URL or Electron exe),
    but tools also work if the caller has already started a Playwright session another way.
    """

    def _page() -> Page:
        return router.playwright_session.get_page()

    # Navigation ---------------------------------------------------------
    registry.register_tool(
        name="pw_goto",
        description="Playwright: navigate to a URL (more direct than start_app for web flows).",
        input_schema={
            "type": "object",
            "properties": {
                "url": {"type": "string"},
                "wait_until": {"type": "string", "enum": ["load", "domcontentloaded", "networkidle"]},
            },
            "required": ["url"],
        },
        handler=lambda url, wait_until="networkidle": _wrap(
            lambda: _page().goto(url, wait_until=wait_until)
        ),
    )

    registry.register_tool(
        name="pw_back",
        description="Playwright: go back in history.",
        input_schema={"type": "object", "properties": {}},
        handler=lambda: _wrap(lambda: _page().go_back()),
    )

    registry.register_tool(
        name="pw_forward",
        description="Playwright: go forward in history.",
        input_schema={"type": "object", "properties": {}},
        handler=lambda: _wrap(lambda: _page().go_forward()),
    )

    registry.register_tool(
        name="pw_reload",
        description="Playwright: reload the current page.",
        input_schema={
            "type": "object",
            "properties": {"wait_until": {"type": "string", "enum": ["load", "domcontentloaded", "networkidle"]}},
        },
        handler=lambda wait_until="networkidle": _wrap(lambda: _page().reload(wait_until=wait_until)),
    )

    registry.register_tool(
        name="pw_wait_for_url",
        description="Playwright: wait for the current URL to match a pattern (glob/regex accepted by Playwright).",
        input_schema={
            "type": "object",
            "properties": {"url": {"type": "string"}, "timeout_ms": {"type": "integer"}},
            "required": ["url"],
        },
        handler=lambda url, timeout_ms=10000: _wrap(lambda: _page().wait_for_url(url, timeout=timeout_ms)),
    )

    # Selector / locator ops --------------------------------------------
    registry.register_tool(
        name="pw_locator_count",
        description="Playwright: count elements matching selector.",
        input_schema={"type": "object", "properties": {"selector": {"type": "string"}}, "required": ["selector"]},
        handler=lambda selector: _wrap(lambda: {"count": _page().locator(selector).count()}),
    )

    registry.register_tool(
        name="pw_locator_text",
        description="Playwright: get innerText/textContent for the first matching element.",
        input_schema={
            "type": "object",
            "properties": {"selector": {"type": "string"}, "timeout_ms": {"type": "integer"}},
            "required": ["selector"],
        },
        handler=lambda selector, timeout_ms=8000: _wrap(
            lambda: {"text": _page().locator(selector).first.text_content(timeout=timeout_ms)}
        ),
    )

    registry.register_tool(
        name="pw_click",
        description="Playwright: click element matching selector.",
        input_schema={
            "type": "object",
            "properties": {"selector": {"type": "string"}, "timeout_ms": {"type": "integer"}},
            "required": ["selector"],
        },
        handler=lambda selector, timeout_ms=8000: _wrap(lambda: _page().locator(selector).click(timeout=timeout_ms)),
    )

    registry.register_tool(
        name="pw_fill",
        description="Playwright: fill element matching selector with text.",
        input_schema={
            "type": "object",
            "properties": {"selector": {"type": "string"}, "text": {"type": "string"}, "timeout_ms": {"type": "integer"}},
            "required": ["selector", "text"],
        },
        handler=lambda selector, text, timeout_ms=8000: _wrap(
            lambda: _page().locator(selector).fill(text, timeout=timeout_ms)
        ),
    )

    registry.register_tool(
        name="pw_press",
        description="Playwright: press a key on an element matching selector (e.g., Enter).",
        input_schema={
            "type": "object",
            "properties": {"selector": {"type": "string"}, "key": {"type": "string"}, "timeout_ms": {"type": "integer"}},
            "required": ["selector", "key"],
        },
        handler=lambda selector, key, timeout_ms=8000: _wrap(
            lambda: _page().locator(selector).press(key, timeout=timeout_ms)
        ),
    )

    registry.register_tool(
        name="pw_select_option",
        description="Playwright: select option(s) in a <select> element.",
        input_schema={
            "type": "object",
            "properties": {"selector": {"type": "string"}, "values": {"type": "array", "items": {"type": "string"}}},
            "required": ["selector", "values"],
        },
        handler=lambda selector, values: _wrap(
            lambda: {"selected": _page().locator(selector).select_option(values=values)}
        ),
    )

    # Evaluation ---------------------------------------------------------
    registry.register_tool(
        name="pw_eval",
        description="Playwright: evaluate JavaScript in the page context and return JSON-serializable value.",
        input_schema={"type": "object", "properties": {"expression": {"type": "string"}}, "required": ["expression"]},
        handler=lambda expression: _wrap(lambda: {"result": _page().evaluate(expression)}),
    )

    registry.register_tool(
        name="pw_eval_on_selector",
        description="Playwright: evaluate JavaScript on the first element matching selector. Provide a function body like `el => el.innerText`.",
        input_schema={
            "type": "object",
            "properties": {"selector": {"type": "string"}, "expression": {"type": "string"}},
            "required": ["selector", "expression"],
        },
        handler=lambda selector, expression: _wrap(
            lambda: {"result": _page().locator(selector).first.evaluate(expression)}
        ),
    )

    # Frames -------------------------------------------------------------
    registry.register_tool(
        name="pw_list_frames",
        description="Playwright: list frames on the current page (name + url).",
        input_schema={"type": "object", "properties": {}},
        handler=lambda: _wrap(
            lambda: {
                "frames": [{"name": f.name, "url": f.url} for f in _page().frames]
            }
        ),
    )

    # Storage/cookies ----------------------------------------------------
    registry.register_tool(
        name="pw_get_cookies",
        description="Playwright: get cookies for the current context.",
        input_schema={"type": "object", "properties": {}},
        handler=lambda: _wrap(lambda: {"cookies": router.playwright_session.get_page().context.cookies()}),
    )


def _wrap(fn):
    try:
        result = fn()
        # If fn returns None (Playwright API), normalize to success.
        if result is None:
            return {"success": True}
        if isinstance(result, dict):
            # Always include success for consistency.
            return {"success": True, **result}
        return {"success": True, "result": result}
    except Exception as exc:
        return {"success": False, "error": str(exc)}



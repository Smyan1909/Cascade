"""Body tools wrapped as MCP tools."""

from __future__ import annotations

import os
from typing import Any, Dict, Optional

from cascade_client.grpc_client import CascadeGrpcClient
from cascade_client.models import ActionType, PlatformSource, Selector

from cascade_client.tools import (
    click_element,
    focus_element,
    get_screenshot,
    get_semantic_tree,
    hover_element,
    reset_state,
    scroll_element,
    start_app,
    type_text,
    wait_visible,
)

from mcp_server.automation_router import AutomationRouter


def register_body_tools(
    registry: Any, grpc_client: CascadeGrpcClient, approval_manager: Optional[Any] = None
) -> AutomationRouter:
    """Register all Body tools with the MCP registry."""

    # Route base tools to either gRPC (UIA) or Python Playwright depending on app/selector.
    headless = os.getenv("CASCADE_PW_HEADLESS", "true").strip().lower() not in ("0", "false", "no")
    browser_channel = os.getenv("CASCADE_PW_CHANNEL") or None
    action_timeout_ms = int(os.getenv("CASCADE_PW_ACTION_TIMEOUT_MS", "8000"))
    router = AutomationRouter(
        grpc_client,
        headless=headless,
        browser_channel=browser_channel,
        action_timeout_ms=action_timeout_ms,
    )

    def _ensure_approved(capability_type: str, parameters: Dict[str, str], reason: str) -> bool:
        if approval_manager is None:
            return True
        try:
            from agents.core.approvals import CapabilityRequest

            return bool(
                approval_manager.ensure_approved(
                    CapabilityRequest(
                        capability_type=capability_type,
                        parameters=parameters,
                        reason=reason,
                    )
                )
            )
        except Exception:
            # Fail closed: require explicit approval.
            return False

    # Click element
    registry.register_tool(
        name="click_element",
        description="Click on a UI element",
        input_schema={
            "type": "object",
            "properties": {
                "selector": {
                    "type": "object",
                    "properties": {
                        "platform_source": {
                            "type": "string",
                            "enum": ["WINDOWS", "JAVA", "WEB"],
                        },
                        "id": {"type": "string"},
                        "name": {"type": "string"},
                        "control_type": {
                            "type": "string",
                            "enum": ["BUTTON", "INPUT", "COMBO", "MENU", "TREE", "TABLE", "CUSTOM"],
                        },
                        "path": {"type": "array", "items": {"type": "string"}},
                        "index": {"type": "integer"},
                        "text_hint": {"type": "string"},
                    },
                    "required": ["platform_source"],
                },
            },
            "required": ["selector"],
        },
        handler=lambda selector: (
            router.click_element(_dict_to_selector(selector))
            if _ensure_approved(
                "ui_action",
                {
                    "action": "click_element",
                    "platform": str(selector.get("platform_source", "")),
                },
                reason="Click UI element",
            )
            else {"content": [{"type": "text", "text": "Denied by approval policy."}], "isError": True}
        ),
    )

    # Type text
    registry.register_tool(
        name="type_text",
        description="Type text into a UI element",
        input_schema={
            "type": "object",
            "properties": {
                "selector": {
                    "type": "object",
                    "properties": {
                        "platform_source": {
                            "type": "string",
                            "enum": ["WINDOWS", "JAVA", "WEB"],
                        },
                        "id": {"type": "string"},
                        "name": {"type": "string"},
                        "path": {"type": "array", "items": {"type": "string"}},
                    },
                    "required": ["platform_source"],
                },
                "text": {"type": "string"},
            },
            "required": ["selector", "text"],
        },
        handler=lambda selector, text: (
            router.type_text(_dict_to_selector(selector), text)
            if _ensure_approved(
                "ui_action",
                {
                    "action": "type_text",
                    "platform": str(selector.get("platform_source", "")),
                },
                reason="Type into UI element",
            )
            else {"content": [{"type": "text", "text": "Denied by approval policy."}], "isError": True}
        ),
    )

    # Get semantic tree
    registry.register_tool(
        name="get_semantic_tree",
        description="Get the semantic tree of UI elements",
        input_schema={"type": "object", "properties": {}},
        handler=lambda: router.get_semantic_tree(),
    )

    # Get screenshot
    registry.register_tool(
        name="get_screenshot",
        description="Get a marked screenshot with element labels",
        input_schema={"type": "object", "properties": {}},
        handler=lambda: router.get_screenshot(),
    )

    # Start app
    registry.register_tool(
        name="start_app",
        description="Start an application",
        input_schema={
            "type": "object",
            "properties": {"app_name": {"type": "string"}},
            "required": ["app_name"],
        },
        handler=lambda app_name: (
            router.start_app(app_name)
            if _ensure_approved(
                "process_spawn",
                {"app_name": str(app_name)},
                reason="Start application",
            )
            else {"content": [{"type": "text", "text": "Denied by approval policy."}], "isError": True}
        ),
    )

    # Additional tools
    registry.register_tool(
        name="hover_element",
        description="Hover over a UI element",
        input_schema={
            "type": "object",
            "properties": {
                "selector": {
                    "type": "object",
                    "properties": {
                        "platform_source": {
                            "type": "string",
                            "enum": ["WINDOWS", "JAVA", "WEB"],
                        },
                    },
                    "required": ["platform_source"],
                },
            },
            "required": ["selector"],
        },
        handler=lambda selector: (
            router.hover_element(_dict_to_selector(selector))
            if _ensure_approved(
                "ui_action",
                {
                    "action": "hover_element",
                    "platform": str(selector.get("platform_source", "")),
                },
                reason="Hover UI element",
            )
            else {"content": [{"type": "text", "text": "Denied by approval policy."}], "isError": True}
        ),
    )

    registry.register_tool(
        name="focus_element",
        description="Focus on a UI element",
        input_schema={
            "type": "object",
            "properties": {
                "selector": {
                    "type": "object",
                    "properties": {
                        "platform_source": {
                            "type": "string",
                            "enum": ["WINDOWS", "JAVA", "WEB"],
                        },
                    },
                    "required": ["platform_source"],
                },
            },
            "required": ["selector"],
        },
        handler=lambda selector: (
            router.focus_element(_dict_to_selector(selector))
            if _ensure_approved(
                "ui_action",
                {
                    "action": "focus_element",
                    "platform": str(selector.get("platform_source", "")),
                },
                reason="Focus UI element",
            )
            else {"content": [{"type": "text", "text": "Denied by approval policy."}], "isError": True}
        ),
    )

    registry.register_tool(
        name="scroll_element",
        description="Scroll a UI element",
        input_schema={
            "type": "object",
            "properties": {
                "selector": {
                    "type": "object",
                    "properties": {
                        "platform_source": {
                            "type": "string",
                            "enum": ["WINDOWS", "JAVA", "WEB"],
                        },
                    },
                    "required": ["platform_source"],
                },
            },
            "required": ["selector"],
        },
        handler=lambda selector: (
            router.scroll_element(_dict_to_selector(selector))
            if _ensure_approved(
                "ui_action",
                {
                    "action": "scroll_element",
                    "platform": str(selector.get("platform_source", "")),
                },
                reason="Scroll UI element",
            )
            else {"content": [{"type": "text", "text": "Denied by approval policy."}], "isError": True}
        ),
    )

    registry.register_tool(
        name="wait_visible",
        description="Wait for a UI element to become visible",
        input_schema={
            "type": "object",
            "properties": {
                "selector": {
                    "type": "object",
                    "properties": {
                        "platform_source": {
                            "type": "string",
                            "enum": ["WINDOWS", "JAVA", "WEB"],
                        },
                    },
                    "required": ["platform_source"],
                },
            },
            "required": ["selector"],
        },
        handler=lambda selector: (
            router.wait_visible(_dict_to_selector(selector))
            if _ensure_approved(
                "ui_action",
                {
                    "action": "wait_visible",
                    "platform": str(selector.get("platform_source", "")),
                },
                reason="Wait for UI element",
            )
            else {"content": [{"type": "text", "text": "Denied by approval policy."}], "isError": True}
        ),
    )

    registry.register_tool(
        name="reset_state",
        description="Reset transient state",
        input_schema={"type": "object", "properties": {}},
        handler=lambda: router.reset_state(),
    )

    return router


def _dict_to_selector(selector_dict: Dict[str, Any]) -> Selector:
    """Convert dict to Selector model."""
    platform_map = {
        "WINDOWS": PlatformSource.WINDOWS,
        "JAVA": PlatformSource.JAVA,
        "WEB": PlatformSource.WEB,
    }
    from cascade_client.models import ControlType

    control_type_map = {
        "BUTTON": ControlType.BUTTON,
        "INPUT": ControlType.INPUT,
        "COMBO": ControlType.COMBO,
        "MENU": ControlType.MENU,
        "TREE": ControlType.TREE,
        "TABLE": ControlType.TABLE,
        "CUSTOM": ControlType.CUSTOM,
    }

    platform = platform_map.get(selector_dict.get("platform_source", ""), PlatformSource.PLATFORM_SOURCE_UNSPECIFIED)
    control_type = None
    if "control_type" in selector_dict:
        control_type = control_type_map.get(selector_dict["control_type"])

    return Selector(
        platform_source=platform,
        path=selector_dict.get("path", []),
        id=selector_dict.get("id"),
        name=selector_dict.get("name"),
        control_type=control_type,
        index=selector_dict.get("index"),
        text_hint=selector_dict.get("text_hint"),
    )


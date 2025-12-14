"""Body tools wrapped as MCP tools."""

from __future__ import annotations

from typing import Any, Dict

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


def register_body_tools(registry: Any, grpc_client: CascadeGrpcClient) -> None:
    """Register all Body tools with the MCP registry."""

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
        handler=lambda selector: click_element(grpc_client, _dict_to_selector(selector)),
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
        handler=lambda selector, text: type_text(grpc_client, _dict_to_selector(selector), text),
    )

    # Get semantic tree
    registry.register_tool(
        name="get_semantic_tree",
        description="Get the semantic tree of UI elements",
        input_schema={"type": "object", "properties": {}},
        handler=lambda: get_semantic_tree(grpc_client),
    )

    # Get screenshot
    registry.register_tool(
        name="get_screenshot",
        description="Get a marked screenshot with element labels",
        input_schema={"type": "object", "properties": {}},
        handler=lambda: get_screenshot(grpc_client),
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
        handler=lambda app_name: start_app(grpc_client, app_name),
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
        handler=lambda selector: hover_element(grpc_client, _dict_to_selector(selector)),
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
        handler=lambda selector: focus_element(grpc_client, _dict_to_selector(selector)),
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
        handler=lambda selector: scroll_element(grpc_client, _dict_to_selector(selector)),
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
        handler=lambda selector: wait_visible(grpc_client, _dict_to_selector(selector)),
    )

    registry.register_tool(
        name="reset_state",
        description="Reset transient state",
        input_schema={"type": "object", "properties": {}},
        handler=lambda: reset_state(grpc_client),
    )


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


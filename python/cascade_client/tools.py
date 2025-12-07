"""
LLM tool abstractions for UI automation.

Provides high-level tool functions that wrap gRPC calls for easy LLM integration.
These tools can be exposed to LLMs via function calling, LangChain tools, etc.
"""

from typing import Any, Dict, List, Optional

from cascade_client.grpc_client import CascadeGrpcClient
from cascade_client.models import Action, ActionType, Selector, Status
from cascade_client.vision import get_marked_screenshot


# Tool Functions
def click_element(client: CascadeGrpcClient, selector: Selector) -> Dict[str, Any]:
    """
    Click on a UI element.

    Args:
        client: CascadeGrpcClient instance
        selector: Selector for target element

    Returns:
        Dictionary with success status and message
    """
    action = Action(action_type=ActionType.CLICK, selector=selector)
    status = client.perform_action(action)
    return {"success": status.success, "message": status.message}


def type_text(
    client: CascadeGrpcClient, selector: Selector, text: str
) -> Dict[str, Any]:
    """
    Type text into a UI element.

    Args:
        client: CascadeGrpcClient instance
        selector: Selector for target element
        text: Text to type

    Returns:
        Dictionary with success status and message
    """
    action = Action(action_type=ActionType.TYPE_TEXT, selector=selector, text=text)
    status = client.perform_action(action)
    return {"success": status.success, "message": status.message}


def hover_element(client: CascadeGrpcClient, selector: Selector) -> Dict[str, Any]:
    """
    Hover over a UI element.

    Args:
        client: CascadeGrpcClient instance
        selector: Selector for target element

    Returns:
        Dictionary with success status and message
    """
    action = Action(action_type=ActionType.HOVER, selector=selector)
    status = client.perform_action(action)
    return {"success": status.success, "message": status.message}


def focus_element(client: CascadeGrpcClient, selector: Selector) -> Dict[str, Any]:
    """
    Focus on a UI element.

    Args:
        client: CascadeGrpcClient instance
        selector: Selector for target element

    Returns:
        Dictionary with success status and message
    """
    action = Action(action_type=ActionType.FOCUS, selector=selector)
    status = client.perform_action(action)
    return {"success": status.success, "message": status.message}


def scroll_element(client: CascadeGrpcClient, selector: Selector) -> Dict[str, Any]:
    """
    Scroll a UI element.

    Args:
        client: CascadeGrpcClient instance
        selector: Selector for target element

    Returns:
        Dictionary with success status and message
    """
    action = Action(action_type=ActionType.SCROLL, selector=selector)
    status = client.perform_action(action)
    return {"success": status.success, "message": status.message}


def wait_visible(client: CascadeGrpcClient, selector: Selector) -> Dict[str, Any]:
    """
    Wait for a UI element to become visible.

    Args:
        client: CascadeGrpcClient instance
        selector: Selector for target element

    Returns:
        Dictionary with success status and message
    """
    action = Action(action_type=ActionType.WAIT_VISIBLE, selector=selector)
    status = client.perform_action(action)
    return {"success": status.success, "message": status.message}


def get_semantic_tree(client: CascadeGrpcClient) -> Dict[str, Any]:
    """
    Get the semantic tree of UI elements.

    Args:
        client: CascadeGrpcClient instance

    Returns:
        Dictionary with semantic tree data
    """
    tree = client.get_semantic_tree()
    return {
        "success": True,
        "elements": [
            {
                "id": elem.id,
                "name": elem.name,
                "control_type": elem.control_type.name,
                "platform_source": elem.platform_source.name,
                "parent_id": elem.parent_id,
            }
            for elem in tree.elements
        ],
    }


def get_screenshot(client: CascadeGrpcClient) -> Dict[str, Any]:
    """
    Get a marked screenshot.

    Args:
        client: CascadeGrpcClient instance

    Returns:
        Dictionary with screenshot data (image as base64, marks, format)
    """
    import base64

    image_bytes, marks, fmt = get_marked_screenshot(client)
    image_base64 = base64.b64encode(image_bytes).decode("utf-8")

    return {
        "success": True,
        "image": image_base64,
        "format": fmt.name,
        "marks": [{"element_id": m.element_id, "label": m.label} for m in marks],
    }


def start_app(client: CascadeGrpcClient, app_name: str) -> Dict[str, Any]:
    """
    Start an application.

    Args:
        client: CascadeGrpcClient instance
        app_name: Application name/identifier

    Returns:
        Dictionary with success status and message
    """
    status = client.start_app(app_name)
    return {"success": status.success, "message": status.message}


def reset_state(client: CascadeGrpcClient) -> Dict[str, Any]:
    """
    Reset transient state.

    Args:
        client: CascadeGrpcClient instance

    Returns:
        Dictionary with success status and message
    """
    status = client.reset_state()
    return {"success": status.success, "message": status.message}


# Tool Schema Definitions
def get_tool_schemas() -> List[Dict[str, Any]]:
    """
    Get tool schemas for LLM frameworks.

    Returns a list of tool schemas in a format compatible with:
    - OpenAI function calling
    - LangChain tools
    - Other LLM tool frameworks

    Returns:
        List of tool schema dictionaries
    """
    return [
        {
            "name": "click_element",
            "description": "Click on a UI element",
            "parameters": {
                "type": "object",
                "properties": {
                    "selector": {
                        "type": "object",
                        "description": "Selector for target element",
                        "properties": {
                            "platform_source": {
                                "type": "string",
                                "enum": ["WINDOWS", "JAVA", "WEB"],
                                "description": "Platform source",
                            },
                            "id": {
                                "type": "string",
                                "description": "Element ID (optional)",
                            },
                            "name": {
                                "type": "string",
                                "description": "Element name (optional)",
                            },
                            "control_type": {
                                "type": "string",
                                "enum": [
                                    "BUTTON",
                                    "INPUT",
                                    "COMBO",
                                    "MENU",
                                    "TREE",
                                    "TABLE",
                                    "CUSTOM",
                                ],
                                "description": "Control type (optional)",
                            },
                            "path": {
                                "type": "array",
                                "items": {"type": "string"},
                                "description": "Path components (optional)",
                            },
                            "index": {
                                "type": "integer",
                                "description": "Element index (optional)",
                            },
                            "text_hint": {
                                "type": "string",
                                "description": "Text hint (optional)",
                            },
                        },
                        "required": ["platform_source"],
                    },
                },
                "required": ["selector"],
            },
        },
        {
            "name": "type_text",
            "description": "Type text into a UI element",
            "parameters": {
                "type": "object",
                "properties": {
                    "selector": {
                        "type": "object",
                        "description": "Selector for target element",
                    },
                    "text": {
                        "type": "string",
                        "description": "Text to type",
                    },
                },
                "required": ["selector", "text"],
            },
        },
        {
            "name": "hover_element",
            "description": "Hover over a UI element",
            "parameters": {
                "type": "object",
                "properties": {
                    "selector": {
                        "type": "object",
                        "description": "Selector for target element",
                    },
                },
                "required": ["selector"],
            },
        },
        {
            "name": "focus_element",
            "description": "Focus on a UI element",
            "parameters": {
                "type": "object",
                "properties": {
                    "selector": {
                        "type": "object",
                        "description": "Selector for target element",
                    },
                },
                "required": ["selector"],
            },
        },
        {
            "name": "scroll_element",
            "description": "Scroll a UI element",
            "parameters": {
                "type": "object",
                "properties": {
                    "selector": {
                        "type": "object",
                        "description": "Selector for target element",
                    },
                },
                "required": ["selector"],
            },
        },
        {
            "name": "wait_visible",
            "description": "Wait for a UI element to become visible",
            "parameters": {
                "type": "object",
                "properties": {
                    "selector": {
                        "type": "object",
                        "description": "Selector for target element",
                    },
                },
                "required": ["selector"],
            },
        },
        {
            "name": "get_semantic_tree",
            "description": "Get the semantic tree of UI elements",
            "parameters": {
                "type": "object",
                "properties": {},
                "required": [],
            },
        },
        {
            "name": "get_screenshot",
            "description": "Get a marked screenshot with element labels",
            "parameters": {
                "type": "object",
                "properties": {},
                "required": [],
            },
        },
        {
            "name": "start_app",
            "description": "Start an application",
            "parameters": {
                "type": "object",
                "properties": {
                    "app_name": {
                        "type": "string",
                        "description": "Application name/identifier",
                    },
                },
                "required": ["app_name"],
            },
        },
        {
            "name": "reset_state",
            "description": "Reset transient state (cached trees, marks, sessions)",
            "parameters": {
                "type": "object",
                "properties": {},
                "required": [],
            },
        },
    ]


# Tool function mapping for dynamic invocation
TOOL_FUNCTIONS = {
    "click_element": click_element,
    "type_text": type_text,
    "hover_element": hover_element,
    "focus_element": focus_element,
    "scroll_element": scroll_element,
    "wait_visible": wait_visible,
    "get_semantic_tree": get_semantic_tree,
    "get_screenshot": get_screenshot,
    "start_app": start_app,
    "reset_state": reset_state,
}


def call_tool(
    client: CascadeGrpcClient, tool_name: str, **kwargs
) -> Dict[str, Any]:
    """
    Dynamically call a tool by name.

    Args:
        client: CascadeGrpcClient instance
        tool_name: Name of the tool to call
        **kwargs: Arguments for the tool

    Returns:
        Tool result dictionary

    Raises:
        ValueError: If tool name is not found
    """
    if tool_name not in TOOL_FUNCTIONS:
        raise ValueError(f"Unknown tool: {tool_name}")

    func = TOOL_FUNCTIONS[tool_name]
    return func(client, **kwargs)


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


# Coordinate-Based Fallback Tools
# These tools provide direct mouse/keyboard control when element detection fails

def click_at_coordinates(
    x: float, y: float, screen_width: int = 1920, screen_height: int = 1080
) -> Dict[str, Any]:
    """
    Click at screen coordinates. Fallback when element detection fails.

    Args:
        x: Normalized X coordinate (0.0-1.0) or pixel coordinate if > 1
        y: Normalized Y coordinate (0.0-1.0) or pixel coordinate if > 1
        screen_width: Screen width for normalized coordinates (default 1920)
        screen_height: Screen height for normalized coordinates (default 1080)

    Returns:
        Dictionary with success status
    """
    try:
        import pyautogui
        
        # Convert normalized coordinates to pixels if needed
        if 0.0 <= x <= 1.0:
            px = int(x * screen_width)
        else:
            px = int(x)
        
        if 0.0 <= y <= 1.0:
            py = int(y * screen_height)
        else:
            py = int(y)
        
        pyautogui.click(px, py)
        return {"success": True, "message": f"Clicked at ({px}, {py})"}
    except ImportError:
        return {"success": False, "message": "pyautogui not installed. Run: pip install pyautogui"}
    except Exception as e:
        return {"success": False, "message": f"Click failed: {str(e)}"}


def type_at_coordinates(
    x: float, y: float, text: str,
    screen_width: int = 1920, screen_height: int = 1080
) -> Dict[str, Any]:
    """
    Click at coordinates and type text. Fallback for element-based typing.

    Args:
        x: Normalized X coordinate (0.0-1.0) or pixel coordinate if > 1
        y: Normalized Y coordinate (0.0-1.0) or pixel coordinate if > 1
        text: Text to type
        screen_width: Screen width for normalized coordinates
        screen_height: Screen height for normalized coordinates

    Returns:
        Dictionary with success status
    """
    try:
        import pyautogui
        
        # Convert normalized coordinates to pixels if needed
        if 0.0 <= x <= 1.0:
            px = int(x * screen_width)
        else:
            px = int(x)
        
        if 0.0 <= y <= 1.0:
            py = int(y * screen_height)
        else:
            py = int(y)
        
        pyautogui.click(px, py)
        pyautogui.typewrite(text, interval=0.02)
        return {"success": True, "message": f"Typed '{text}' at ({px}, {py})"}
    except ImportError:
        return {"success": False, "message": "pyautogui not installed. Run: pip install pyautogui"}
    except Exception as e:
        return {"success": False, "message": f"Type failed: {str(e)}"}


def mouse_move(
    x: float, y: float, screen_width: int = 1920, screen_height: int = 1080
) -> Dict[str, Any]:
    """
    Move mouse to coordinates without clicking.

    Args:
        x: Normalized X coordinate (0.0-1.0) or pixel coordinate if > 1
        y: Normalized Y coordinate (0.0-1.0) or pixel coordinate if > 1
        screen_width: Screen width for normalized coordinates
        screen_height: Screen height for normalized coordinates

    Returns:
        Dictionary with success status
    """
    try:
        import pyautogui
        
        # Convert normalized coordinates to pixels if needed
        if 0.0 <= x <= 1.0:
            px = int(x * screen_width)
        else:
            px = int(x)
        
        if 0.0 <= y <= 1.0:
            py = int(y * screen_height)
        else:
            py = int(y)
        
        pyautogui.moveTo(px, py)
        return {"success": True, "message": f"Moved to ({px}, {py})"}
    except ImportError:
        return {"success": False, "message": "pyautogui not installed. Run: pip install pyautogui"}
    except Exception as e:
        return {"success": False, "message": f"Move failed: {str(e)}"}


def keyboard_type(text: str, interval: float = 0.02) -> Dict[str, Any]:
    """
    Type text using keyboard without targeting a specific element.

    Args:
        text: Text to type
        interval: Delay between keystrokes in seconds

    Returns:
        Dictionary with success status
    """
    try:
        import pyautogui
        
        pyautogui.typewrite(text, interval=interval)
        return {"success": True, "message": f"Typed '{text}'"}
    except ImportError:
        return {"success": False, "message": "pyautogui not installed. Run: pip install pyautogui"}
    except Exception as e:
        return {"success": False, "message": f"Type failed: {str(e)}"}


def keyboard_hotkey(*keys: str) -> Dict[str, Any]:
    """
    Press a keyboard hotkey combination.

    Args:
        *keys: Keys to press (e.g., 'ctrl', 'c' for Ctrl+C)

    Returns:
        Dictionary with success status
    """
    try:
        import pyautogui
        
        pyautogui.hotkey(*keys)
        return {"success": True, "message": f"Pressed hotkey: {'+'.join(keys)}"}
    except ImportError:
        return {"success": False, "message": "pyautogui not installed. Run: pip install pyautogui"}
    except Exception as e:
        return {"success": False, "message": f"Hotkey failed: {str(e)}"}


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
    # Coordinate-based fallback tools (don't need client)
    "click_at_coordinates": click_at_coordinates,
    "type_at_coordinates": type_at_coordinates,
    "mouse_move": mouse_move,
    "keyboard_type": keyboard_type,
    "keyboard_hotkey": keyboard_hotkey,
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


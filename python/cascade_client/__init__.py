"""
Cascade Python Client SDK

Shared Python client SDK for gRPC access, selector utilities, and auth/context handling.
Used by Explorer, Worker, and Orchestrator agents.
"""

from cascade_client.auth.context import CascadeContext
from cascade_client.grpc_client import CascadeGrpcClient
from cascade_client.a2a import AgentA2AClient
from cascade_client.models import (
    Action,
    ActionType,
    ControlType,
    ImageFormat,
    Mark,
    NormalizedRectangle,
    PlatformSource,
    Screenshot,
    Selector,
    SemanticTree,
    StartAppRequest,
    Status,
    UIElement,
)
from cascade_client.selectors import (
    by_control_type,
    by_id,
    by_index,
    by_name,
    by_path,
    with_text_hint,
)
from cascade_client.tools import (
    click_element,
    focus_element,
    get_screenshot,
    get_semantic_tree,
    get_tool_schemas,
    hover_element,
    reset_state,
    scroll_element,
    start_app,
    type_text,
    wait_visible,
)
from cascade_client.vision import get_marked_screenshot

__all__ = [
    # Client
    "CascadeGrpcClient",
    "AgentA2AClient",
    # Context
    "CascadeContext",
    # Models
    "Action",
    "ActionType",
    "ControlType",
    "ImageFormat",
    "Mark",
    "NormalizedRectangle",
    "PlatformSource",
    "Screenshot",
    "Selector",
    "SemanticTree",
    "StartAppRequest",
    "Status",
    "UIElement",
    # Selectors
    "by_control_type",
    "by_id",
    "by_index",
    "by_name",
    "by_path",
    "with_text_hint",
    # Tools
    "click_element",
    "focus_element",
    "get_screenshot",
    "get_semantic_tree",
    "get_tool_schemas",
    "hover_element",
    "reset_state",
    "scroll_element",
    "start_app",
    "type_text",
    "wait_visible",
    # Vision
    "get_marked_screenshot",
]

__version__ = "0.1.0"


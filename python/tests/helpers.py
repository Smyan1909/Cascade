"""
Test helpers and utilities for Cascade Python Client SDK tests.
"""

from typing import Any, Dict

from cascade_client.models import (
    ActionType,
    ControlType,
    ImageFormat,
    PlatformSource,
    Selector,
)


def create_test_selector(
    platform: PlatformSource = PlatformSource.WEB,
    element_id: str = "test_id",
    **kwargs,
) -> Selector:
    """Create a test selector with default values."""
    return Selector(
        platform_source=platform,
        id=element_id,
        path=kwargs.get("path", []),
        name=kwargs.get("name"),
        control_type=kwargs.get("control_type"),
        index=kwargs.get("index"),
        text_hint=kwargs.get("text_hint"),
    )


def create_test_action_dict(
    action_type: str = "CLICK",
    selector: Dict[str, Any] = None,
    **kwargs,
) -> Dict[str, Any]:
    """Create a test action dictionary."""
    if selector is None:
        selector = {
            "platform_source": "WEB",
            "id": "test_id",
        }

    action = {
        "action_type": action_type,
        "selector": selector,
    }

    if "text" in kwargs:
        action["text"] = kwargs["text"]
    elif "number" in kwargs:
        action["number"] = kwargs["number"]
    elif "json_payload" in kwargs:
        action["json_payload"] = kwargs["json_payload"]

    return action


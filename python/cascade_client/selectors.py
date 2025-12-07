"""
Platform-neutral selector builder utilities.

Provides functions to create normalized selectors for targeting UI elements
across different platforms (Windows, Java, Web) without platform-specific details.
"""

from typing import List, Optional

from cascade_client.models import ControlType, PlatformSource, Selector


def by_id(
    platform_source: PlatformSource, element_id: str, **kwargs
) -> Selector:
    """
    Create a selector that targets an element by its stable ID.

    Args:
        platform_source: Platform to target (WINDOWS, JAVA, WEB)
        element_id: Stable element ID within a session
        **kwargs: Additional selector filters (path, name, control_type, index, text_hint)

    Returns:
        Selector configured to target element by ID
    """
    return Selector(
        platform_source=platform_source,
        id=element_id,
        path=kwargs.get("path", []),
        name=kwargs.get("name"),
        control_type=kwargs.get("control_type"),
        index=kwargs.get("index"),
        text_hint=kwargs.get("text_hint"),
    )


def by_name(
    platform_source: PlatformSource, name: str, **kwargs
) -> Selector:
    """
    Create a selector that targets an element by its name.

    Args:
        platform_source: Platform to target (WINDOWS, JAVA, WEB)
        name: Element name
        **kwargs: Additional selector filters (path, id, control_type, index, text_hint)

    Returns:
        Selector configured to target element by name
    """
    return Selector(
        platform_source=platform_source,
        name=name,
        path=kwargs.get("path", []),
        id=kwargs.get("id"),
        control_type=kwargs.get("control_type"),
        index=kwargs.get("index"),
        text_hint=kwargs.get("text_hint"),
    )


def by_control_type(
    platform_source: PlatformSource, control_type: ControlType, **kwargs
) -> Selector:
    """
    Create a selector that targets elements by control type.

    Args:
        platform_source: Platform to target (WINDOWS, JAVA, WEB)
        control_type: Control type (BUTTON, INPUT, COMBO, etc.)
        **kwargs: Additional selector filters (path, id, name, index, text_hint)

    Returns:
        Selector configured to target elements by control type
    """
    return Selector(
        platform_source=platform_source,
        control_type=control_type,
        path=kwargs.get("path", []),
        id=kwargs.get("id"),
        name=kwargs.get("name"),
        index=kwargs.get("index"),
        text_hint=kwargs.get("text_hint"),
    )


def by_path(
    platform_source: PlatformSource, path: List[str], **kwargs
) -> Selector:
    """
    Create a selector that targets an element by its path.

    Args:
        platform_source: Platform to target (WINDOWS, JAVA, WEB)
        path: List of path components (platform-specific interpretation)
        **kwargs: Additional selector filters (id, name, control_type, index, text_hint)

    Returns:
        Selector configured to target element by path
    """
    return Selector(
        platform_source=platform_source,
        path=path,
        id=kwargs.get("id"),
        name=kwargs.get("name"),
        control_type=kwargs.get("control_type"),
        index=kwargs.get("index"),
        text_hint=kwargs.get("text_hint"),
    )


def by_index(
    platform_source: PlatformSource, index: int, **kwargs
) -> Selector:
    """
    Create a selector that targets an element by its index.

    Args:
        platform_source: Platform to target (WINDOWS, JAVA, WEB)
        index: Element index
        **kwargs: Additional selector filters (path, id, name, control_type, text_hint)

    Returns:
        Selector configured to target element by index
    """
    return Selector(
        platform_source=platform_source,
        index=index,
        path=kwargs.get("path", []),
        id=kwargs.get("id"),
        name=kwargs.get("name"),
        control_type=kwargs.get("control_type"),
        text_hint=kwargs.get("text_hint"),
    )


def with_text_hint(selector: Selector, text_hint: str) -> Selector:
    """
    Add a text hint to an existing selector.

    This is a modifier function that creates a new selector with the text hint added.
    Useful for disambiguating elements that match other criteria.

    Args:
        selector: Existing selector to modify
        text_hint: Text hint to add for disambiguation

    Returns:
        New selector with text hint added
    """
    return Selector(
        platform_source=selector.platform_source,
        path=selector.path,
        id=selector.id,
        name=selector.name,
        control_type=selector.control_type,
        index=selector.index,
        text_hint=text_hint,
    )


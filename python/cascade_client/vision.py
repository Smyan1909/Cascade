"""
Vision utilities for marked screenshots.

Provides convenience functions for working with screenshots and marks
returned from the Body server.
"""

from pathlib import Path
from typing import List, Tuple

from cascade_client.grpc_client import CascadeGrpcClient
from cascade_client.models import ImageFormat, Mark, Screenshot


def get_marked_screenshot(
    client: CascadeGrpcClient,
) -> Tuple[bytes, List[Mark], ImageFormat]:
    """
    Get a marked screenshot from the Body server.

    This is a convenience function that calls GetMarkedScreenshot RPC,
    decodes the response, and returns the image bytes, marks, and format.

    Args:
        client: CascadeGrpcClient instance

    Returns:
        Tuple of (image_bytes, marks_list, format):
        - image_bytes: Raw image bytes (PNG or JPEG)
        - marks_list: List of Mark objects with element_id and label
        - format: ImageFormat enum (PNG or JPEG)

    Example:
        >>> client = CascadeGrpcClient(endpoint="localhost:50051")
        >>> image_bytes, marks, fmt = get_marked_screenshot(client)
        >>> print(f"Screenshot: {len(image_bytes)} bytes, {len(marks)} marks, format: {fmt}")
    """
    proto_response = client.get_marked_screenshot()
    screenshot = Screenshot.from_proto(proto_response)

    return screenshot.image, screenshot.marks, screenshot.format


async def get_marked_screenshot_async(
    client: CascadeGrpcClient,
) -> Tuple[bytes, List[Mark], ImageFormat]:
    """
    Get a marked screenshot from the Body server (async).

    Args:
        client: CascadeGrpcClient instance

    Returns:
        Tuple of (image_bytes, marks_list, format)
    """
    proto_response = await client.get_marked_screenshot_async()
    screenshot = Screenshot.from_proto(proto_response)

    return screenshot.image, screenshot.marks, screenshot.format


def save_screenshot(
    image_bytes: bytes,
    file_path: str,
    format: ImageFormat = ImageFormat.IMAGE_FORMAT_UNSPECIFIED,
) -> None:
    """
    Save screenshot image bytes to a file.

    Args:
        image_bytes: Raw image bytes
        file_path: Path where to save the file
        format: Image format (PNG or JPEG). If not provided, inferred from file extension.

    Example:
        >>> image_bytes, marks, fmt = get_marked_screenshot(client)
        >>> save_screenshot(image_bytes, "screenshot.png", fmt)
    """
    path = Path(file_path)

    # Infer format from extension if not provided
    if format == ImageFormat.IMAGE_FORMAT_UNSPECIFIED:
        ext = path.suffix.lower()
        if ext in (".jpg", ".jpeg"):
            format = ImageFormat.JPEG
        else:
            format = ImageFormat.PNG

    # Ensure file extension matches format
    if format == ImageFormat.JPEG and path.suffix.lower() not in (".jpg", ".jpeg"):
        path = path.with_suffix(".jpg")
    elif format == ImageFormat.PNG and path.suffix.lower() != ".png":
        path = path.with_suffix(".png")

    # Write file
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(image_bytes)


def get_mark_by_element_id(marks: List[Mark], element_id: str) -> Mark | None:
    """
    Find a mark by element ID.

    Args:
        marks: List of Mark objects
        element_id: Element ID to search for

    Returns:
        Mark object if found, None otherwise
    """
    for mark in marks:
        if mark.element_id == element_id:
            return mark
    return None


def get_marks_by_label(marks: List[Mark], label: str) -> List[Mark]:
    """
    Find all marks with a specific label.

    Args:
        marks: List of Mark objects
        label: Label to search for

    Returns:
        List of Mark objects with matching label
    """
    return [mark for mark in marks if mark.label == label]


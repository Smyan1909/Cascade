import base64

from cascade_client.models import ImageFormat, Mark, Screenshot
from cascade_client.playwright_semantics import screenshot_to_tool_payload


def test_screenshot_to_tool_payload_base64_and_marks():
    shot = Screenshot(
        image=b"abc",
        format=ImageFormat.PNG,
        marks=[Mark(element_id="e1", label="1")],
    )
    payload = screenshot_to_tool_payload(shot)
    assert payload["success"] is True
    assert payload["format"] == "PNG"
    assert payload["image"] == base64.b64encode(b"abc").decode("utf-8")
    assert payload["marks"] == [{"element_id": "e1", "label": "1"}]



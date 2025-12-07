"""
Unit tests for selectors.py (selector builder functions).
"""

import pytest

from cascade_client.models import ControlType, PlatformSource, Selector
from cascade_client.selectors import (
    by_control_type,
    by_id,
    by_index,
    by_name,
    by_path,
    with_text_hint,
)


class TestSelectorBuilders:
    """Tests for selector builder functions."""

    def test_by_id(self):
        """Test by_id selector builder."""
        selector = by_id(PlatformSource.WEB, "btn_submit")
        assert selector.platform_source == PlatformSource.WEB
        assert selector.id == "btn_submit"
        assert selector.path == []

    def test_by_id_with_kwargs(self):
        """Test by_id with additional filters."""
        selector = by_id(
            PlatformSource.WEB,
            "btn_submit",
            path=["html", "body"],
            control_type=ControlType.BUTTON,
        )
        assert selector.id == "btn_submit"
        assert selector.path == ["html", "body"]
        assert selector.control_type == ControlType.BUTTON

    def test_by_name(self):
        """Test by_name selector builder."""
        selector = by_name(PlatformSource.WINDOWS, "Submit Button")
        assert selector.platform_source == PlatformSource.WINDOWS
        assert selector.name == "Submit Button"

    def test_by_control_type(self):
        """Test by_control_type selector builder."""
        selector = by_control_type(PlatformSource.WEB, ControlType.BUTTON)
        assert selector.platform_source == PlatformSource.WEB
        assert selector.control_type == ControlType.BUTTON

    def test_by_path(self):
        """Test by_path selector builder."""
        path = ["html", "body", "div", "button"]
        selector = by_path(PlatformSource.WEB, path)
        assert selector.platform_source == PlatformSource.WEB
        assert selector.path == path

    def test_by_index(self):
        """Test by_index selector builder."""
        selector = by_index(PlatformSource.WEB, 0)
        assert selector.platform_source == PlatformSource.WEB
        assert selector.index == 0

    def test_with_text_hint(self, sample_selector):
        """Test with_text_hint modifier."""
        new_selector = with_text_hint(sample_selector, "Click me")
        assert new_selector.text_hint == "Click me"
        # Original selector should be unchanged
        assert sample_selector.text_hint != "Click me"

    def test_combined_selectors(self):
        """Test combining multiple selector criteria."""
        selector = by_id(
            PlatformSource.WEB,
            "btn_submit",
            name="Submit",
            control_type=ControlType.BUTTON,
            index=0,
        )
        assert selector.id == "btn_submit"
        assert selector.name == "Submit"
        assert selector.control_type == ControlType.BUTTON
        assert selector.index == 0


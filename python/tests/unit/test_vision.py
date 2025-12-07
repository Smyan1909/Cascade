"""
Unit tests for vision.py (vision helpers).
"""

import pytest

from cascade_client.models import ImageFormat, Mark
from cascade_client.vision import (
    get_mark_by_element_id,
    get_marks_by_label,
    save_screenshot,
)


class TestVisionHelpers:
    """Tests for vision helper functions."""

    def test_get_mark_by_element_id(self):
        """Test finding mark by element ID."""
        marks = [
            Mark(element_id="elem1", label="1"),
            Mark(element_id="elem2", label="2"),
            Mark(element_id="elem3", label="3"),
        ]

        mark = get_mark_by_element_id(marks, "elem2")
        assert mark is not None
        assert mark.element_id == "elem2"
        assert mark.label == "2"

        mark = get_mark_by_element_id(marks, "nonexistent")
        assert mark is None

    def test_get_marks_by_label(self):
        """Test finding marks by label."""
        marks = [
            Mark(element_id="elem1", label="1"),
            Mark(element_id="elem2", label="2"),
            Mark(element_id="elem3", label="1"),
        ]

        found = get_marks_by_label(marks, "1")
        assert len(found) == 2
        assert all(m.label == "1" for m in found)

        found = get_marks_by_label(marks, "nonexistent")
        assert len(found) == 0

    def test_save_screenshot_png(self, tmp_path):
        """Test saving screenshot as PNG."""
        image_bytes = b"fake_png_data"
        file_path = tmp_path / "screenshot.png"

        save_screenshot(image_bytes, str(file_path), ImageFormat.PNG)
        assert file_path.exists()
        assert file_path.read_bytes() == image_bytes

    def test_save_screenshot_jpeg(self, tmp_path):
        """Test saving screenshot as JPEG."""
        image_bytes = b"fake_jpeg_data"
        file_path = tmp_path / "screenshot.jpg"

        save_screenshot(image_bytes, str(file_path), ImageFormat.JPEG)
        assert file_path.exists()
        assert file_path.read_bytes() == image_bytes

    def test_save_screenshot_auto_format(self, tmp_path):
        """Test saving screenshot with auto-detected format."""
        image_bytes = b"fake_image_data"

        # PNG from extension
        png_path = tmp_path / "test.png"
        save_screenshot(image_bytes, str(png_path))
        assert png_path.exists()

        # JPEG from extension
        jpg_path = tmp_path / "test.jpg"
        save_screenshot(image_bytes, str(jpg_path))
        assert jpg_path.exists()

    def test_save_screenshot_creates_directory(self, tmp_path):
        """Test that save_screenshot creates parent directories."""
        image_bytes = b"fake_image_data"
        file_path = tmp_path / "subdir" / "screenshot.png"

        save_screenshot(image_bytes, str(file_path))
        assert file_path.exists()
        assert file_path.parent.exists()


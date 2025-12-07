"""
Unit tests for models.py (Pydantic models).
"""

import pytest

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


class TestNormalizedRectangle:
    """Tests for NormalizedRectangle model."""

    def test_create_valid(self):
        """Test creating a valid NormalizedRectangle."""
        rect = NormalizedRectangle(x=0.1, y=0.2, width=0.3, height=0.4)
        assert rect.x == 0.1
        assert rect.y == 0.2
        assert rect.width == 0.3
        assert rect.height == 0.4

    def test_validation_bounds(self):
        """Test that values are bounded between 0 and 1."""
        # Valid values
        NormalizedRectangle(x=0.0, y=0.0, width=1.0, height=1.0)
        NormalizedRectangle(x=0.5, y=0.5, width=0.5, height=0.5)

        # Invalid values should raise validation error
        with pytest.raises(Exception):  # Pydantic validation error
            NormalizedRectangle(x=-0.1, y=0.0, width=1.0, height=1.0)

        with pytest.raises(Exception):
            NormalizedRectangle(x=0.0, y=0.0, width=1.1, height=1.0)


class TestUIElement:
    """Tests for UIElement model."""

    def test_create_minimal(self):
        """Test creating minimal UIElement."""
        elem = UIElement(id="elem1", name="Test")
        assert elem.id == "elem1"
        assert elem.name == "Test"
        assert elem.control_type == ControlType.CONTROL_TYPE_UNSPECIFIED

    def test_create_full(self, sample_ui_element):
        """Test creating full UIElement."""
        assert sample_ui_element.id == "elem1"
        assert sample_ui_element.name == "Test Button"
        assert sample_ui_element.control_type == ControlType.BUTTON
        assert sample_ui_element.bounding_box is not None
        assert sample_ui_element.platform_source == PlatformSource.WINDOWS


class TestSemanticTree:
    """Tests for SemanticTree model."""

    def test_create_empty(self):
        """Test creating empty semantic tree."""
        tree = SemanticTree()
        assert len(tree.elements) == 0

    def test_create_with_elements(self, sample_ui_element):
        """Test creating semantic tree with elements."""
        tree = SemanticTree(elements=[sample_ui_element])
        assert len(tree.elements) == 1
        assert tree.elements[0].id == "elem1"

    def test_to_graph(self, sample_ui_element):
        """Test converting semantic tree to graph."""
        parent = UIElement(
            id="parent1",
            name="Parent",
            control_type=ControlType.CUSTOM,
            parent_id="",
        )
        tree = SemanticTree(elements=[parent, sample_ui_element])

        graph = tree.to_graph()
        assert "parent1" in graph
        assert "elem1" in graph
        assert graph["parent1"]["children"] == ["elem1"]
        assert graph["elem1"]["parent"] == "parent1"

    def test_get_element_by_id(self, sample_ui_element):
        """Test getting element by ID."""
        tree = SemanticTree(elements=[sample_ui_element])
        elem = tree.get_element_by_id("elem1")
        assert elem is not None
        assert elem.id == "elem1"

        elem = tree.get_element_by_id("nonexistent")
        assert elem is None

    def test_get_elements_by_control_type(self, sample_ui_element):
        """Test getting elements by control type."""
        input_elem = UIElement(
            id="elem2",
            name="Input",
            control_type=ControlType.INPUT,
        )
        tree = SemanticTree(elements=[sample_ui_element, input_elem])

        buttons = tree.get_elements_by_control_type(ControlType.BUTTON)
        assert len(buttons) == 1
        assert buttons[0].id == "elem1"

        inputs = tree.get_elements_by_control_type(ControlType.INPUT)
        assert len(inputs) == 1
        assert inputs[0].id == "elem2"


class TestSelector:
    """Tests for Selector model."""

    def test_create_minimal(self):
        """Test creating minimal selector."""
        selector = Selector(platform_source=PlatformSource.WEB)
        assert selector.platform_source == PlatformSource.WEB
        assert selector.path == []

    def test_create_full(self, sample_selector):
        """Test creating full selector."""
        assert sample_selector.platform_source == PlatformSource.WEB
        assert sample_selector.id == "btn_submit"
        assert sample_selector.control_type == ControlType.BUTTON


class TestAction:
    """Tests for Action model."""

    def test_create_click(self, sample_selector):
        """Test creating click action."""
        action = Action(action_type=ActionType.CLICK, selector=sample_selector)
        assert action.action_type == ActionType.CLICK
        assert action.selector == sample_selector

    def test_create_type_text(self, sample_selector):
        """Test creating type text action."""
        action = Action(
            action_type=ActionType.TYPE_TEXT,
            selector=sample_selector,
            text="Hello",
        )
        assert action.action_type == ActionType.TYPE_TEXT
        assert action.text == "Hello"


class TestMark:
    """Tests for Mark model."""

    def test_create(self):
        """Test creating mark."""
        mark = Mark(element_id="elem1", label="1")
        assert mark.element_id == "elem1"
        assert mark.label == "1"


class TestScreenshot:
    """Tests for Screenshot model."""

    def test_create(self, sample_mark):
        """Test creating screenshot."""
        screenshot = Screenshot(
            image=b"fake_image_data",
            format=ImageFormat.PNG,
            marks=[sample_mark],
        )
        assert screenshot.image == b"fake_image_data"
        assert screenshot.format == ImageFormat.PNG
        assert len(screenshot.marks) == 1


class TestStatus:
    """Tests for Status model."""

    def test_create_success(self):
        """Test creating success status."""
        status = Status(success=True, message="OK")
        assert status.success is True
        assert status.message == "OK"

    def test_create_failure(self):
        """Test creating failure status."""
        status = Status(success=False, message="Error")
        assert status.success is False
        assert status.message == "Error"


class TestStartAppRequest:
    """Tests for StartAppRequest model."""

    def test_create(self):
        """Test creating start app request."""
        request = StartAppRequest(app_name="calculator")
        assert request.app_name == "calculator"


class TestEnums:
    """Tests for enum types."""

    def test_action_type(self):
        """Test ActionType enum."""
        assert ActionType.CLICK == 1
        assert ActionType.TYPE_TEXT == 2

    def test_platform_source(self):
        """Test PlatformSource enum."""
        assert PlatformSource.WINDOWS == 1
        assert PlatformSource.WEB == 3

    def test_control_type(self):
        """Test ControlType enum."""
        assert ControlType.BUTTON == 1
        assert ControlType.INPUT == 2

    def test_image_format(self):
        """Test ImageFormat enum."""
        assert ImageFormat.PNG == 1
        assert ImageFormat.JPEG == 2


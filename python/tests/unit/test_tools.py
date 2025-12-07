"""
Unit tests for tools.py (LLM tool abstractions).
"""

from unittest.mock import MagicMock, patch

import pytest

from cascade_client.grpc_client import CascadeGrpcClient
from cascade_client.models import ActionType, ControlType, PlatformSource, Selector, Status
from cascade_client.selectors import by_id
from cascade_client.tools import (
    call_tool,
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


class TestToolFunctions:
    """Tests for tool functions."""

    @pytest.fixture
    def mock_client(self):
        """Create a mock CascadeGrpcClient."""
        client = MagicMock(spec=CascadeGrpcClient)
        return client

    def test_click_element(self, mock_client):
        """Test click_element tool."""
        mock_client.perform_action.return_value = Status(
            success=True, message="Clicked"
        )

        selector = by_id(PlatformSource.WEB, "btn1")
        result = click_element(mock_client, selector)

        assert result["success"] is True
        assert result["message"] == "Clicked"
        mock_client.perform_action.assert_called_once()
        call_args = mock_client.perform_action.call_args[0][0]
        assert call_args.action_type == ActionType.CLICK

    def test_type_text(self, mock_client):
        """Test type_text tool."""
        mock_client.perform_action.return_value = Status(
            success=True, message="Typed"
        )

        selector = by_id(PlatformSource.WEB, "input1")
        result = type_text(mock_client, selector, "Hello")

        assert result["success"] is True
        mock_client.perform_action.assert_called_once()
        call_args = mock_client.perform_action.call_args[0][0]
        assert call_args.action_type == ActionType.TYPE_TEXT
        assert call_args.text == "Hello"

    def test_hover_element(self, mock_client):
        """Test hover_element tool."""
        mock_client.perform_action.return_value = Status(
            success=True, message="Hovered"
        )

        selector = by_id(PlatformSource.WEB, "elem1")
        result = hover_element(mock_client, selector)

        assert result["success"] is True
        call_args = mock_client.perform_action.call_args[0][0]
        assert call_args.action_type == ActionType.HOVER

    def test_focus_element(self, mock_client):
        """Test focus_element tool."""
        mock_client.perform_action.return_value = Status(
            success=True, message="Focused"
        )

        selector = by_id(PlatformSource.WEB, "elem1")
        result = focus_element(mock_client, selector)

        assert result["success"] is True
        call_args = mock_client.perform_action.call_args[0][0]
        assert call_args.action_type == ActionType.FOCUS

    def test_scroll_element(self, mock_client):
        """Test scroll_element tool."""
        mock_client.perform_action.return_value = Status(
            success=True, message="Scrolled"
        )

        selector = by_id(PlatformSource.WEB, "elem1")
        result = scroll_element(mock_client, selector)

        assert result["success"] is True
        call_args = mock_client.perform_action.call_args[0][0]
        assert call_args.action_type == ActionType.SCROLL

    def test_wait_visible(self, mock_client):
        """Test wait_visible tool."""
        mock_client.perform_action.return_value = Status(
            success=True, message="Visible"
        )

        selector = by_id(PlatformSource.WEB, "elem1")
        result = wait_visible(mock_client, selector)

        assert result["success"] is True
        call_args = mock_client.perform_action.call_args[0][0]
        assert call_args.action_type == ActionType.WAIT_VISIBLE

    def test_get_semantic_tree(self, mock_client):
        """Test get_semantic_tree tool."""
        from cascade_client.models import SemanticTree, UIElement

        mock_tree = SemanticTree(
            elements=[
                UIElement(id="elem1", name="Button", control_type=ControlType.BUTTON)
            ]
        )
        mock_client.get_semantic_tree.return_value = mock_tree

        result = get_semantic_tree(mock_client)

        assert result["success"] is True
        assert len(result["elements"]) == 1
        assert result["elements"][0]["id"] == "elem1"

    def test_get_screenshot(self, mock_client):
        """Test get_screenshot tool."""
        # Mock the get_marked_screenshot function
        with patch("cascade_client.tools.get_marked_screenshot") as mock_get:
            from cascade_client.models import ImageFormat, Mark

            mock_get.return_value = (
                b"fake_image_data",
                [Mark(element_id="elem1", label="1")],
                ImageFormat.PNG,
            )

            result = get_screenshot(mock_client)

            assert result["success"] is True
            assert "image" in result
            assert result["format"] == "PNG"
            assert len(result["marks"]) == 1

    def test_start_app(self, mock_client):
        """Test start_app tool."""
        mock_client.start_app.return_value = Status(
            success=True, message="Started"
        )

        result = start_app(mock_client, "calculator")

        assert result["success"] is True
        mock_client.start_app.assert_called_once_with("calculator")

    def test_reset_state(self, mock_client):
        """Test reset_state tool."""
        mock_client.reset_state.return_value = Status(
            success=True, message="Reset"
        )

        result = reset_state(mock_client)

        assert result["success"] is True
        mock_client.reset_state.assert_called_once()


class TestToolSchemas:
    """Tests for tool schema generation."""

    def test_get_tool_schemas(self):
        """Test getting tool schemas."""
        schemas = get_tool_schemas()

        assert len(schemas) > 0
        assert all("name" in schema for schema in schemas)
        assert all("description" in schema for schema in schemas)
        assert all("parameters" in schema for schema in schemas)

        # Check for specific tools
        tool_names = [schema["name"] for schema in schemas]
        assert "click_element" in tool_names
        assert "type_text" in tool_names
        assert "get_semantic_tree" in tool_names

    def test_tool_schema_structure(self):
        """Test that tool schemas have correct structure."""
        schemas = get_tool_schemas()
        click_schema = next(s for s in schemas if s["name"] == "click_element")

        assert "parameters" in click_schema
        assert click_schema["parameters"]["type"] == "object"
        assert "properties" in click_schema["parameters"]


class TestCallTool:
    """Tests for dynamic tool calling."""

    @pytest.fixture
    def mock_client(self):
        """Create a mock CascadeGrpcClient."""
        client = MagicMock(spec=CascadeGrpcClient)
        client.perform_action.return_value = Status(success=True, message="OK")
        return client

    def test_call_tool_valid(self, mock_client):
        """Test calling a valid tool."""
        selector = by_id(PlatformSource.WEB, "btn1")
        result = call_tool(mock_client, "click_element", selector=selector)

        assert result["success"] is True

    def test_call_tool_invalid(self, mock_client):
        """Test calling an invalid tool."""
        with pytest.raises(ValueError, match="Unknown tool"):
            call_tool(mock_client, "nonexistent_tool")


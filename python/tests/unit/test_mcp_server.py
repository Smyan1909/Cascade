"""Unit tests for MCP server."""

import json
from unittest.mock import MagicMock, patch

import pytest

from mcp_server.server import MCPServer
from mcp_server.tool_registry import ToolRegistry


@pytest.fixture
def registry():
    """Create a test tool registry."""
    reg = ToolRegistry()
    reg.register_tool(
        name="test_tool",
        description="A test tool",
        input_schema={"type": "object", "properties": {}},
        handler=lambda: {"result": "success"},
    )
    return reg


@pytest.fixture
def server(registry):
    """Create an MCP server."""
    return MCPServer(registry)


def test_tool_registry_register():
    """Test tool registration."""
    registry = ToolRegistry()
    registry.register_tool(
        name="test",
        description="Test tool",
        input_schema={"type": "object"},
        handler=lambda x: x,
    )
    assert "test" in registry._tools
    assert "test" in registry._schemas


def test_tool_registry_list():
    """Test tool listing."""
    registry = ToolRegistry()
    registry.register_tool(
        name="test",
        description="Test tool",
        input_schema={"type": "object"},
        handler=lambda: {},
    )
    tools = registry.list_tools()
    assert len(tools) == 1
    assert tools[0]["name"] == "test"


def test_tool_registry_call():
    """Test tool calling."""
    registry = ToolRegistry()
    registry.register_tool(
        name="add",
        description="Add two numbers",
        input_schema={
            "type": "object",
            "properties": {"a": {"type": "integer"}, "b": {"type": "integer"}},
        },
        handler=lambda a, b: {"result": a + b},
    )
    result = registry.call_tool("add", {"a": 2, "b": 3})
    assert "content" in result
    content_text = result["content"][0]["text"]
    data = json.loads(content_text)
    assert data["result"] == 5


def test_mcp_server_initialize(server, registry):
    """Test MCP server initialization."""
    request = {
        "jsonrpc": "2.0",
        "id": 1,
        "method": "initialize",
        "params": {},
    }
    # Mock stdout
    with patch("sys.stdout") as mock_stdout:
        server._handle_request(request)
        # Verify response was sent
        assert mock_stdout.write.called


def test_mcp_server_tools_list(server, registry):
    """Test tools/list request."""
    request = {
        "jsonrpc": "2.0",
        "id": 1,
        "method": "tools/list",
        "params": {},
    }
    with patch("sys.stdout") as mock_stdout:
        server._handle_request(request)
        assert mock_stdout.write.called


def test_mcp_server_tools_call(server, registry):
    """Test tools/call request."""
    request = {
        "jsonrpc": "2.0",
        "id": 1,
        "method": "tools/call",
        "params": {"name": "test_tool", "arguments": {}},
    }
    with patch("sys.stdout") as mock_stdout:
        server._handle_request(request)
        assert mock_stdout.write.called


@pytest.mark.skipif(
    True, reason="Requires langgraph - integration test"
)
def test_mcp_integration():
    """Integration test for MCP server (requires full setup)."""
    pass


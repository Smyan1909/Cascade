"""MCP server implementation."""

from __future__ import annotations

import json
import sys
from typing import Any, Dict, Optional

from .tool_registry import ToolRegistry


class MCPServer:
    """MCP server using stdio transport."""

    def __init__(self, tool_registry: ToolRegistry):
        self._registry = tool_registry
        self._initialized = False

    def _send_response(self, id: Optional[Any], result: Any = None, error: Any = None):
        """Send JSON-RPC response."""
        response = {"jsonrpc": "2.0"}
        if id is not None:
            response["id"] = id
        if error:
            response["error"] = error
        else:
            response["result"] = result
        print(json.dumps(response), flush=True)

    def _handle_request(self, request: Dict[str, Any]):
        """Handle a JSON-RPC request."""
        method = request.get("method")
        params = request.get("params", {})
        request_id = request.get("id")

        if method == "initialize":
            self._initialized = True
            self._send_response(
                request_id,
                {
                    "protocolVersion": "2024-11-05",
                    "capabilities": {
                        "tools": {},
                        "resources": {},
                    },
                    "serverInfo": {
                        "name": "cascade-mcp-server",
                        "version": "1.0.0",
                    },
                },
            )
        elif method == "tools/list":
            tools = self._registry.list_tools()
            self._send_response(request_id, {"tools": tools})
        elif method == "tools/call":
            tool_name = params.get("name")
            arguments = params.get("arguments", {})
            if not tool_name:
                self._send_response(
                    request_id,
                    error={"code": -32602, "message": "Missing tool name"},
                )
                return
            result = self._registry.call_tool(tool_name, arguments)
            self._send_response(request_id, result)
        elif method == "resources/list":
            # Resources for skill discovery
            resources = []
            self._send_response(request_id, {"resources": resources})
        else:
            self._send_response(
                request_id,
                error={"code": -32601, "message": f"Method not found: {method}"},
            )

    def run(self):
        """Run MCP server on stdio."""
        for line in sys.stdin:
            try:
                request = json.loads(line.strip())
                if not request.get("jsonrpc") == "2.0":
                    continue
                self._handle_request(request)
            except json.JSONDecodeError:
                continue
            except Exception as e:
                self._send_response(
                    None, error={"code": -32603, "message": f"Internal error: {str(e)}"}
                )


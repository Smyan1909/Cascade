"""Tool registry for MCP server."""

from __future__ import annotations

import json
from typing import Any, Callable, Dict, List, Optional

from pydantic import BaseModel


class ToolSchema(BaseModel):
    """MCP tool schema."""

    name: str
    description: str
    inputSchema: Dict[str, Any]


class ToolRegistry:
    """Central registry for MCP tools."""

    def __init__(self):
        self._tools: Dict[str, Callable] = {}
        self._schemas: Dict[str, ToolSchema] = {}

    def register_tool(
        self,
        name: str,
        description: str,
        input_schema: Dict[str, Any],
        handler: Callable,
    ) -> None:
        """Register a tool."""
        self._tools[name] = handler
        self._schemas[name] = ToolSchema(
            name=name, description=description, inputSchema=input_schema
        )

    def list_tools(self) -> List[Dict[str, Any]]:
        """List all registered tools."""
        return [
            {
                "name": schema.name,
                "description": schema.description,
                "inputSchema": schema.inputSchema,
            }
            for schema in self._schemas.values()
        ]

    def get_tool(self, name: str) -> Optional[Callable]:
        """Get tool handler by name."""
        return self._tools.get(name)

    def call_tool(self, name: str, arguments: Dict[str, Any]) -> Dict[str, Any]:
        """Call a tool by name with arguments."""
        handler = self._tools.get(name)
        if not handler:
            raise ValueError(f"Tool not found: {name}")

        try:
            result = handler(**arguments)
            if isinstance(result, dict):
                return result
            return {"content": [{"type": "text", "text": json.dumps(result)}]}
        except Exception as e:
            return {
                "content": [{"type": "text", "text": f"Error: {str(e)}"}],
                "isError": True,
            }


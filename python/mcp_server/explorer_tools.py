"""Explorer tools wrapped as MCP tools."""

from __future__ import annotations

from typing import Any, Optional
from urllib.parse import urlparse

from agents.explorer.tools.api_tester import ApiTester
from agents.explorer.tools.web_search import WebSearchClient


def register_explorer_tools(registry: Any, approval_manager: Optional[Any] = None) -> None:
    """Register Explorer tools with the MCP registry."""

    web_search_client = WebSearchClient()
    api_tester = ApiTester()

    # Web search
    registry.register_tool(
        name="web_search",
        description="Search the web for information",
        input_schema={
            "type": "object",
            "properties": {
                "query": {"type": "string", "description": "Search query"},
                "top_k": {
                    "type": "integer",
                    "description": "Number of results to return",
                    "default": 5,
                },
            },
            "required": ["query"],
        },
        handler=lambda query, top_k=5: {
            "content": [
                {
                    "type": "text",
                    "text": str(
                        web_search_client.search(query, top_k)
                        if approval_manager is None
                        else (
                            web_search_client.search(query, top_k)
                            if approval_manager.ensure_approved(
                                __import__("agents.core.approvals", fromlist=["CapabilityRequest"]).CapabilityRequest(
                                    capability_type="network",
                                    parameters={"host": "web_search_provider"},
                                    reason="Web search",
                                )
                            )
                            else "Denied by approval policy."
                        )
                    ),
                }
            ]
        },
    )

    # API tester
    registry.register_tool(
        name="test_api",
        description="Test an API endpoint",
        input_schema={
            "type": "object",
            "properties": {
                "method": {
                    "type": "string",
                    "enum": ["GET", "POST", "PUT", "DELETE", "PATCH"],
                    "description": "HTTP method",
                },
                "url": {"type": "string", "description": "API endpoint URL"},
                "headers": {
                    "type": "object",
                    "description": "HTTP headers",
                    "additionalProperties": {"type": "string"},
                },
                "params": {
                    "type": "object",
                    "description": "Query parameters",
                    "additionalProperties": True,
                },
                "json_body": {
                    "type": "object",
                    "description": "JSON request body",
                    "additionalProperties": True,
                },
            },
            "required": ["method", "url"],
        },
        handler=lambda method, url, headers=None, params=None, json_body=None: {
            "content": [
                {
                    "type": "text",
                    "text": str(
                        api_tester.test(
                            method=method,
                            url=url,
                            headers=headers,
                            params=params,
                            json=json_body,
                        )
                        if approval_manager is None
                        else (
                            api_tester.test(
                                method=method,
                                url=url,
                                headers=headers,
                                params=params,
                                json=json_body,
                            )
                            if approval_manager.ensure_approved(
                                __import__("agents.core.approvals", fromlist=["CapabilityRequest"]).CapabilityRequest(
                                    capability_type="network",
                                    parameters={
                                        "host": (urlparse(url).hostname or "").lower() or "unknown",
                                        "method": str(method).upper(),
                                    },
                                    reason="Test API endpoint",
                                )
                            )
                            else "Denied by approval policy."
                        )
                    ),
                }
            ]
        },
    )


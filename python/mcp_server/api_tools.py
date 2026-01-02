"""API tools for Worker agent.

Provides base tools for HTTP API calls (for web API skills).
"""

from __future__ import annotations

import json
import os
from typing import Any, Dict, Optional

import requests
from urllib.parse import urlparse


def register_api_tools(registry: Any, approval_manager: Optional[Any] = None) -> None:
    """Register API-related tools with the MCP registry."""
    
    # HTTP API tool for web API skills
    registry.register_tool(
        name="call_http_api",
        description="""Execute an HTTP API request.

Use this tool when a skill specifies a web API endpoint. 
Pass the method, URL, and optionally headers and body.

Example:
  call_http_api(method="GET", url="https://api.example.com/data")
  call_http_api(method="POST", url="https://api.example.com/users", 
                headers={"Content-Type": "application/json"},
                body={"name": "John"})
""",
        input_schema={
            "type": "object",
            "properties": {
                "method": {
                    "type": "string",
                    "enum": ["GET", "POST", "PUT", "DELETE", "PATCH"],
                    "description": "HTTP method"
                },
                "url": {
                    "type": "string",
                    "description": "Full URL to call"
                },
                "headers": {
                    "type": "object",
                    "description": "Optional HTTP headers",
                    "additionalProperties": {"type": "string"}
                },
                "body": {
                    "type": "object",
                    "description": "Optional request body (as JSON)"
                },
                "timeout": {
                    "type": "number",
                    "description": "Request timeout in seconds (default: 30)"
                }
            },
            "required": ["method", "url"]
        },
        handler=lambda method, url, headers=None, body=None, timeout=30: _execute_http_request(
            method=method,
            url=url,
            headers=headers,
            body=body,
            timeout=timeout,
            approval_manager=approval_manager,
        ),
    )


def _execute_http_request(
    method: str,
    url: str,
    headers: Optional[Dict[str, str]] = None,
    body: Optional[Dict[str, Any]] = None,
    timeout: float = 30,
    approval_manager: Optional[Any] = None,
) -> Dict[str, Any]:
    """Execute an HTTP request and return the result."""
    try:
        if approval_manager is not None:
            parsed = urlparse(url)
            host = (parsed.hostname or "").lower()
            from agents.core.approvals import CapabilityRequest

            ok = approval_manager.ensure_approved(
                CapabilityRequest(
                    capability_type="network",
                    parameters={
                        "host": host or "unknown",
                        "method": method.upper(),
                        "url_prefix": f"{parsed.scheme}://{host}" if host else "",
                    },
                    reason="HTTP request",
                )
            )
            if not ok:
                return {
                    "content": [{"type": "text", "text": "Denied by approval policy."}],
                    "isError": True,
                }

        response = requests.request(
            method=method.upper(),
            url=url,
            headers=headers,
            json=body if body else None,
            timeout=timeout,
        )
        
        # Try to parse JSON response
        try:
            response_body = response.json()
        except (json.JSONDecodeError, ValueError):
            response_body = response.text[:1000]  # Limit text response
        
        return {
            "content": [{
                "type": "text",
                "text": json.dumps({
                    "success": response.ok,
                    "status_code": response.status_code,
                    "response": response_body,
                })
            }]
        }
        
    except requests.exceptions.Timeout:
        return {
            "content": [{"type": "text", "text": f"Request timed out after {timeout}s"}],
            "isError": True
        }
    except requests.exceptions.RequestException as e:
        return {
            "content": [{"type": "text", "text": f"Request failed: {str(e)}"}],
            "isError": True
        }

"""API tools for Worker agent.

Provides base tools for HTTP API calls (for web API skills)
and scaffolding for native code execution.
"""

from __future__ import annotations

import json
from typing import Any, Dict, Optional

import requests


def register_api_tools(registry: Any) -> None:
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
            method=method, url=url, headers=headers, body=body, timeout=timeout
        ),
    )


def _execute_http_request(
    method: str,
    url: str,
    headers: Optional[Dict[str, str]] = None,
    body: Optional[Dict[str, Any]] = None,
    timeout: float = 30,
) -> Dict[str, Any]:
    """Execute an HTTP request and return the result."""
    try:
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


def register_code_execution_tool(registry: Any, grpc_client: Any = None) -> None:
    """Register scaffolded code execution tool.
    
    NOTE: This is a scaffold. Actual code execution requires:
    1. Body service to implement CodeExecutionService RPC
    2. grpc_client to have execute_code method
    """
    
    registry.register_tool(
        name="execute_code_skill",
        description="""Execute a native code skill (C# via Roslyn, PowerShell, etc.)

This tool runs pre-generated code artifacts for automation tasks that 
require native Windows APIs or COM automation (e.g., Excel, Outlook).

NOTE: This requires the Body service to support code execution.
""",
        input_schema={
            "type": "object",
            "properties": {
                "skill_id": {
                    "type": "string",
                    "description": "ID of the code skill to execute"
                },
                "artifact_id": {
                    "type": "string",
                    "description": "ID of the code artifact to run"
                },
                "inputs": {
                    "type": "object",
                    "description": "Runtime inputs for the code",
                    "additionalProperties": {"type": "string"}
                }
            },
            "required": ["skill_id"]
        },
        handler=lambda skill_id, artifact_id=None, inputs=None: _execute_code_skill(
            skill_id=skill_id, artifact_id=artifact_id, inputs=inputs, grpc_client=grpc_client
        ),
    )


def _execute_code_skill(
    skill_id: str,
    artifact_id: Optional[str] = None,
    inputs: Optional[Dict[str, str]] = None,
    grpc_client: Any = None,
) -> Dict[str, Any]:
    """Execute a code skill via the Body service.
    
    This is a scaffold - actual implementation requires Body support.
    """
    # TODO: Implement when Body service supports CodeExecutionService
    if grpc_client is None:
        return {
            "content": [{
                "type": "text",
                "text": json.dumps({
                    "success": False,
                    "error": "Code execution not yet available. Body service needs CodeExecutionService implementation.",
                    "skill_id": skill_id,
                    "scaffold": True,
                })
            }],
            "isError": True
        }
    
    # Future: Call grpc_client.execute_code(...)
    return {
        "content": [{
            "type": "text",
            "text": json.dumps({
                "success": False,
                "error": "Code execution RPC not implemented in Body service yet.",
                "skill_id": skill_id,
            })
        }],
        "isError": True
    }

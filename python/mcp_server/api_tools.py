"""API tools for Worker agent.

Provides base tools for HTTP API calls (for web API skills)
and scaffolding for native code execution.
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


def register_code_execution_tool(
    registry: Any,
    grpc_client: Any = None,
    *,
    context: Optional[Any] = None,
    approval_manager: Optional[Any] = None,
) -> None:
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
            skill_id=skill_id,
            artifact_id=artifact_id,
            inputs=inputs,
            grpc_client=grpc_client,
            context=context,
            approval_manager=approval_manager,
        ),
    )


def _execute_code_skill(
    skill_id: str,
    artifact_id: Optional[str] = None,
    inputs: Optional[Dict[str, str]] = None,
    grpc_client: Any = None,
    context: Optional[Any] = None,
    approval_manager: Optional[Any] = None,
) -> Dict[str, Any]:
    """Execute a code skill via the Body service.
    
    This is a scaffold - actual implementation requires Body support.
    """
    try:
        from cascade_client.auth.context import CascadeContext
        from storage.firestore_client import FirestoreClient
        from storage.code_artifact import CodeArtifact
        from agents.explorer.skill_map import SkillMap
        from agents.core.approvals import ApprovalManager, CapabilityRequest
        from agents.code_exec.python_executor import PythonArtifactExecutor, PythonExecutionPolicy
        from cascade_client.models import (
            CodeExecutionRequest,
            CodeExecutionResult,
            CodeFile,
            CapabilitySpec,
        )
        from pathlib import Path

        if grpc_client is None:
            raise ValueError("grpc_client is required for code execution")

        # Context is expected to be available in env for Firestore scoping.
        ctx = context or CascadeContext.from_env()
        fs = FirestoreClient(ctx)

        # Resolve artifact_id from skill if not provided.
        resolved_artifact_id = artifact_id
        skill_lang = None
        skill_entry = None
        if not resolved_artifact_id:
            raw_skill = fs.get_skill_map(skill_id)
            if not raw_skill:
                return {
                    "content": [{"type": "text", "text": f"Skill '{skill_id}' not found."}],
                    "isError": True,
                }
            skill = SkillMap.model_validate(raw_skill)
            resolved_artifact_id = skill.metadata.code_artifact_id
            skill_lang = skill.metadata.code_language
            skill_entry = skill.metadata.code_entrypoint

        if not resolved_artifact_id:
            return {
                "content": [{"type": "text", "text": f"No code artifact linked for skill '{skill_id}'."}],
                "isError": True,
            }

        raw_art = fs.get_code_artifact(resolved_artifact_id)
        if not raw_art:
            return {
                "content": [{"type": "text", "text": f"Code artifact '{resolved_artifact_id}' not found."}],
                "isError": True,
            }
        artifact = CodeArtifact.model_validate(raw_art)

        # Approvals (full access is handled by ApprovalManager auto_approve in caller; here we construct from env).
        approvals = approval_manager or ApprovalManager(ctx, fs, auto_approve=False)
        for cap in artifact.capabilities:
            try:
                ok = approvals.ensure_approved(
                    CapabilityRequest(
                        capability_type=str(cap.get("type", "")),
                        parameters={k: str(v) for k, v in (cap.get("parameters") or {}).items()},
                        reason=str(cap.get("reason", "")),
                    )
                )
                if not ok:
                    return {
                        "content": [{"type": "text", "text": "Denied by approval policy."}],
                        "isError": True,
                    }
            except Exception:
                # If capability parsing fails, prompt for a generic approval.
                ok = approvals.ensure_approved(
                    CapabilityRequest(
                        capability_type="code_exec",
                        parameters={"skill_id": skill_id},
                        reason="Execute code artifact",
                    )
                )
                if not ok:
                    return {
                        "content": [{"type": "text", "text": "Denied by approval policy."}],
                        "isError": True,
                    }

        # Determine language and entrypoint.
        language = (skill_lang or (artifact.files[0].language if artifact.files else "") or "").lower()
        if not language:
            language = "python"
        entrypoint = skill_entry or ""

        runtime_inputs = inputs or {}

        if language == "python":
            if not entrypoint:
                # Default to skill_{skill_id}:run if present.
                entrypoint = f"skill_{skill_id}:run"
            policy = PythonExecutionPolicy(
                sandbox_root=Path.cwd() / ".cascade_sandbox",
                allowed_net_hosts=[],
                allow_process_spawn=False,
                timeout_seconds=20.0,
            )
            executor = PythonArtifactExecutor(policy)
            exec_payload = executor.execute(
                files=artifact.files,
                entrypoint=entrypoint,
                inputs=runtime_inputs,
                extra_env={
                    # Ensure the child process can create a CascadeGrpcClient.
                    "CASCADE_GRPC_ENDPOINT": str(getattr(grpc_client, "_endpoint", "") or os.environ.get("CASCADE_GRPC_ENDPOINT", "")),
                },
            )
            return {"content": [{"type": "text", "text": json.dumps(exec_payload)}], "isError": not bool(exec_payload.get("success", False))}

        if language in ("csharp", "cs", "c#"):
            req = CodeExecutionRequest(
                artifact_id=resolved_artifact_id,
                language="csharp",
                skill_id=skill_id,
                inputs={k: str(v) for k, v in runtime_inputs.items()},
                user_id=ctx.user_id,
                app_id=ctx.app_id,
                files=[
                    CodeFile(path=f.path, content=f.content, language=f.language)
                    for f in artifact.files
                ],
                dependencies=list(artifact.dependencies),
                capabilities=[
                    CapabilitySpec(
                        type=str(c.get("type", "")),
                        parameters={k: str(v) for k, v in (c.get("parameters") or {}).items()},
                        reason=str(c.get("reason", "")),
                    )
                    for c in artifact.capabilities
                ],
            )
            res: CodeExecutionResult = grpc_client.execute_code(req)
            payload = {
                "success": res.success,
                "output": res.output,
                "error": res.error,
                "execution_time_ms": res.execution_time_ms,
            }
            return {"content": [{"type": "text", "text": json.dumps(payload)}], "isError": not res.success}

        return {
            "content": [{"type": "text", "text": f"Unsupported code language: {language}"}],
            "isError": True,
        }
    except Exception as e:
        return {"content": [{"type": "text", "text": f"Error executing code skill: {str(e)}"}], "isError": True}

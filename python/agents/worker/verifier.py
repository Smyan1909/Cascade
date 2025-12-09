"""LLM-based verifier for worker task completion."""

from __future__ import annotations

from typing import Any, Dict, List, Optional

from mcp_server.llm_integration import verify_with_tools
from mcp_server.tool_registry import ToolRegistry


def verify_task_completion(
    task: str,
    execution_history: List[Dict[str, Any]],
    observations: Dict[str, Any],
    llm_client=None,
    mcp_registry: Optional[ToolRegistry] = None,
) -> Dict[str, Any]:
    """
    Verify whether the task is complete.

    observations may include semantic tree summaries, OCR text, or other signals.
    Returns dict with keys: {"complete": bool, "reason": str}
    """
    if llm_client is None:
        # Heuristic: if we executed at least one skill, assume success
        return {
            "complete": bool(execution_history),
            "reason": "heuristic fallback",
        }

    if mcp_registry:
        try:
            return verify_with_tools(llm_client, task, execution_history, mcp_registry)
        except Exception:
            pass

    # Fallback to prompt-based verification
    prompt = _build_prompt(task, execution_history, observations)
    try:
        from clients.llm_client import LlmMessage

        messages = [LlmMessage(role="user", content=prompt)]
        response = llm_client.generate(messages=messages)
        import json

        content = response.content.strip()
        if content.startswith("```json"):
            content = content[7:]
        if content.startswith("```"):
            content = content[3:]
        if content.endswith("```"):
            content = content[:-3]
        result = json.loads(content.strip())
        if isinstance(result, dict) and "complete" in result:
            return {
                "complete": bool(result.get("complete")),
                "reason": result.get("reason", ""),
            }
    except Exception:
        pass

    return {"complete": False, "reason": "LLM verifier unavailable"}


def _build_prompt(
    task: str, history: List[Dict[str, Any]], observations: Dict[str, Any]
) -> str:
    return (
        "You are a verifier. Determine if the task is complete.\n"
        f"Task: {task}\n"
        f"Execution history: {history}\n"
        f"Observations (UI/semantic/OCR): {observations}\n"
        "Return JSON: {\"complete\": bool, \"reason\": str}"
    )


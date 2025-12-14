"""MCP-LLM integration helpers."""

from __future__ import annotations

import json
from typing import Any, Dict, List, Optional

from clients.llm_client import LlmClient, LlmMessage, LlmResponse

from .tool_registry import ToolRegistry


def get_mcp_tool_schemas(registry: ToolRegistry) -> List[Dict[str, Any]]:
    """Convert MCP tool schemas to LLM function calling format."""
    tools = registry.list_tools()
    llm_tools = []
    for tool in tools:
        llm_tools.append(
            {
                "type": "function",
                "function": {
                    "name": tool["name"],
                    "description": tool["description"],
                    "parameters": tool["inputSchema"],
                },
            }
        )
    return llm_tools


def execute_llm_with_tools(
    llm_client: LlmClient,
    messages: List[LlmMessage],
    registry: ToolRegistry,
    max_iterations: int = 5,
) -> LlmResponse:
    """
    Execute LLM with tool calling support.

    Handles tool calls by executing them via MCP registry and continuing the conversation.
    """
    tool_schemas = get_mcp_tool_schemas(registry)
    conversation = messages.copy()

    for _ in range(max_iterations):
        response = llm_client.generate(
            messages=conversation,
            tools=tool_schemas if tool_schemas else None,
        )

        # Check if LLM wants to call tools (OpenAI format)
        if response.raw and hasattr(response.raw, "choices") and response.raw.choices:
            message = response.raw.choices[0].message
            tool_calls = getattr(message, "tool_calls", None)
            if tool_calls:
                # Add assistant message with tool calls
                conversation.append(
                    LlmMessage(role="assistant", content=message.content or "")
                )

                # Execute each tool call
                for tool_call in tool_calls:
                    tool_name = tool_call.function.name
                    try:
                        arguments = json.loads(tool_call.function.arguments)
                    except (json.JSONDecodeError, AttributeError):
                        arguments = {}

                    result = registry.call_tool(tool_name, arguments)
                    result_text = ""
                    if isinstance(result, dict) and "content" in result:
                        for content_item in result["content"]:
                            if content_item.get("type") == "text":
                                result_text += content_item.get("text", "")
                    else:
                        result_text = json.dumps(result)

                    # Add tool result to conversation (OpenAI format uses tool role with name)
                    conversation.append(
                        LlmMessage(
                            role="tool",
                            content=result_text,
                        )
                    )
                continue

        # No tool calls, return final response
        return response

    return response


def plan_with_tools(
    llm_client: LlmClient,
    task: str,
    available_skills: List[Any],
    registry: ToolRegistry,
) -> List[Dict[str, Any]]:
    """Plan skill execution using LLM with tool calling."""
    skill_summaries = []
    for skill in available_skills:
        skill_summaries.append(
            {
                "skill_id": skill.metadata.skill_id,
                "capability": skill.metadata.capability or "",
                "description": skill.metadata.description or "",
                "inputs": skill.metadata.inputs or {},
                "outputs": skill.metadata.outputs or {},
            }
        )

    prompt = (
        f"You are a planner. Choose which skills to execute to complete this task: {task}\n"
        f"Available skills: {json.dumps(skill_summaries, indent=2)}\n"
        "You can use tools like get_semantic_tree to inspect the current UI state.\n"
        "Return a JSON list of steps: [{\"skill_id\": str, \"rationale\": str, \"inputs\": object}]"
    )

    messages = [LlmMessage(role="user", content=prompt)]
    response = execute_llm_with_tools(llm_client, messages, registry)

    try:
        # Try to parse JSON from response
        content = response.content.strip()
        if content.startswith("```json"):
            content = content[7:]
        if content.startswith("```"):
            content = content[3:]
        if content.endswith("```"):
            content = content[:-3]
        content = content.strip()
        plan = json.loads(content)
        if isinstance(plan, list):
            return plan
    except json.JSONDecodeError:
        pass

    return []


def verify_with_tools(
    llm_client: LlmClient,
    task: str,
    execution_history: List[Dict[str, Any]],
    registry: ToolRegistry,
) -> Dict[str, Any]:
    """Verify task completion using LLM with tool calling."""
    prompt = (
        f"Verify if this task is complete: {task}\n"
        f"Execution history: {json.dumps(execution_history, indent=2)}\n"
        "You can use tools like get_semantic_tree or get_screenshot to inspect the current state.\n"
        "Return JSON: {\"complete\": bool, \"reason\": str}"
    )

    messages = [LlmMessage(role="user", content=prompt)]
    response = execute_llm_with_tools(llm_client, messages, registry)

    try:
        content = response.content.strip()
        if content.startswith("```json"):
            content = content[7:]
        if content.startswith("```"):
            content = content[3:]
        if content.endswith("```"):
            content = content[:-3]
        content = content.strip()
        result = json.loads(content)
        if isinstance(result, dict) and "complete" in result:
            return result
    except json.JSONDecodeError:
        pass

    return {"complete": False, "reason": "Could not parse verification result"}


"""Autonomous agent components using LangGraph patterns.

Provides:
- ReActVerifier: A ReAct loop for verification/testing phases
- AutonomousAgent: Full ReAct agent for autonomous task execution
"""

from __future__ import annotations

import time
from dataclasses import dataclass, field
from enum import Enum
from typing import Any, Callable, Dict, List, Optional

from langchain_core.messages import HumanMessage, AIMessage
from langchain_core.tools import StructuredTool
from langchain.agents import create_agent
from langgraph.checkpoint.memory import MemorySaver

from mcp_server.tool_registry import ToolRegistry


class AgentStatus(str, Enum):
    """Status of agent execution."""
    RUNNING = "running"
    COMPLETED = "completed"
    FAILED = "failed"
    MAX_ITERATIONS = "max_iterations"


@dataclass
class AgentConfig:
    """Configuration for autonomous agent."""
    max_iterations: int = 500  # High limit - agent should stop by saying it's done
    max_tool_errors: int = 3
    temperature: float = 0.2
    verbose: bool = True
    enable_checkpointing: bool = True
    thread_id: str = "default"


@dataclass
class AgentResult:
    """Result of agent execution."""
    status: AgentStatus
    final_response: str
    iterations: int
    tool_calls: List[Dict[str, Any]] = field(default_factory=list)
    error: Optional[str] = None
    elapsed_seconds: float = 0.0
    messages: List[Any] = field(default_factory=list)


@dataclass 
class VerificationResult:
    """Result of a verification phase."""
    success: bool
    feedback: str
    iterations: int
    issues_found: List[str] = field(default_factory=list)
    fixes_applied: List[str] = field(default_factory=list)


def _convert_mcp_tools_to_langchain(registry: ToolRegistry) -> List[StructuredTool]:
    """Convert MCP tool registry to LangChain StructuredTools."""
    from pydantic import BaseModel, Field, create_model
    
    tools = []
    
    for tool_schema in registry.list_tools():
        name = tool_schema["name"]
        description = tool_schema["description"]
        input_schema = tool_schema.get("inputSchema", {})
        handler = registry.get_tool(name)
        
        if handler is None:
            continue
        
        # Build Pydantic model from input schema for LangChain
        properties = input_schema.get("properties", {})
        required = input_schema.get("required", [])
        
        field_definitions = {}
        for prop_name, prop_info in properties.items():
            prop_type = prop_info.get("type", "string")
            prop_desc = prop_info.get("description", "")
            
            # Map JSON schema types to Python types
            type_map = {"string": str, "integer": int, "number": float, "boolean": bool, "object": dict, "array": list}
            python_type = type_map.get(prop_type, str)
            
            # Make optional fields have default None
            if prop_name in required:
                field_definitions[prop_name] = (python_type, Field(description=prop_desc))
            else:
                field_definitions[prop_name] = (Optional[python_type], Field(default=None, description=prop_desc))
        
        # Create dynamic Pydantic model for this tool's arguments
        if field_definitions:
            ArgsModel = create_model(f"{name}_args", **field_definitions)
        else:
            ArgsModel = None
        
        def make_tool_func(tool_name: str, tool_handler: Callable):
            def tool_func(**kwargs) -> str:
                try:
                    # Filter out None values for optional params
                    filtered_kwargs = {k: v for k, v in kwargs.items() if v is not None}
                    result = tool_handler(**filtered_kwargs)
                    if isinstance(result, dict) and "content" in result:
                        texts = []
                        for item in result["content"]:
                            if item.get("type") == "text":
                                texts.append(item.get("text", ""))
                        output = "\n".join(texts) if texts else str(result)
                    else:
                        output = str(result)
                    
                    # Summarize large outputs to prevent context overflow
                    if len(output) > 5000:
                        output = _summarize_tool_output(tool_name, output)
                    
                    return output
                except Exception as e:
                    return f"Error calling {tool_name}: {str(e)}"
            return tool_func
        
        # Create the tool with or without args schema
        if ArgsModel:
            tool = StructuredTool.from_function(
                func=make_tool_func(name, handler),
                name=name,
                description=description,
                args_schema=ArgsModel,
            )
        else:
            tool = StructuredTool.from_function(
                func=make_tool_func(name, handler),
                name=name,
                description=description,
            )
        tools.append(tool)
    
    return tools


def _summarize_tool_output(tool_name: str, output: str) -> str:
    """
    Summarize large tool outputs to prevent context overflow.
    Uses fast string-based methods, NOT LLM calls (which would be slow).
    """
    # Fast path for known tool types
    if tool_name == "get_semantic_tree":
        return _summarize_semantic_tree(output)
    elif tool_name == "get_screenshot":
        return _summarize_screenshot(output)
    elif tool_name == "web_search":
        return _summarize_search_results(output)
    
    # Default: fast truncation keeping first and last portions
    # This preserves context without slow LLM calls
    max_len = 4000
    if len(output) <= max_len:
        return output
    
    # Keep first 3000 chars and last 500 chars
    return f"{output[:3000]}\n\n[... {len(output) - 3500} chars truncated ...]\n\n{output[-500:]}"


def _summarize_semantic_tree(output: str) -> str:
    """Extract key interactive elements from semantic tree without losing button/control info."""
    import json
    
    try:
        # Try to parse as JSON first
        data = json.loads(output) if output.startswith('{') else None
        if data:
            elements = data.get("elements", [])
            summary_lines = [f"[Semantic Tree: {len(elements)} elements]"]
            
            # Group by control type
            buttons = []
            inputs = []
            other = []
            
            for elem in elements[:100]:  # Limit to first 100
                name = elem.get("name", "")
                control_type = elem.get("control_type", "")
                automation_id = elem.get("automation_id", "")
                
                info = f"{name}" + (f" [{automation_id}]" if automation_id else "")
                
                if "button" in control_type.lower():
                    buttons.append(info)
                elif "edit" in control_type.lower() or "input" in control_type.lower():
                    inputs.append(info)
                elif name:  # Only include named elements
                    other.append(f"{info} ({control_type})")
            
            if buttons:
                summary_lines.append(f"\nButtons ({len(buttons)}): {', '.join(buttons[:30])}")
            if inputs:
                summary_lines.append(f"\nInputs ({len(inputs)}): {', '.join(inputs[:10])}")
            if other:
                summary_lines.append(f"\nOther controls ({len(other)}): {', '.join(other[:20])}")
            
            return "\n".join(summary_lines)
    except:
        pass
    
    # Fallback for non-JSON or parse errors
    return f"[Semantic Tree]\n{output[:3000]}..."


def _summarize_screenshot(output: str) -> str:
    """Summarize screenshot data - keep element labels visible."""
    # Screenshots might be base64 encoded or have markers
    if len(output) > 10000:
        return f"[Screenshot captured - {len(output)} chars of image data]\nUse get_semantic_tree() for element details."
    return output


def _summarize_search_results(output: str) -> str:
    """Summarize web search results to key points."""
    if len(output) > 5000:
        # Keep first 3000 chars which likely has the key results
        return f"[Web Search Results]\n{output[:3000]}\n...[additional results truncated]..."
    return output


def _get_langchain_model(temperature: float = 0.2):
    """Get a LangChain chat model from environment."""
    import os
    provider = os.getenv("CASCADE_MODEL_PROVIDER", "openai").lower()
    model_name = os.getenv("CASCADE_MODEL_NAME", "gpt-4o")
    api_key = os.getenv("CASCADE_MODEL_API_KEY")
    
    if provider == "openai":
        from langchain_openai import ChatOpenAI
        return ChatOpenAI(model=model_name, api_key=api_key, temperature=temperature)
    elif provider == "anthropic":
        from langchain_anthropic import ChatAnthropic
        return ChatAnthropic(model=model_name, api_key=api_key, temperature=temperature)
    else:
        from langchain_openai import ChatOpenAI
        return ChatOpenAI(model=model_name, api_key=api_key, temperature=temperature)


class ReActVerifier:
    """
    ReAct-based verification loop for testing and iterating.
    
    Used by Explorer and Orchestrator to verify their outputs.
    Runs a ReAct loop that can test, identify issues, and fix them.
    """

    def __init__(
        self,
        tool_registry: ToolRegistry,
        system_prompt: str,
        max_iterations: int = 10,
        verbose: bool = True,
    ):
        self._registry = tool_registry
        self._system_prompt = system_prompt
        self._max_iterations = max_iterations
        self._verbose = verbose
        
        self._tools = _convert_mcp_tools_to_langchain(tool_registry)
        self._model = _get_langchain_model(temperature=0.1)
        self._checkpointer = MemorySaver()
        
        self._agent = create_agent(
            model=self._model,
            tools=self._tools,
            system_prompt=self._system_prompt,
            checkpointer=self._checkpointer,
        )

    def _log(self, msg: str) -> None:
        if self._verbose:
            print(f"[Verifier] {msg}")

    def verify(
        self,
        verification_task: str,
        context: Optional[Dict[str, Any]] = None,
        thread_id: str = "verify",
    ) -> VerificationResult:
        """
        Run verification loop until success or max iterations.
        
        Args:
            verification_task: Description of what to verify
            context: Additional context
            thread_id: Thread ID for state management
            
        Returns:
            VerificationResult with success status and feedback
        """
        import json
        
        user_content = verification_task
        if context:
            user_content += f"\n\nContext:\n{json.dumps(context, indent=2)}"
        
        config = {
            "configurable": {"thread_id": thread_id},
            "recursion_limit": self._max_iterations,
        }
        
        self._log(f"Starting verification: {verification_task[:80]}...")
        
        iterations = 0
        issues_found = []
        fixes_applied = []
        final_response = ""
        
        try:
            initial_state = {"messages": [HumanMessage(content=user_content)]}
            
            for event in self._agent.stream(initial_state, config, stream_mode="values"):
                iterations += 1
                messages = event.get("messages", [])
                
                for msg in messages:
                    if hasattr(msg, "tool_calls") and msg.tool_calls:
                        for tc in msg.tool_calls:
                            tool_name = tc.get("name", "")
                            self._log(f"  Tool: {tool_name}")
                    
                    if hasattr(msg, "content") and msg.content and msg.type == "ai":
                        final_response = msg.content
                        # Parse response for issues/fixes
                        if "issue" in final_response.lower() or "error" in final_response.lower():
                            issues_found.append(final_response[:200])
                        if "fixed" in final_response.lower() or "corrected" in final_response.lower():
                            fixes_applied.append(final_response[:200])
                
                if iterations >= self._max_iterations:
                    break
            
            # Determine success based on final response - be strict!
            # Check for explicit failure indicators FIRST
            failure_indicators = ["unable", "cannot", "failed", "error", "not working", 
                                  "issue", "problem", "invalid", "missing", "None"]
            has_failure = any(word in final_response.lower() for word in failure_indicators)
            
            # Only count as success if success words present AND no failure indicators
            success_indicators = ["verified", "all steps work", "successfully tested"]
            has_success = any(word in final_response.lower() for word in success_indicators)
            
            success = has_success and not has_failure
            
            self._log(f"Verification complete: {'SUCCESS' if success else 'NEEDS WORK'}")
            
            return VerificationResult(
                success=success,
                feedback=final_response,
                iterations=iterations,
                issues_found=issues_found,
                fixes_applied=fixes_applied,
            )
            
        except Exception as e:
            self._log(f"Verification error: {e}")
            return VerificationResult(
                success=False,
                feedback=str(e),
                iterations=iterations,
                issues_found=[str(e)],
            )


class AutonomousAgent:
    """
    Full ReAct-based autonomous agent using LangGraph.
    
    Used by Worker for pure ReAct execution. Can also be used
    for verification phases by Explorer and Orchestrator.
    """

    def __init__(
        self,
        tool_registry: ToolRegistry,
        system_prompt: str,
        config: Optional[AgentConfig] = None,
        on_tool_call: Optional[Callable[[str, Dict[str, Any]], None]] = None,
        on_tool_result: Optional[Callable[[str, Any], None]] = None,
    ):
        self._registry = tool_registry
        self._system_prompt = system_prompt
        self._config = config or AgentConfig()
        self._on_tool_call = on_tool_call
        self._on_tool_result = on_tool_result
        
        self._tools = _convert_mcp_tools_to_langchain(tool_registry)
        self._model = _get_langchain_model(self._config.temperature)
        self._checkpointer = MemorySaver() if self._config.enable_checkpointing else None
        
        self._agent = create_agent(
            model=self._model,
            tools=self._tools,
            system_prompt=self._system_prompt,
            checkpointer=self._checkpointer,
        )

    def _log(self, message: str) -> None:
        if self._config.verbose:
            print(f"[Agent] {message}")

    def run(self, task: str, context: Optional[Dict[str, Any]] = None) -> AgentResult:
        """Run the agent on a task until completion."""
        import json
        start_time = time.time()
        
        user_content = task
        if context:
            user_content += f"\n\nContext:\n{json.dumps(context, indent=2)}"
        
        config = {
            "configurable": {"thread_id": self._config.thread_id},
            "recursion_limit": self._config.max_iterations,
        }
        
        self._log(f"Starting task: {task[:100]}...")
        self._log(f"Available tools: {[t.name for t in self._tools]}")
        
        all_tool_calls = []
        final_response = ""
        iterations = 0
        
        try:
            current_messages = [HumanMessage(content=user_content)]
            
            # Track which messages we've already seen/logged to avoid duplicates
            seen_message_ids = set()
            
            while iterations < self._config.max_iterations:
                initial_state = {"messages": current_messages}
                should_continue = False
                
                for event in self._agent.stream(initial_state, config, stream_mode="values"):
                    iterations += 1
                    messages = event.get("messages", [])
                    
                    # Only process NEW messages (not seen before)
                    for msg in messages:
                        # Create a unique ID for this message
                        msg_id = id(msg)
                        if msg_id in seen_message_ids:
                            continue  # Skip already-logged messages
                        seen_message_ids.add(msg_id)
                        
                        # Log AI reasoning before tool calls
                        if hasattr(msg, "content") and msg.type == "ai":
                            if hasattr(msg, "tool_calls") and msg.tool_calls:
                                # This is reasoning before tool calls
                                if msg.content:  # Only log if there's actual reasoning
                                    self._log(f"\n{'='*50}")
                                    self._log(f"REASONING (iteration {iterations}):")
                                    self._log(f"{'='*50}")
                                    reasoning = msg.content[:1000] if len(msg.content) > 1000 else msg.content
                                    for line in reasoning.split('\n'):
                                        self._log(f"  {line}")
                                    self._log(f"{'='*50}")
                                
                                # Log tool calls
                                for tc in msg.tool_calls:
                                    tool_name = tc.get("name", "unknown")
                                    tool_args = tc.get("args", {})
                                    all_tool_calls.append({"name": tool_name, "arguments": tool_args})
                                    self._log(f"  → Calling: {tool_name}({str(tool_args)[:100]})")
                                    if self._on_tool_call:
                                        self._on_tool_call(tool_name, tool_args)
                            else:
                                # Response without tool calls - determine if we should continue or stop
                                final_response = msg.content
                                response_lower = final_response.lower() if final_response else ""
                                
                                # Detect EXPLICIT completion signals
                                completion_signals = [
                                    "all capabilities have been",
                                    "exploration complete",
                                    "all skill maps have been",
                                    "finished creating",
                                    "completed all",
                                    "all requested capabilities",
                                    "task complete",
                                    "mission complete",
                                    "successfully mapped all",
                                ]
                                is_complete = any(sig in response_lower for sig in completion_signals)
                                
                                # Detect REPLAN or unfinished work signals (should continue)
                                continue_signals = [
                                    "replan",
                                    "next step",
                                    "continue with",
                                    "moving to",
                                    "now i will",
                                    "next i need",
                                    "need to discover",
                                    "still need to",
                                    "remaining capabilities",
                                ]
                                wants_to_continue = any(sig in response_lower for sig in continue_signals)
                                
                                if is_complete:
                                    self._log(f"\n{'='*50}")
                                    self._log("EXPLORATION COMPLETE - Agent signaled completion")
                                    self._log(f"{'='*50}")
                                    should_continue = False
                                elif wants_to_continue:
                                    self._log(f"\n{'='*50}")
                                    self._log("CONTINUING - More work to do...")
                                    self._log(f"{'='*50}")
                                    should_continue = True
                                else:
                                    # Default: if there's no tool call but no explicit completion, assume work remains
                                    should_continue = True
                    
                    if iterations >= self._config.max_iterations:
                        break
                
                # Continue with a new invocation if not complete
                if should_continue and iterations < self._config.max_iterations:
                    # Add the current messages plus a continuation prompt
                    current_messages = messages + [
                        HumanMessage(content="Continue with your plan. What's next?")
                    ]
                else:
                    break
            
            # Generate a summary if we have tool calls
            if all_tool_calls:
                summary_parts = []
                summary_parts.append(f"Completed {len(all_tool_calls)} actions in {iterations} iterations.")
                
                # Count unique tools used
                tool_counts = {}
                for tc in all_tool_calls:
                    name = tc["name"]
                    tool_counts[name] = tool_counts.get(name, 0) + 1
                
                summary_parts.append(f"Tools used: {', '.join(f'{k}({v})' for k, v in tool_counts.items())}")
                
                # Append the final response if it contains useful info
                if final_response and len(final_response) < 500:
                    summary_parts.append(f"Status: {final_response[:200]}")
                
                summary = "\n".join(summary_parts)
            else:
                summary = final_response
            
            self._log(f"Completed: {summary[:100]}...")
            
            return AgentResult(
                status=AgentStatus.COMPLETED if final_response else AgentStatus.MAX_ITERATIONS,
                final_response=summary,
                iterations=iterations,
                tool_calls=all_tool_calls,
                elapsed_seconds=time.time() - start_time,
            )
            
        except Exception as e:
            self._log(f"Error: {e}")
            return AgentResult(
                status=AgentStatus.FAILED,
                final_response="",
                iterations=iterations,
                tool_calls=all_tool_calls,
                error=str(e),
                elapsed_seconds=time.time() - start_time,
            )

    def run_with_recovery(
        self,
        task: str,
        context: Optional[Dict[str, Any]] = None,
        max_retries: int = 2,
    ) -> AgentResult:
        """Run agent with automatic retry on failure."""
        last_result: Optional[AgentResult] = None
        
        for attempt in range(max_retries + 1):
            if attempt > 0:
                self._log(f"Retry attempt {attempt}/{max_retries}")
                self._config.thread_id = f"{self._config.thread_id}_retry_{attempt}"
            
            result = self.run(task, context)
            last_result = result
            
            if result.status == AgentStatus.COMPLETED:
                return result
            
            if result.error:
                context = context or {}
                context["previous_error"] = result.error
        
        return last_result or AgentResult(
            status=AgentStatus.FAILED,
            final_response="",
            iterations=0,
            error="All retry attempts failed",
        )

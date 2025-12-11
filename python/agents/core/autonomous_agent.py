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
from langgraph.prebuilt import create_react_agent
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
    max_iterations: int = 50
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
    tools = []
    
    for tool_schema in registry.list_tools():
        name = tool_schema["name"]
        description = tool_schema["description"]
        handler = registry.get_tool(name)
        
        if handler is None:
            continue
        
        def make_tool_func(tool_name: str, tool_handler: Callable):
            def tool_func(**kwargs) -> str:
                try:
                    result = tool_handler(**kwargs)
                    if isinstance(result, dict) and "content" in result:
                        texts = []
                        for item in result["content"]:
                            if item.get("type") == "text":
                                texts.append(item.get("text", ""))
                        return "\n".join(texts) if texts else str(result)
                    return str(result)
                except Exception as e:
                    return f"Error: {str(e)}"
            return tool_func
        
        tool = StructuredTool.from_function(
            func=make_tool_func(name, handler),
            name=name,
            description=description,
        )
        tools.append(tool)
    
    return tools


def _get_langchain_model(temperature: float = 0.2):
    """Get a LangChain chat model from environment."""
    import os
    provider = os.getenv("CASCADE_MODEL_PROVIDER", "openai").lower()
    model_name = os.getenv("CASCADE_MODEL_NAME", "gpt-4")
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
        
        self._agent = create_react_agent(
            model=self._model,
            tools=self._tools,
            state_modifier=self._system_prompt,
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
            
            # Determine success based on final response
            success = any(word in final_response.lower() for word in 
                         ["success", "verified", "working", "complete", "passed"])
            
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
        
        self._agent = create_react_agent(
            model=self._model,
            tools=self._tools,
            state_modifier=self._system_prompt,
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
            initial_state = {"messages": [HumanMessage(content=user_content)]}
            
            for event in self._agent.stream(initial_state, config, stream_mode="values"):
                iterations += 1
                messages = event.get("messages", [])
                
                for msg in messages:
                    if hasattr(msg, "tool_calls") and msg.tool_calls:
                        for tc in msg.tool_calls:
                            tool_name = tc.get("name", "unknown")
                            tool_args = tc.get("args", {})
                            all_tool_calls.append({"name": tool_name, "arguments": tool_args})
                            self._log(f"Tool: {tool_name}({str(tool_args)[:60]}...)")
                            if self._on_tool_call:
                                self._on_tool_call(tool_name, tool_args)
                    
                    if hasattr(msg, "content") and msg.content and msg.type == "ai":
                        if not hasattr(msg, "tool_calls") or not msg.tool_calls:
                            final_response = msg.content
                
                if iterations >= self._config.max_iterations:
                    break
            
            self._log(f"Completed: {final_response[:100]}...")
            
            return AgentResult(
                status=AgentStatus.COMPLETED if final_response else AgentStatus.MAX_ITERATIONS,
                final_response=final_response,
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

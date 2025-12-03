# Agent Runtime Specification

## Overview

The Agent Runtime module provides the infrastructure for loading, executing, and managing specialized agents created by the Builder Agent. All executions occur inside hidden Windows Virtual Desktop sessions so users can continue interacting with their desktops while agents run.

## Architecture

```
python/cascade_agent/runtime/
├── __init__.py
├── loader.py                   # Agent loading from database
├── executor.py                 # Execution management
├── context.py                  # Execution context
├── memory.py                   # Conversation memory
├── graph.py                    # Runtime graph definition
├── recovery.py                 # Error recovery strategies
├── nodes/
│   ├── __init__.py
│   ├── planning.py             # Task planning
│   ├── execution.py            # Action execution
│   ├── clarification.py        # User clarification
│   └── completion.py           # Task completion
├── tools/
│   ├── __init__.py
│   ├── dynamic_tools.py        # Dynamically loaded tools
│   └── base_tools.py           # Common tools
└── models/
    ├── __init__.py
    ├── loaded_agent.py
    ├── execution_context.py
    └── task_result.py
```

## Session Orchestration

- `RuntimeSessionManager` talks to the gRPC `SessionService` to acquire, heartbeat, and release hidden desktop sessions.
- Each execution receives a unique `SessionHandle` that is injected into the `ExecutionContext`, loaded tools, and downstream CodeGen scripts.
- Sessions can be pooled or per-request; the runtime refuses to run automation without an active session, ensuring the user’s desktop stays untouched.

## Agent Loading

### AgentLoader

```python
from dataclasses import dataclass
from typing import Optional
import importlib.util
import sys

@dataclass
class LoadedAgent:
    """Represents a loaded agent ready for execution."""
    id: str
    name: str
    description: str
    target_application: str
    capabilities: list[str]
    instruction_list: str
    scripts: list[dict]
    python_module: Optional[object]
    tools: list[callable]
    graph: Optional[StateGraph]
    metadata: dict
    session_profile: Optional[VirtualDesktopProfile] = None


class AgentLoader:
    """Loads agents from database and prepares them for execution."""
    
    def __init__(self, agent_client, codegen_client):
        self.agent_client = agent_client
        self.codegen_client = codegen_client
        self._loaded_agents: dict[str, LoadedAgent] = {}
    
    async def load_agent(self, agent_id: str = None, agent_name: str = None) -> LoadedAgent:
        """Load an agent by ID or name."""
        
        # Get agent definition from database
        agent_def = await self.agent_client.get_agent_definition(
            id=agent_id,
            name=agent_name
        )
        
        if not agent_def["result"]["success"]:
            raise AgentNotFoundError(f"Agent not found: {agent_id or agent_name}")
        
        # Compile scripts if needed
        compiled_scripts = await self._compile_scripts(agent_def["scripts"])
        
        # Create dynamic tools from scripts
        tools = await self._create_tools(compiled_scripts)
        
        # Load Python agent module if available
        python_module = await self._load_python_module(agent_def)
        
        # Create execution graph
        graph = self._create_execution_graph(agent_def, tools)
        
        loaded_agent = LoadedAgent(
            id=agent_def["agent"]["id"],
            name=agent_def["agent"]["name"],
            description=agent_def["agent"]["description"],
            target_application=agent_def["agent"]["target_application"],
            capabilities=agent_def["capabilities"],
            instruction_list=agent_def["instruction_list"],
            scripts=compiled_scripts,
            python_module=python_module,
            tools=tools,
            graph=graph,
            metadata=agent_def["agent"].get("metadata", {}),
            session_profile=agent_def["agent"].get("session_profile")
        )
        
        self._loaded_agents[loaded_agent.id] = loaded_agent
        return loaded_agent
    
    async def _compile_scripts(self, scripts: list[dict]) -> list[dict]:
        """Compile all scripts for the agent."""
        compiled = []
        
        for script in scripts:
            # Check if already compiled
            cached = await self.codegen_client.get_compiled_assembly(
                script["id"], 
                script.get("version", "latest")
            )
            
            if cached:
                compiled.append({**script, "assembly": cached})
                continue
            
            # Compile script
            result = await self.codegen_client.compile(script["source_code"])
            
            if not result["compilation_success"]:
                raise CompilationError(
                    f"Failed to compile {script['name']}: {result['errors']}"
                )
            
            # Cache compiled assembly
            await self.codegen_client.save_compiled_assembly(
                script["id"],
                script.get("version", "latest"),
                result["assembly_bytes"]
            )
            
            compiled.append({**script, "assembly": result["assembly_bytes"]})
        
        return compiled
    
    async def _create_tools(self, scripts: list[dict]) -> list[callable]:
        """Create LangChain tools from compiled scripts."""
        tools = []
        
        for script in scripts:
            tool = self._script_to_tool(script)
            tools.append(tool)
        
        return tools
    
    def _script_to_tool(self, script: dict) -> callable:
        """Convert a compiled script to a LangChain tool."""
        
        @tool(name=script["name"], description=script.get("description", ""))
        async def dynamic_tool(**kwargs):
            result = await self.codegen_client.execute(
                script_id=script["id"],
                type_name=script.get("type_name", script["name"]),
                method_name=script.get("method_name", "ExecuteAsync"),
                variables=kwargs
            )
            
            if not result["execution_success"]:
                raise ToolExecutionError(result["exception_message"])
            
            return result["return_value"]
        
        return dynamic_tool
    
    def _create_execution_graph(self, agent_def: dict, tools: list) -> StateGraph:
        """Create the LangGraph execution graph for the agent."""
        from langgraph.graph import StateGraph, END
        from langgraph.prebuilt import ToolNode
        
        workflow = StateGraph(RuntimeState)
        
        workflow.add_node("understand", understand_task_node)
        workflow.add_node("plan", plan_execution_node)
        workflow.add_node("execute", execute_step_node)
        workflow.add_node("tools", ToolNode(tools=tools))
        workflow.add_node("evaluate", evaluate_result_node)
        workflow.add_node("clarify", clarify_with_user_node)
        workflow.add_node("complete", complete_task_node)
        
        workflow.set_entry_point("understand")
        
        workflow.add_conditional_edges(
            "understand",
            check_understanding,
            {"clear": "plan", "unclear": "clarify"}
        )
        
        workflow.add_edge("clarify", "understand")
        workflow.add_edge("plan", "execute")
        
        workflow.add_conditional_edges(
            "execute",
            check_execution_result,
            {"tool_call": "tools", "evaluate": "evaluate", "error": "evaluate"}
        )
        
        workflow.add_edge("tools", "execute")
        
        workflow.add_conditional_edges(
            "evaluate",
            check_task_completion,
            {"complete": "complete", "continue": "execute", "replan": "plan", "clarify": "clarify"}
        )
        
        workflow.add_edge("complete", END)
        
        return workflow.compile()
    
    def get_loaded_agent(self, agent_id: str) -> Optional[LoadedAgent]:
        """Get a previously loaded agent."""
        return self._loaded_agents.get(agent_id)
    
    async def unload_agent(self, agent_id: str):
        """Unload an agent and free resources."""
        if agent_id in self._loaded_agents:
            del self._loaded_agents[agent_id]
```

## Execution Context

```python
from dataclasses import dataclass, field
from typing import Any, Optional
from datetime import datetime
import uuid

@dataclass
class ExecutionContext:
    """Context for agent execution."""
    
    # Identity
    execution_id: str = field(default_factory=lambda: str(uuid.uuid4()))
    agent_id: str = ""
    user_id: Optional[str] = None
    session_id: Optional[str] = None
    session_handle: Optional[SessionHandle] = None
    
    # Task
    task: str = ""
    task_parameters: dict = field(default_factory=dict)
    
    # State
    current_step: int = 0
    total_steps: int = 0
    status: str = "pending"  # pending, running, completed, failed, cancelled
    
    # Results
    result: Optional[Any] = None
    error: Optional[str] = None
    
    # Timing
    started_at: Optional[datetime] = None
    completed_at: Optional[datetime] = None
    
    # Logging
    logs: list[str] = field(default_factory=list)
    screenshots: list[bytes] = field(default_factory=list)
    
    # Recovery
    retry_count: int = 0
    max_retries: int = 3
    last_checkpoint: Optional[dict] = None
    
    def log(self, message: str):
        """Add a log entry."""
        timestamp = datetime.now().isoformat()
        self.logs.append(f"[{timestamp}] {message}")
    
    def add_screenshot(self, screenshot: bytes):
        """Add a screenshot to the execution log."""
        self.screenshots.append(screenshot)
    
    def checkpoint(self, state: dict):
        """Save a checkpoint for recovery."""
        self.last_checkpoint = {
            "step": self.current_step,
            "state": state,
            "timestamp": datetime.now().isoformat()
        }
    
    def to_dict(self) -> dict:
        """Convert to dictionary for serialization."""
        return {
            "execution_id": self.execution_id,
            "agent_id": self.agent_id,
            "task": self.task,
            "status": self.status,
            "current_step": self.current_step,
            "total_steps": self.total_steps,
            "result": self.result,
            "error": self.error,
            "started_at": self.started_at.isoformat() if self.started_at else None,
            "completed_at": self.completed_at.isoformat() if self.completed_at else None,
            "retry_count": self.retry_count
        }
```

## Runtime State

```python
from typing import TypedDict, Annotated, Optional
from langgraph.graph.message import add_messages

class RuntimeState(TypedDict):
    """State for agent runtime execution."""
    
    # Messages (conversation history)
    messages: Annotated[list, add_messages]
    
    # Task information
    task: str
    task_understanding: Optional[dict]
    is_task_clear: bool
    clarification_needed: Optional[str]
    
    # Planning
    execution_plan: Optional[list[dict]]
    current_step_index: int
    
    # Execution
    current_action: Optional[str]
    action_parameters: Optional[dict]
    last_result: Optional[dict]
    accumulated_results: list[dict]
    
    # Agent context
    agent: LoadedAgent
    context: ExecutionContext
    session_handle: Optional[SessionHandle]
    session_state: str
    
    # Error handling
    error: Optional[str]
    error_count: int
    recovery_strategy: Optional[str]
    
    # Completion
    is_complete: bool
    final_result: Optional[dict]
    summary: Optional[str]
```

## Executor

```python
class AgentExecutor:
    """Executes loaded agents with task management."""
    
    def __init__(self, loader: AgentLoader, llm_provider, session_client):
        self.loader = loader
        self.llm = llm_provider
        self.session_client = session_client
        self._active_executions: dict[str, ExecutionContext] = {}
    
    async def execute(
        self,
        agent_id: str,
        task: str,
        parameters: dict = None,
        user_id: str = None,
        session_id: str = None
    ) -> ExecutionResult:
        """Execute a task with the specified agent."""
        
        # Load agent if not already loaded
        agent = self.loader.get_loaded_agent(agent_id)
        if not agent:
            agent = await self.loader.load_agent(agent_id=agent_id)
        
        # Create execution context
        context = ExecutionContext(
            agent_id=agent_id,
            user_id=user_id,
            session_id=session_id,
            task=task,
            task_parameters=parameters or {}
        )
        
        self._active_executions[context.execution_id] = context
        acquired_session = None
        
        try:
            session_handle = await self._ensure_session(agent, session_id)
            acquired_session = session_handle if session_id is None else None
            context.session_id = session_handle.session_id
            context.session_handle = session_handle
            
            # Initialize state
            initial_state = RuntimeState(
                messages=[HumanMessage(content=task)],
                task=task,
                task_understanding=None,
                is_task_clear=False,
                clarification_needed=None,
                execution_plan=None,
                current_step_index=0,
                current_action=None,
                action_parameters=None,
                last_result=None,
                accumulated_results=[],
                agent=agent,
                context=context,
                session_handle=session_handle,
                session_state="active",
                error=None,
                error_count=0,
                recovery_strategy=None,
                is_complete=False,
                final_result=None,
                summary=None
            )
            
            # Update context
            context.status = "running"
            context.started_at = datetime.now()
            context.log(f"Starting task: {task}")
            
            # Run the graph
            final_state = await agent.graph.ainvoke(initial_state)
            
            # Update context with results
            context.status = "completed" if final_state["is_complete"] else "failed"
            context.completed_at = datetime.now()
            context.result = final_state["final_result"]
            
            # Record execution
            await self._record_execution(context, final_state)
            
            return ExecutionResult(
                success=final_state["is_complete"],
                result=final_state["final_result"],
                summary=final_state["summary"],
                execution_time=(context.completed_at - context.started_at).total_seconds(),
                logs=context.logs
            )
            
        except Exception as e:
            context.status = "failed"
            context.error = str(e)
            context.completed_at = datetime.now()
            context.log(f"Execution failed: {e}")
            
            return ExecutionResult(
                success=False,
                result=None,
                summary=f"Task failed: {e}",
                execution_time=(context.completed_at - context.started_at).total_seconds(),
                logs=context.logs,
                error=str(e)
            )
        
        finally:
            del self._active_executions[context.execution_id]
            if acquired_session:
                await self.session_client.release_session(acquired_session.session_id, reason="runtime_complete")
    
    async def _record_execution(self, context: ExecutionContext, state: RuntimeState):
        """Record execution history to database."""
        await self.loader.agent_client.record_execution(
            agent_id=context.agent_id,
            task_description=context.task,
            success=state["is_complete"],
            error_message=state.get("error"),
            duration_ms=int((context.completed_at - context.started_at).total_seconds() * 1000),
            steps=[
                {
                    "order": i,
                    "action": r.get("action"),
                    "success": r.get("success", True),
                    "duration_ms": r.get("duration_ms", 0)
                }
                for i, r in enumerate(state["accumulated_results"])
            ]
        )
    
    async def cancel(self, execution_id: str):
        """Cancel an active execution."""
        if execution_id in self._active_executions:
            context = self._active_executions[execution_id]
            context.status = "cancelled"
            context.log("Execution cancelled by user")
    
    def get_execution_status(self, execution_id: str) -> Optional[dict]:
        """Get the status of an execution."""
        if execution_id in self._active_executions:
            return self._active_executions[execution_id].to_dict()
        return None

    async def _ensure_session(self, agent: LoadedAgent, session_id: Optional[str]) -> SessionHandle:
        if session_id:
            return await self.session_client.attach_session(session_id)
        
        profile = agent.session_profile or VirtualDesktopProfile(width=1920, height=1080, dpi=100, enable_gpu=False)
        response = await self.session_client.create_session(agent_id=agent.id, profile=profile)
        if not response.result.success:
            raise RuntimeError(f"Unable to create session: {response.result.error_message}")
        return response.session


@dataclass
class ExecutionResult:
    """Result of agent execution."""
    success: bool
    result: Optional[Any]
    summary: str
    execution_time: float
    logs: list[str]
    error: Optional[str] = None
```

## Runtime Nodes

```python
async def understand_task_node(state: RuntimeState) -> dict:
    """Understand the user's task request."""
    
    agent = state["agent"]
    task = state["task"]
    
    prompt = UNDERSTAND_TASK_PROMPT.format(
        task=task,
        agent_name=agent.name,
        capabilities=agent.capabilities,
        instructions=agent.instruction_list
    )
    
    response = await llm.ainvoke(prompt)
    understanding = parse_understanding(response)
    
    is_clear = understanding.get("is_clear", False)
    clarification = understanding.get("clarification_needed")
    
    state["context"].log(f"Task understanding: {'clear' if is_clear else 'unclear'}")
    
    return {
        "task_understanding": understanding,
        "is_task_clear": is_clear,
        "clarification_needed": clarification,
        "messages": [AIMessage(content=understanding.get("interpretation", ""))]
    }


async def plan_execution_node(state: RuntimeState) -> dict:
    """Create an execution plan for the task."""
    
    agent = state["agent"]
    understanding = state["task_understanding"]
    
    prompt = PLAN_EXECUTION_PROMPT.format(
        task=state["task"],
        understanding=understanding,
        available_tools=[t.name for t in agent.tools],
        capabilities=agent.capabilities
    )
    
    response = await llm.ainvoke(prompt)
    plan = parse_plan(response)
    
    state["context"].log(f"Created plan with {len(plan)} steps")
    state["context"].total_steps = len(plan)
    
    return {
        "execution_plan": plan,
        "current_step_index": 0,
        "messages": [AIMessage(content=f"Plan created: {len(plan)} steps")]
    }


async def execute_step_node(state: RuntimeState) -> dict:
    """Execute the current step in the plan."""
    
    plan = state["execution_plan"]
    step_index = state["current_step_index"]
    agent = state["agent"]
    
    if step_index >= len(plan):
        return {"is_complete": True}
    
    current_step = plan[step_index]
    state["context"].log(f"Executing step {step_index + 1}: {current_step['action']}")
    state["context"].current_step = step_index + 1
    
    # Find the tool for this step
    tool_name = current_step["tool"]
    tool = next((t for t in agent.tools if t.name == tool_name), None)
    
    if not tool:
        return {
            "error": f"Tool not found: {tool_name}",
            "error_count": state["error_count"] + 1
        }
    
    return {
        "current_action": tool_name,
        "action_parameters": current_step.get("parameters", {}),
        "messages": [AIMessage(content=f"Executing: {current_step['action']}")]
    }


async def evaluate_result_node(state: RuntimeState) -> dict:
    """Evaluate the result of the last action."""
    
    last_result = state["last_result"]
    plan = state["execution_plan"]
    step_index = state["current_step_index"]
    
    # Check for errors
    if state.get("error"):
        error_count = state["error_count"]
        
        if error_count >= 3:
            return {
                "is_complete": False,
                "final_result": None,
                "summary": f"Task failed after {error_count} errors: {state['error']}"
            }
        
        # Determine recovery strategy
        strategy = determine_recovery_strategy(state)
        return {
            "recovery_strategy": strategy,
            "messages": [AIMessage(content=f"Error occurred, trying recovery: {strategy}")]
        }
    
    # Record result
    accumulated = state["accumulated_results"] + [{
        "step": step_index,
        "action": state["current_action"],
        "result": last_result,
        "success": True
    }]
    
    # Check if plan is complete
    if step_index + 1 >= len(plan):
        return {
            "accumulated_results": accumulated,
            "is_complete": True,
            "current_step_index": step_index + 1
        }
    
    return {
        "accumulated_results": accumulated,
        "current_step_index": step_index + 1
    }


async def complete_task_node(state: RuntimeState) -> dict:
    """Complete the task and generate summary."""
    
    accumulated = state["accumulated_results"]
    task = state["task"]
    
    prompt = SUMMARIZE_COMPLETION_PROMPT.format(
        task=task,
        results=accumulated
    )
    
    response = await llm.ainvoke(prompt)
    summary = extract_summary(response)
    
    state["context"].log(f"Task completed: {summary[:100]}...")
    
    return {
        "final_result": accumulated[-1]["result"] if accumulated else None,
        "summary": summary,
        "messages": [AIMessage(content=summary)]
    }


async def clarify_with_user_node(state: RuntimeState) -> dict:
    """Request clarification from the user."""
    
    clarification_needed = state["clarification_needed"]
    
    return {
        "messages": [AIMessage(content=f"I need some clarification: {clarification_needed}")]
    }
```

## Session Safety & User Concurrency

- Runtime pauses automation if the session host detects the user has taken manual control, ensuring no fighting over windows.
- Heartbeats are sent every 5 seconds; missing three heartbeats triggers automatic retries with a new session while preserving state checkpoints.
- When the specialized agent finishes, the runtime releases the session so the user can immediately continue working in the foreground application.

## Conversation Memory

```python
from typing import Optional
from datetime import datetime

class ConversationMemory:
    """Manages conversation history and context."""
    
    def __init__(self, max_messages: int = 100):
        self.max_messages = max_messages
        self._sessions: dict[str, list[dict]] = {}
    
    def create_session(self, session_id: str, agent_id: str) -> str:
        """Create a new conversation session."""
        self._sessions[session_id] = {
            "agent_id": agent_id,
            "created_at": datetime.now().isoformat(),
            "messages": [],
            "context": {}
        }
        return session_id
    
    def add_message(self, session_id: str, role: str, content: str, metadata: dict = None):
        """Add a message to the session."""
        if session_id not in self._sessions:
            raise ValueError(f"Session not found: {session_id}")
        
        message = {
            "role": role,
            "content": content,
            "timestamp": datetime.now().isoformat(),
            "metadata": metadata or {}
        }
        
        self._sessions[session_id]["messages"].append(message)
        
        # Trim if necessary
        if len(self._sessions[session_id]["messages"]) > self.max_messages:
            self._sessions[session_id]["messages"] = \
                self._sessions[session_id]["messages"][-self.max_messages:]
    
    def get_messages(self, session_id: str, limit: int = None) -> list[dict]:
        """Get messages from a session."""
        if session_id not in self._sessions:
            return []
        
        messages = self._sessions[session_id]["messages"]
        if limit:
            messages = messages[-limit:]
        return messages
    
    def get_context(self, session_id: str) -> dict:
        """Get accumulated context for a session."""
        if session_id not in self._sessions:
            return {}
        return self._sessions[session_id].get("context", {})
    
    def update_context(self, session_id: str, key: str, value: any):
        """Update session context."""
        if session_id in self._sessions:
            self._sessions[session_id]["context"][key] = value
    
    def clear_session(self, session_id: str):
        """Clear a session's history."""
        if session_id in self._sessions:
            self._sessions[session_id]["messages"] = []
            self._sessions[session_id]["context"] = {}
    
    def delete_session(self, session_id: str):
        """Delete a session entirely."""
        if session_id in self._sessions:
            del self._sessions[session_id]
```

## Error Recovery

```python
from enum import Enum
from typing import Optional

class RecoveryStrategy(Enum):
    RETRY = "retry"
    SKIP = "skip"
    REPLAN = "replan"
    FALLBACK = "fallback"
    ABORT = "abort"


class ErrorRecovery:
    """Handles error recovery during agent execution."""
    
    def __init__(self, max_retries: int = 3):
        self.max_retries = max_retries
    
    def determine_strategy(self, state: RuntimeState) -> RecoveryStrategy:
        """Determine the best recovery strategy for an error."""
        
        error = state.get("error", "")
        error_count = state.get("error_count", 0)
        
        # Check if we've exceeded retry limit
        if error_count >= self.max_retries:
            return RecoveryStrategy.ABORT
        
        # Element not found - try alternative locator
        if "element not found" in error.lower():
            return RecoveryStrategy.RETRY
        
        # Timeout - retry with longer timeout
        if "timeout" in error.lower():
            return RecoveryStrategy.RETRY
        
        # Window changed - replan
        if "window" in error.lower():
            return RecoveryStrategy.REPLAN
        
        # Unknown error - try fallback
        return RecoveryStrategy.FALLBACK
    
    async def apply_strategy(
        self, 
        strategy: RecoveryStrategy, 
        state: RuntimeState
    ) -> dict:
        """Apply the recovery strategy."""
        
        if strategy == RecoveryStrategy.RETRY:
            return await self._retry(state)
        elif strategy == RecoveryStrategy.SKIP:
            return await self._skip(state)
        elif strategy == RecoveryStrategy.REPLAN:
            return await self._replan(state)
        elif strategy == RecoveryStrategy.FALLBACK:
            return await self._fallback(state)
        else:
            return {"is_complete": False, "error": state["error"]}
    
    async def _retry(self, state: RuntimeState) -> dict:
        """Retry the current action."""
        return {
            "error": None,
            "messages": [AIMessage(content="Retrying action...")]
        }
    
    async def _skip(self, state: RuntimeState) -> dict:
        """Skip the current step and continue."""
        return {
            "error": None,
            "current_step_index": state["current_step_index"] + 1,
            "messages": [AIMessage(content="Skipping failed step, continuing...")]
        }
    
    async def _replan(self, state: RuntimeState) -> dict:
        """Create a new plan from current state."""
        return {
            "error": None,
            "execution_plan": None,  # Will trigger replanning
            "messages": [AIMessage(content="Replanning execution...")]
        }
    
    async def _fallback(self, state: RuntimeState) -> dict:
        """Try a fallback approach."""
        # Try OCR-based element finding as fallback
        return {
            "error": None,
            "recovery_strategy": "ocr_fallback",
            "messages": [AIMessage(content="Trying fallback approach...")]
        }
```

## Runtime Prompts

```python
UNDERSTAND_TASK_PROMPT = """
You are {agent_name}, an AI agent specialized in automating tasks for a specific application.

User's Request: {task}

Your Capabilities:
{capabilities}

Instructions:
{instructions}

Analyze the user's request and determine:
1. What specific actions are being requested
2. Whether the request is within your capabilities
3. Any ambiguities that need clarification
4. The expected outcome

Output as JSON:
{{
    "is_clear": true/false,
    "interpretation": "Your understanding of the task",
    "required_capabilities": ["list", "of", "capabilities"],
    "clarification_needed": "Question if unclear, null otherwise",
    "expected_outcome": "What the user expects to achieve"
}}
"""

PLAN_EXECUTION_PROMPT = """
Create an execution plan for the following task.

Task: {task}
Understanding: {understanding}

Available Tools:
{available_tools}

Agent Capabilities:
{capabilities}

Create a step-by-step plan where each step specifies:
1. The action to take
2. The tool to use
3. Parameters for the tool
4. Expected result
5. How to verify success

Output as JSON array of steps.
"""

SUMMARIZE_COMPLETION_PROMPT = """
Summarize the completed task for the user.

Original Task: {task}

Execution Results:
{results}

Provide a clear, user-friendly summary of:
1. What was accomplished
2. Any notable observations
3. The final outcome
"""
```



"""
Agent executor for running loaded agents.
"""

from dataclasses import dataclass, field
from datetime import datetime
from typing import Any, Optional
import uuid

from cascade_agent.runtime.loader import AgentLoader, LoadedAgent


@dataclass
class ExecutionContext:
    """Context for agent execution."""
    execution_id: str = field(default_factory=lambda: str(uuid.uuid4()))
    agent_id: str = ""
    user_id: Optional[str] = None
    session_id: Optional[str] = None
    
    task: str = ""
    task_parameters: dict = field(default_factory=dict)
    
    current_step: int = 0
    total_steps: int = 0
    status: str = "pending"
    
    result: Optional[Any] = None
    error: Optional[str] = None
    
    started_at: Optional[datetime] = None
    completed_at: Optional[datetime] = None
    
    logs: list[str] = field(default_factory=list)
    retry_count: int = 0
    max_retries: int = 3
    
    def log(self, message: str) -> None:
        """Add a log entry."""
        timestamp = datetime.now().isoformat()
        self.logs.append(f"[{timestamp}] {message}")
    
    def to_dict(self) -> dict:
        """Convert to dictionary."""
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


@dataclass
class ExecutionResult:
    """Result of agent execution."""
    success: bool
    result: Optional[Any]
    summary: str
    execution_time: float
    logs: list[str]
    error: Optional[str] = None


class AgentExecutor:
    """Executes loaded agents with task management."""
    
    def __init__(self, loader: AgentLoader, llm_client=None):
        """
        Initialize the executor.
        
        Args:
            loader: AgentLoader instance
            llm_client: LLM client for agent reasoning
        """
        self.loader = loader
        self.llm = llm_client
        self._active_executions: dict[str, ExecutionContext] = {}
    
    async def execute(
        self,
        agent_id: str,
        task: str,
        parameters: dict = None,
        user_id: str = None,
        session_id: str = None
    ) -> ExecutionResult:
        """
        Execute a task with the specified agent.
        
        Args:
            agent_id: ID of the agent to use
            task: Task description in natural language
            parameters: Optional task parameters
            user_id: Optional user identifier
            session_id: Optional session identifier
        
        Returns:
            ExecutionResult with task outcome
        """
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
        
        try:
            context.status = "running"
            context.started_at = datetime.now()
            context.log(f"Starting task: {task}")
            
            # Execute agent graph
            result = await self._execute_agent(agent, context)
            
            context.status = "completed" if result.success else "failed"
            context.completed_at = datetime.now()
            context.result = result.result
            
            return result
            
        except Exception as e:
            context.status = "failed"
            context.error = str(e)
            context.completed_at = datetime.now()
            context.log(f"Execution failed: {e}")
            
            return ExecutionResult(
                success=False,
                result=None,
                summary=f"Task failed: {e}",
                execution_time=(
                    context.completed_at - context.started_at
                ).total_seconds(),
                logs=context.logs,
                error=str(e)
            )
        
        finally:
            del self._active_executions[context.execution_id]
    
    async def _execute_agent(
        self,
        agent: LoadedAgent,
        context: ExecutionContext
    ) -> ExecutionResult:
        """Execute the agent's graph."""
        if not agent.graph:
            raise RuntimeError("Agent has no execution graph")
        
        # Create initial state
        initial_state = {
            "messages": [],
            "task": context.task,
            "agent": agent,
            "context": context
        }
        
        # Run the graph
        final_state = await agent.graph.ainvoke(initial_state)
        
        execution_time = (
            datetime.now() - context.started_at
        ).total_seconds()
        
        return ExecutionResult(
            success=final_state.get("is_complete", False),
            result=final_state.get("final_result"),
            summary=final_state.get("summary", ""),
            execution_time=execution_time,
            logs=context.logs
        )
    
    async def cancel(self, execution_id: str) -> None:
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



"""Autonomous Worker agent using pure ReAct pattern."""

from __future__ import annotations

import json
import uuid
from typing import Any, Dict, Optional

from cascade_client.auth.context import CascadeContext
from cascade_client.grpc_client import CascadeGrpcClient

from mcp_server.tool_registry import ToolRegistry
from mcp_server.body_tools import register_body_tools
from mcp_server.explorer_tools import register_explorer_tools

from agents.core.autonomous_agent import AutonomousAgent, AgentConfig, AgentResult
from agents.core.verify_prompts import WORKER_SYSTEM_PROMPT, get_worker_task


class AutonomousWorker:
    """
    Worker agent using pure ReAct pattern.
    
    Unlike Explorer/Orchestrator, Worker uses pure ReAct because:
    - Tasks are usually short and focused
    - Dynamic UI state requires reactive decision-making
    - No complex planning needed for single tasks
    """

    def __init__(
        self,
        context: CascadeContext,
        grpc_client: CascadeGrpcClient,
        config: Optional[AgentConfig] = None,
    ):
        self._context = context
        self._grpc = grpc_client
        self._config = config or AgentConfig(
            max_iterations=30,
            verbose=True,
            thread_id=f"worker_{uuid.uuid4().hex[:8]}",
        )
        
        # Setup MCP tool registry
        self._registry = ToolRegistry()
        register_body_tools(self._registry, grpc_client)
        register_explorer_tools(self._registry)
        self._register_skill_tools()

    def _register_skill_tools(self) -> None:
        """Register skill execution tools from available skill maps."""
        try:
            from storage.firestore_client import FirestoreClient
            from agents.explorer.skill_map import SkillMap
            
            fs = FirestoreClient(self._context)
            skill_maps = fs.list_skill_maps()
            
            for skill_id, data in skill_maps.items():
                try:
                    skill = SkillMap.model_validate(data)
                    self._register_single_skill(skill)
                except Exception:
                    continue
                    
            print(f"[Worker] Registered {len(skill_maps)} skill tools")
            
        except Exception as e:
            print(f"[Worker] Could not load skills: {e}")

    def _register_single_skill(self, skill) -> None:
        """Register a single skill as a tool."""
        skill_id = skill.metadata.skill_id
        tool_name = f"execute_skill_{skill_id}"
        description = skill.metadata.description or skill.metadata.capability or f"Execute {skill_id}"
        
        # Capture grpc_client in closure
        grpc = self._grpc
        
        def make_handler(skill_map, grpc_client):
            def handler() -> Dict[str, Any]:
                from agents.worker.graph import StepExecutor
                
                skill_id = skill_map.metadata.skill_id
                print(f"[Worker] Executing skill: {skill_id}")
                print(f"[Worker]   Steps: {len(skill_map.steps)}")
                
                try:
                    executor = StepExecutor(grpc_client, dry_run=False)
                    statuses = executor.execute_skill(skill_map)
                    success = all(st.success for st in statuses) if statuses else False
                    
                    print(f"[Worker]   Result: {'SUCCESS' if success else 'FAILED'}")
                    for st in statuses:
                        print(f"[Worker]     Step {st.step_index}: {st.action} - {st.message}")
                    
                    return {
                        "content": [{
                            "type": "text",
                            "text": json.dumps({
                                "success": success,
                                "skill_id": skill_id,
                                "statuses": [st.model_dump() for st in statuses],
                            })
                        }]
                    }
                except Exception as e:
                    import traceback
                    print(f"[Worker]   ERROR: {e}")
                    traceback.print_exc()
                    return {
                        "content": [{"type": "text", "text": f"Error: {str(e)}"}],
                        "isError": True,
                    }
            return handler
        
        self._registry.register_tool(
            name=tool_name,
            description=description,
            input_schema={"type": "object", "properties": {}},
            handler=make_handler(skill, grpc),
        )

    def execute(
        self,
        task: str,
        app_name: Optional[str] = None,
        additional_context: str = "",
    ) -> AgentResult:
        """
        Execute a task using pure ReAct.
        
        Args:
            task: The task to execute
            app_name: Optional application name
            additional_context: Optional context
            
        Returns:
            AgentResult with execution outcome
        """
        task_prompt = get_worker_task(
            task=task,
            app_name=app_name or "",
            additional_context=additional_context,
        )
        
        context = {
            "task": task,
            "app_name": app_name,
            "user_id": self._context.user_id,
        }
        
        agent = AutonomousAgent(
            tool_registry=self._registry,
            system_prompt=WORKER_SYSTEM_PROMPT,
            config=self._config,
        )
        
        print(f"[Worker] Executing task: {task[:80]}...")
        print(f"[Worker] Using pure ReAct pattern")
        
        result = agent.run_with_recovery(task_prompt, context, max_retries=1)
        
        print(f"[Worker] Completed: {result.status}")
        return result


def build_autonomous_worker(
    context: CascadeContext,
    grpc_client: CascadeGrpcClient,
) -> AutonomousWorker:
    """Factory function to build worker."""
    return AutonomousWorker(context, grpc_client)

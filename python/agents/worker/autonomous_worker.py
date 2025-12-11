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
        
        def make_handler(skill_map):
            def handler() -> Dict[str, Any]:
                # Execute skill steps
                results = []
                for step in skill_map.steps:
                    action = getattr(step, 'action', 'unknown')
                    selector = getattr(step, 'selector', None)
                    results.append(f"{action}: {selector}")
                return {
                    "content": [{
                        "type": "text",
                        "text": json.dumps({
                            "success": True,
                            "skill_id": skill_map.metadata.skill_id,
                            "steps": results,
                        })
                    }]
                }
            return handler
        
        self._registry.register_tool(
            name=tool_name,
            description=description,
            input_schema={"type": "object", "properties": {}},
            handler=make_handler(skill),
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

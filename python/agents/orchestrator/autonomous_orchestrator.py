"""Autonomous Orchestrator: LLM-driven coordination using tools."""

from __future__ import annotations

import json
import uuid
from typing import Any, Dict, List, Optional

from cascade_client.auth.context import CascadeContext
from cascade_client.grpc_client import CascadeGrpcClient

from mcp_server.tool_registry import ToolRegistry
from mcp_server.body_tools import register_body_tools
from mcp_server.explorer_tools import register_explorer_tools

from agents.core.autonomous_agent import (
    AgentConfig, AgentResult, AgentStatus, AutonomousAgent
)
from agents.core.planning_agent import PlanningAgent, get_user_plan_approval
from .prompts_autonomous import ORCHESTRATOR_SYSTEM_PROMPT, get_orchestrator_task


class AutonomousOrchestrator:
    """
    Autonomous Orchestrator that uses LLM-driven coordination.
    
    Architecture:
    - Pure agentic loop where LLM uses tools to coordinate
    - Can run Explorer to learn apps (via run_explorer tool)
    - Can run Worker to execute tasks (via run_worker tool)
    - Uses vision and semantic tree to monitor progress
    - LLM reasons about what to do and when
    
    No programmatic planning - the LLM does ALL the reasoning.
    """

    def __init__(
        self,
        context: CascadeContext,
        grpc_client: CascadeGrpcClient,
        config: Optional[Any] = None,  # Accept config for CLI compatibility
        max_iterations: int = 50,
        max_explorer_iterations: int = 50,
        max_worker_iterations: int = 30,
        verbose: bool = True,
    ):
        self._context = context
        self._grpc = grpc_client
        self._max_iterations = max_iterations
        self._max_explorer_iters = max_explorer_iterations
        self._max_worker_iters = max_worker_iterations
        self._verbose = verbose
        
        # Handle config if passed from CLI
        if config:
            self._max_iterations = getattr(config, 'max_orchestrator_iterations', max_iterations)
            self._max_explorer_iters = getattr(config, 'max_explorer_iterations', max_explorer_iterations)
            self._max_worker_iters = getattr(config, 'max_worker_iterations', max_worker_iterations)
            self._verbose = getattr(config, 'verbose', verbose)
        
        # Setup tool registry
        self._registry = ToolRegistry()
        register_body_tools(self._registry, grpc_client)
        register_explorer_tools(self._registry)
        self._add_orchestration_tools()

    def _add_orchestration_tools(self) -> None:
        """Add tools for coordinating Explorer and Worker agents."""
        context = self._context
        grpc = self._grpc
        max_explorer_iters = self._max_explorer_iters
        max_worker_iters = self._max_worker_iters
        verbose = self._verbose
        
        def run_explorer(app_name: str, instructions: str = "") -> Dict[str, Any]:
            """Run Explorer to learn an application and create skill maps."""
            from agents.explorer.autonomous_explorer import HybridExplorer
            
            instr_dict = {}
            if instructions:
                try:
                    instr_dict = json.loads(instructions)
                except:
                    instr_dict = {"objective": instructions}
            
            print(f"[Orchestrator] Running Explorer on: {app_name}")
            explorer = HybridExplorer(
                context, grpc,
                max_explore_iterations=max_explorer_iters,
                max_verify_iterations=10,
                verbose=verbose,
            )
            result = explorer.explore(app_name, instr_dict, skip_verify=True)
            
            return {
                "content": [{
                    "type": "text",
                    "text": f"Explorer result: {result.status.value}\n{result.final_response[:500]}"
                }]
            }
        
        def run_worker(task: str, app_name: str = "") -> Dict[str, Any]:
            """Run Worker to execute a specific task."""
            from agents.worker.autonomous_worker import AutonomousWorker
            
            print(f"[Orchestrator] Running Worker for: {task[:50]}...")
            config = AgentConfig(max_iterations=max_worker_iters, verbose=verbose)
            worker = AutonomousWorker(context, grpc, config)
            result = worker.execute(task, app_name)
            
            return {
                "content": [{
                    "type": "text",
                    "text": f"Worker result: {result.status.value}\n{result.final_response[:500]}"
                }]
            }
        
        def list_skills() -> Dict[str, Any]:
            """List all available skill maps."""
            try:
                from storage.firestore_client import FirestoreClient
                fs = FirestoreClient(context)
                skills = fs.list_skill_maps()
                skill_info = [
                    {"skill_id": sid[:12], "capability": data.get("metadata", {}).get("capability", "")[:50]}
                    for sid, data in skills.items()
                ]
                return {"content": [{"type": "text", "text": json.dumps({"skills": skill_info}, indent=2)}]}
            except Exception as e:
                return {"content": [{"type": "text", "text": f"Error: {str(e)}"}], "isError": True}
        
        # Register tools
        self._registry.register_tool(
            name="run_explorer",
            description="""Run Explorer agent to learn an application and create skill maps.
Use this when you need to discover capabilities of an app that aren't already known.
The Explorer will autonomously:
- Launch and observe the app
- Identify UI elements and capabilities
- Create and save skill maps for automation""",
            input_schema={
                "type": "object",
                "properties": {
                    "app_name": {"type": "string", "description": "Application name to explore"},
                    "instructions": {"type": "string", "description": "Optional JSON instructions for what to explore"}
                },
                "required": ["app_name"]
            },
            handler=lambda app_name, instructions="": run_explorer(app_name, instructions),
        )
        
        self._registry.register_tool(
            name="run_worker",
            description="""Run Worker agent to execute a specific task.
Use this to perform actions in an application using available skills or direct automation.""",
            input_schema={
                "type": "object",
                "properties": {
                    "task": {"type": "string", "description": "Task to execute"},
                    "app_name": {"type": "string", "description": "Optional application name"}
                },
                "required": ["task"]
            },
            handler=lambda task, app_name="": run_worker(task, app_name),
        )
        
        self._registry.register_tool(
            name="list_skills",
            description="List all available skill maps that have been created by Explorer.",
            input_schema={"type": "object", "properties": {}},
            handler=lambda: list_skills(),
        )

    def _log(self, msg: str) -> None:
        if self._verbose:
            print(f"[Orchestrator] {msg}")

    def run(
        self,
        goal: str,
        additional_instructions: str = "",
        summarized_conversation_history: Optional[str] = None,
        raw_conversation_history: Optional[List[Dict[str, str]]] = None,
        auto_approve: bool = False,
    ) -> AgentResult:
        """
        Run autonomous orchestration with Plan-Approve-Execute flow.
        
        The agent will:
        1. Create a detailed orchestration plan
        2. Wait for user approval (unless auto_approve=True)
        3. Execute the plan (run Explorer/Worker as needed)
        4. Report results
        
        Args:
            goal: High-level goal to achieve
            additional_instructions: Optional extra instructions
            auto_approve: Skip plan approval step
            
        Returns:
            AgentResult with orchestration outcome
        """
        run_id = uuid.uuid4().hex[:8]
        
        # =====================================================
        # PHASE 0: PLANNING (with approval loop)
        # =====================================================
        self._log("=== PHASE 0: PLANNING ===")
        self._log(f"Goal: {goal}")
        self._log("Creating orchestration plan...")
        
        planner = PlanningAgent(verbose=self._verbose)
        plan = planner.create_plan(goal, app_name="multiple apps", context=additional_instructions)
        
        # Approval loop
        if not auto_approve:
            self._log("Waiting for user approval...")
            approved = False
            while not approved:
                approved, feedback = get_user_plan_approval(plan)
                if not approved:
                    if feedback in ("User rejected the plan", "Cancelled by user"):
                        self._log("Plan rejected by user")
                        return AgentResult(
                            status=AgentStatus.FAILED,
                            final_response="Plan rejected by user",
                            iterations=0,
                        )
                    self._log(f"Refining plan based on feedback: {feedback[:50]}...")
                    plan = planner.refine_plan(plan, feedback)
            
            self._log("Plan approved by user")
        else:
            self._log("Auto-approving plan")
            print("\n" + plan.to_display_string())
        
        plan.status = plan.status.APPROVED
        
        # =====================================================
        # PHASE 1: EXECUTION
        # =====================================================
        self._log("=== PHASE 1: EXECUTION ===")
        self._log("Executing approved plan...")
        
        # Create the orchestration task with plan context
        task = plan.to_execution_prompt() + "\n\n" + get_orchestrator_task(
            goal=goal,
            user_id=self._context.user_id,
            app_id=self._context.app_id,
            additional_instructions=additional_instructions,
        )
        
        # Create the orchestration agent
        config = AgentConfig(
            max_iterations=self._max_iterations,
            verbose=self._verbose,
            thread_id=f"orchestrate_{run_id}",
        )
        
        agent = AutonomousAgent(
            tool_registry=self._registry,
            system_prompt=ORCHESTRATOR_SYSTEM_PROMPT,
            config=config,
            summarized_conversation_history=summarized_conversation_history,
            raw_conversation_history=raw_conversation_history,
        )
        
        # Run the orchestration
        self._log(f"Starting with max {self._max_iterations} iterations")
        result = agent.run(task)
        
        self._log(f"Orchestration complete: {result.status.value}")
        return result


# Backward compatibility
HybridOrchestrator = AutonomousOrchestrator


def build_autonomous_orchestrator(
    context: CascadeContext,
    grpc_client: CascadeGrpcClient,
) -> AutonomousOrchestrator:
    """Factory function to build orchestrator."""
    return AutonomousOrchestrator(context, grpc_client)

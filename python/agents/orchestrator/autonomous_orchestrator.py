"""Autonomous Orchestrator: LangGraph-based Plan-Execute-Verify architecture."""

from __future__ import annotations

import json
import re
import uuid
from typing import Any, Dict, List, Optional, TypedDict

from langgraph.graph import StateGraph, END

from cascade_client.auth.context import CascadeContext
from cascade_client.grpc_client import CascadeGrpcClient

from mcp_server.tool_registry import ToolRegistry
from mcp_server.body_tools import register_body_tools
from mcp_server.explorer_tools import register_explorer_tools

from agents.core.autonomous_agent import (
    AgentConfig, AgentResult, AgentStatus, ReActVerifier
)
from agents.core.verify_prompts import ORCHESTRATOR_VERIFY_PROMPT, get_orchestrator_verify_task


# ============================================================
# Prompts
# ============================================================

ORCHESTRATOR_PLAN_PROMPT = """You are an Orchestrator planner for the Cascade agent system.

Analyze the user's goal and create a plan with sub-tasks.

Respond in this JSON format:
{
    "understanding": "Brief summary of the goal",
    "sub_tasks": [
        {"id": 1, "type": "explore" | "execute", "description": "What to do", "app_name": "optional app"},
        ...
    ],
    "success_criteria": "How to know goal is achieved"
}

Types:
- "explore": Use Explorer to learn an app and create skill maps
- "execute": Use Worker to perform a specific action
"""


# ============================================================
# State Definition
# ============================================================

class OrchestratorState(TypedDict, total=False):
    """State for the Orchestrator LangGraph."""
    # Input
    run_id: str
    goal: str
    additional_instructions: str
    
    # Planning phase
    plan: Optional[Dict[str, Any]]
    sub_tasks: List[Dict[str, Any]]
    
    # Execution phase
    current_task_index: int
    task_results: List[Dict[str, Any]]
    actions_log: List[str]
    
    # Verification phase
    verification_result: Optional[Dict[str, Any]]
    
    # Final
    status: str
    error: Optional[str]


# ============================================================
# Node Functions
# ============================================================

def node_plan(state: OrchestratorState) -> OrchestratorState:
    """Create a plan for the goal."""
    from agents.core.autonomous_agent import _get_langchain_model
    
    goal = state.get("goal", "")
    additional = state.get("additional_instructions", "")
    
    print("[Orchestrator] === PHASE 1: PLANNING ===")
    print(f"[Orchestrator] Goal: {goal}")
    
    model = _get_langchain_model(temperature=0.2)
    
    prompt = f"{ORCHESTRATOR_PLAN_PROMPT}\n\nGoal: {goal}"
    if additional:
        prompt += f"\n\nAdditional: {additional}"
    
    try:
        response = model.invoke(prompt)
        content = response.content
        
        # Parse JSON from response
        json_match = re.search(r'\{[\s\S]*\}', content)
        if json_match:
            plan = json.loads(json_match.group())
            sub_tasks = plan.get("sub_tasks", [])
            print(f"[Orchestrator] Created plan with {len(sub_tasks)} sub-tasks")
            
            return {
                **state,
                "plan": plan,
                "sub_tasks": sub_tasks,
                "actions_log": [f"Plan: {plan.get('understanding', goal)}"],
            }
    except Exception as e:
        print(f"[Orchestrator] Planning error: {e}")
    
    return {**state, "plan": None, "sub_tasks": [], "error": "Planning failed"}


def node_execute_tasks(
    state: OrchestratorState,
    context: CascadeContext,
    grpc_client: CascadeGrpcClient,
    max_explorer_iters: int,
    max_worker_iters: int,
    verbose: bool,
) -> OrchestratorState:
    """Execute all sub-tasks sequentially."""
    sub_tasks = state.get("sub_tasks", [])
    actions_log = state.get("actions_log", []).copy()
    task_results = []
    
    if not sub_tasks:
        return {**state, "task_results": [], "error": "No sub-tasks to execute"}
    
    print("[Orchestrator] === PHASE 2: EXECUTION ===")
    
    for i, task in enumerate(sub_tasks):
        task_type = task.get("type", "execute")
        description = task.get("description", "")
        app_name = task.get("app_name", "")
        
        print(f"[Orchestrator] Sub-task {i+1}/{len(sub_tasks)}: {task_type} - {description[:50]}...")
        
        if task_type == "explore":
            result = _run_explorer(context, grpc_client, app_name or description, max_explorer_iters, verbose)
            actions_log.append(f"Explored: {app_name or description} -> {result.status.value}")
        else:
            result = _run_worker(context, grpc_client, description, app_name, max_worker_iters, verbose)
            actions_log.append(f"Executed: {description[:50]} -> {result.status.value}")
        
        task_results.append({
            "task_id": task.get("id", i),
            "type": task_type,
            "status": result.status.value,
            "error": result.error,
        })
        
        if result.status == AgentStatus.FAILED:
            print(f"[Orchestrator] Sub-task failed: {result.error}")
    
    return {**state, "task_results": task_results, "actions_log": actions_log}


def _run_explorer(
    context: CascadeContext,
    grpc_client: CascadeGrpcClient,
    app_name: str,
    max_iters: int,
    verbose: bool,
) -> AgentResult:
    """Run Explorer on an app."""
    from agents.explorer.autonomous_explorer import HybridExplorer
    
    explorer = HybridExplorer(context, grpc_client, max_verify_iterations=10, verbose=verbose)
    return explorer.explore(app_name, skip_verify=True)  # Orchestrator does its own verify


def _run_worker(
    context: CascadeContext,
    grpc_client: CascadeGrpcClient,
    task: str,
    app_name: str,
    max_iters: int,
    verbose: bool,
) -> AgentResult:
    """Run Worker on a task."""
    from agents.worker.autonomous_worker import AutonomousWorker
    
    config = AgentConfig(max_iterations=max_iters, verbose=verbose)
    worker = AutonomousWorker(context, grpc_client, config)
    return worker.execute(task, app_name)


def should_continue_to_verify(state: OrchestratorState) -> str:
    """Decide whether to verify or end early."""
    if state.get("error"):
        return "end_early"
    if not state.get("plan"):
        return "end_early"
    return "verify"


# ============================================================
# Graph Builder
# ============================================================

def build_orchestrator_graph(
    context: CascadeContext,
    grpc_client: CascadeGrpcClient,
    max_explorer_iters: int = 50,
    max_worker_iters: int = 30,
    verbose: bool = True,
) -> StateGraph:
    """Build the LangGraph for orchestration."""
    
    graph = StateGraph(OrchestratorState)
    
    # Add nodes with bound dependencies
    graph.add_node("plan", node_plan)
    graph.add_node(
        "execute_tasks",
        lambda s: node_execute_tasks(s, context, grpc_client, max_explorer_iters, max_worker_iters, verbose)
    )
    
    # Define flow
    graph.set_entry_point("plan")
    graph.add_conditional_edges(
        "plan",
        lambda s: "execute" if s.get("plan") else "end_early",
        {"execute": "execute_tasks", "end_early": END}
    )
    graph.add_edge("execute_tasks", END)  # Verification handled externally
    
    return graph.compile()


# ============================================================
# HybridOrchestrator Class
# ============================================================

class HybridOrchestrator:
    """
    Orchestrator using LangGraph for Plan/Execute and ReAct for Verify.
    
    Architecture:
    - Plan: LLM decomposes goal into sub-tasks (LangGraph)
    - Execute: Run Explorer/Worker for each sub-task (LangGraph)
    - Verify: ReAct loop to confirm goal achieved (ReAct)
    """

    def __init__(
        self,
        context: CascadeContext,
        grpc_client: CascadeGrpcClient,
        config: Optional[Any] = None,  # Accept config for CLI compatibility
        max_explorer_iterations: int = 50,
        max_worker_iterations: int = 30,
        max_verify_iterations: int = 10,
        verbose: bool = True,
    ):
        self._context = context
        self._grpc = grpc_client
        self._max_explorer_iters = max_explorer_iterations
        self._max_worker_iters = max_worker_iterations
        self._max_verify_iters = max_verify_iterations
        self._verbose = verbose
        
        # Handle config if passed from CLI
        if config:
            self._max_explorer_iters = getattr(config, 'max_explorer_iterations', max_explorer_iterations)
            self._max_worker_iters = getattr(config, 'max_worker_iterations', max_worker_iterations)
            self._verbose = getattr(config, 'verbose', verbose)
        
        # Build LangGraph
        self._graph = build_orchestrator_graph(
            context, grpc_client,
            self._max_explorer_iters, self._max_worker_iters, self._verbose
        )
        
        # Setup tool registry for verification
        self._registry = ToolRegistry()
        register_body_tools(self._registry, grpc_client)
        register_explorer_tools(self._registry)
        self._add_list_skills_tool()

    def _add_list_skills_tool(self) -> None:
        """Add tool to list available skills."""
        from storage.firestore_client import FirestoreClient
        
        fs = FirestoreClient(self._context)
        
        def list_skills() -> Dict[str, Any]:
            try:
                skills = fs.list_skill_maps()
                skill_info = [
                    {"skill_id": sid, "capability": data.get("metadata", {}).get("capability", "")}
                    for sid, data in skills.items()
                ]
                return {"content": [{"type": "text", "text": json.dumps({"skills": skill_info})}]}
            except Exception as e:
                return {"content": [{"type": "text", "text": f"Error: {str(e)}"}], "isError": True}
        
        self._registry.register_tool(
            name="list_skills",
            description="List all available skill maps",
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
    ) -> AgentResult:
        """
        Run orchestration with LangGraph Plan/Execute + ReAct Verify.
        """
        run_id = uuid.uuid4().hex[:8]
        
        # =====================================================
        # PHASES 1-2: LangGraph Flow (Plan → Execute)
        # =====================================================
        self._log("=== LANGGRAPH FLOW: PLAN → EXECUTE ===")
        
        initial_state: OrchestratorState = {
            "run_id": run_id,
            "goal": goal,
            "additional_instructions": additional_instructions,
            "plan": None,
            "sub_tasks": [],
            "current_task_index": 0,
            "task_results": [],
            "actions_log": [],
            "verification_result": None,
            "status": "running",
            "error": None,
        }
        
        try:
            final_state = self._graph.invoke(initial_state)
            
            if final_state.get("error"):
                return AgentResult(
                    status=AgentStatus.FAILED,
                    final_response="",
                    iterations=0,
                    error=final_state["error"],
                )
            
            actions_log = final_state.get("actions_log", [])
            
        except Exception as e:
            self._log(f"LangGraph flow failed: {e}")
            return AgentResult(
                status=AgentStatus.FAILED,
                final_response="",
                iterations=0,
                error=str(e),
            )
        
        # =====================================================
        # PHASE 3: ReAct Verification
        # =====================================================
        self._log("=== PHASE 3: REACT VERIFICATION ===")
        
        verifier = ReActVerifier(
            tool_registry=self._registry,
            system_prompt=ORCHESTRATOR_VERIFY_PROMPT,
            max_iterations=self._max_verify_iters,
            verbose=self._verbose,
        )
        
        verify_task = get_orchestrator_verify_task(goal, "\n".join(actions_log))
        verify_result = verifier.verify(
            verification_task=verify_task,
            thread_id=f"orch_verify_{run_id}",
        )
        
        if verify_result.success:
            self._log("Goal verification PASSED")
            return AgentResult(
                status=AgentStatus.COMPLETED,
                final_response=f"Goal achieved: {verify_result.feedback}",
                iterations=verify_result.iterations,
                tool_calls=[],
            )
        else:
            self._log(f"Goal verification INCOMPLETE: {verify_result.feedback[:100]}")
            return AgentResult(
                status=AgentStatus.COMPLETED,
                final_response=f"Goal partially completed: {verify_result.feedback}",
                iterations=verify_result.iterations,
                tool_calls=[],
            )


# Backward compatibility
AutonomousOrchestrator = HybridOrchestrator


def build_autonomous_orchestrator(
    context: CascadeContext,
    grpc_client: CascadeGrpcClient,
) -> HybridOrchestrator:
    """Factory function to build orchestrator."""
    return HybridOrchestrator(context, grpc_client)

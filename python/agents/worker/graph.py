"""Worker graph execution logic with StepExecutor."""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional, TypedDict

from cascade_client.auth.context import CascadeContext
from cascade_client.grpc_client import CascadeGrpcClient
from cascade_client.models import Action, ActionType, Selector

from agents.explorer.skill_map import SkillMap, SkillStep


@dataclass
class StepStatus:
    """Result of executing a single step."""
    step_index: int
    success: bool
    message: str = ""
    action: str = ""
    
    def model_dump(self) -> Dict[str, Any]:
        return {
            "step_index": self.step_index,
            "success": self.success,
            "message": self.message,
            "action": self.action,
        }


class StepExecutor:
    """Executes skill map steps via gRPC."""
    
    def __init__(self, grpc_client: CascadeGrpcClient, dry_run: bool = False, skill_loader: Any = None):
        self._grpc = grpc_client
        self._dry_run = dry_run
        self._skill_loader = skill_loader  # Function to load skill by ID
        self._executed_skills: set = set()  # Prevent infinite loops
    
    def execute_skill(
        self, 
        skill_map: SkillMap, 
        all_skills: Optional[List[SkillMap]] = None
    ) -> List[StepStatus]:
        """Execute all steps in a skill map, including composed_of prerequisites."""
        statuses: List[StepStatus] = []
        skill_id = skill_map.metadata.skill_id
        
        # Prevent infinite loops for circular dependencies
        if skill_id in self._executed_skills:
            return [StepStatus(
                step_index=-1,
                success=True,
                message=f"Skill {skill_id} already executed (skipping duplicate)",
                action="skip"
            )]
        self._executed_skills.add(skill_id)
        
        # For composite skills with composed_of, execute prerequisites first
        if skill_map.metadata.composed_of and all_skills:
            for prereq_skill_id in skill_map.metadata.composed_of:
                prereq_skill = self._find_skill(prereq_skill_id, all_skills)
                if prereq_skill:
                    print(f"[Executor] Running prerequisite skill: {prereq_skill_id}")
                    prereq_statuses = self.execute_skill(prereq_skill, all_skills)
                    statuses.extend(prereq_statuses)
                    
                    # Stop if prerequisite failed
                    if not all(st.success for st in prereq_statuses):
                        return statuses
                else:
                    print(f"[Executor] Warning: Prerequisite skill not found: {prereq_skill_id}")
        
        # Execute this skill's own steps
        for i, step in enumerate(skill_map.steps):
            status = self.execute_step(step, i)
            statuses.append(status)
            
            # Stop on first failure
            if not status.success:
                break
        
        return statuses
    
    def _find_skill(self, skill_id: str, all_skills: List[SkillMap]) -> Optional[SkillMap]:
        """Find a skill by ID in the available skills list."""
        for skill in all_skills:
            if skill.metadata.skill_id == skill_id:
                return skill
        return None
    
    def execute_step(self, step: SkillStep, index: int) -> StepStatus:
        """Execute a single skill step."""
        if self._dry_run:
            return StepStatus(
                step_index=index,
                success=True,
                message=f"DRY RUN: {step.action}",
                action=step.action,
            )
        
        try:
            # Map action string to ActionType
            action_type = self._map_action(step.action)
            
            if step.selector:
                # UI automation path
                action = Action(
                    action_type=action_type,
                    selector=step.selector,
                    text=step.inputs.get("text", "") if step.inputs else "",
                )
                
                result = self._grpc.perform_action(action)
                
                return StepStatus(
                    step_index=index,
                    success=result.success,
                    message=result.message or "OK",
                    action=step.action,
                )
            
            elif step.api_endpoint:
                # API automation path (not implemented yet)
                return StepStatus(
                    step_index=index,
                    success=False,
                    message="API automation not yet implemented",
                    action=step.action,
                )
            
            else:
                return StepStatus(
                    step_index=index,
                    success=False,
                    message="Step has no selector or API endpoint",
                    action=step.action,
                )
                
        except Exception as e:
            return StepStatus(
                step_index=index,
                success=False,
                message=str(e),
                action=step.action,
            )
    
    def _map_action(self, action: str) -> ActionType:
        """Map action string to ActionType enum."""
        action_lower = action.lower()
        
        if "click" in action_lower:
            return ActionType.CLICK
        elif "type" in action_lower or "input" in action_lower or "enter" in action_lower:
            return ActionType.TYPE
        elif "hover" in action_lower:
            return ActionType.HOVER
        elif "focus" in action_lower:
            return ActionType.FOCUS
        elif "scroll" in action_lower:
            return ActionType.SCROLL
        elif "wait" in action_lower:
            return ActionType.WAIT_VISIBLE
        else:
            # Default to click
            return ActionType.CLICK


class WorkerState(TypedDict, total=False):
    """State for LangGraph worker graph."""
    context: CascadeContext
    run_id: str
    task: str
    available_skills: List[SkillMap]
    execution_plan: List[Any]  # List of PlannedSkill
    current_skill_index: int
    execution_history: List[Dict[str, Any]]
    pending_events: List[Any]  # List of WorkerEvent
    dry_run: bool
    metadata: Dict[str, Any]
    replan_count: int
    max_replans: int
    failed: bool


def build_worker_graph(
    storage: Any,
    executor: StepExecutor,
    planner_fn: Any = None,
    llm_client: Any = None,
    mcp_registry: Any = None,
) -> Any:
    """Build the LangGraph worker graph."""
    from langgraph.graph import StateGraph, END
    
    def plan_node(state: WorkerState) -> WorkerState:
        """Planning node - creates execution plan from available skills."""
        if planner_fn and llm_client:
            try:
                plan = planner_fn(
                    state["task"],
                    state["available_skills"],
                    llm_client,
                )
                state["execution_plan"] = plan
            except Exception as e:
                print(f"[Worker] Planning failed: {e}")
                state["execution_plan"] = []
        return state
    
    def execute_node(state: WorkerState) -> WorkerState:
        """Execution node - runs skills from the plan."""
        plan = state.get("execution_plan", [])
        current_idx = state.get("current_skill_index", 0)
        
        if current_idx >= len(plan):
            return state
        
        planned_skill = plan[current_idx]
        skill_id = planned_skill.skill_id if hasattr(planned_skill, "skill_id") else str(planned_skill)
        
        # Find the skill map
        skill_map = None
        for skill in state.get("available_skills", []):
            if skill.metadata.skill_id == skill_id:
                skill_map = skill
                break
        
        if skill_map:
            statuses = executor.execute_skill(skill_map)
            success = all(st.success for st in statuses) if statuses else False
            
            state["execution_history"].append({
                "skill_id": skill_id,
                "success": success,
                "statuses": [st.model_dump() for st in statuses],
            })
            
            if not success:
                state["failed"] = True
        
        state["current_skill_index"] = current_idx + 1
        return state
    
    def should_continue(state: WorkerState) -> str:
        """Decide whether to continue execution."""
        if state.get("failed"):
            return "end"
        
        plan = state.get("execution_plan", [])
        current_idx = state.get("current_skill_index", 0)
        
        if current_idx >= len(plan):
            return "end"
        
        return "execute"
    
    # Build the graph
    graph = StateGraph(WorkerState)
    graph.add_node("plan", plan_node)
    graph.add_node("execute", execute_node)
    
    graph.set_entry_point("plan")
    graph.add_edge("plan", "execute")
    graph.add_conditional_edges(
        "execute",
        should_continue,
        {
            "execute": "execute",
            "end": END,
        }
    )
    
    return graph.compile()

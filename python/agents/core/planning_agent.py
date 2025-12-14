"""Planning Agent for Plan-Approve-Execute architecture.

Provides:
- Plan/PlanStep data structures
- PlanningAgent for creating and refining plans
"""

from __future__ import annotations

import json
from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional
from enum import Enum

from mcp_server.tool_registry import ToolRegistry


class PlanStatus(Enum):
    """Status of a plan."""
    DRAFT = "draft"
    APPROVED = "approved"
    REJECTED = "rejected"
    EXECUTING = "executing"
    COMPLETED = "completed"


@dataclass
class PlanStep:
    """A single step in a plan."""
    step_number: int
    description: str
    action: str  # e.g., "explore", "test", "save_skill", "run_worker"
    expected_outcome: str
    completed: bool = False
    result: Optional[str] = None


@dataclass
class Plan:
    """Structured plan for agent execution."""
    goal: str
    success_criteria: List[str]
    steps: List[PlanStep]
    status: PlanStatus = PlanStatus.DRAFT
    estimated_actions: int = 0
    
    def to_display_string(self) -> str:
        """Format plan for display to user."""
        lines = [
            "━" * 50,
            f"GOAL: {self.goal}",
            "",
            "STEPS:",
        ]
        
        for step in self.steps:
            status = "✓" if step.completed else "○"
            lines.append(f"  {status} {step.step_number}. {step.description}")
            if step.expected_outcome:
                lines.append(f"      → Expected: {step.expected_outcome}")
        
        lines.append("")
        lines.append("SUCCESS CRITERIA:")
        for criterion in self.success_criteria:
            lines.append(f"  • {criterion}")
        
        lines.append("━" * 50)
        return "\n".join(lines)
    
    def to_execution_prompt(self) -> str:
        """Convert plan to execution prompt for agent."""
        steps_text = "\n".join([
            f"{s.step_number}. {s.description} (Expected: {s.expected_outcome})"
            for s in self.steps
        ])
        
        criteria_text = "\n".join([f"- {c}" for c in self.success_criteria])
        
        return f"""## Approved Plan - Execute Now

GOAL: {self.goal}

STEPS TO EXECUTE:
{steps_text}

SUCCESS CRITERIA:
{criteria_text}

INSTRUCTIONS:
1. Execute each step in order
2. After each step, briefly report progress
3. If a step fails, attempt to recover or report the issue
4. When all steps complete successfully, say "EXECUTION COMPLETE"
"""


# Prompts for planning
PLANNING_SYSTEM_PROMPT = """You are a Planning Agent. Your job is to create detailed, structured plans.

## Your Task
Given a goal, create a clear plan with:
1. Numbered steps (in order of execution)
2. Expected outcome for each step
3. Success criteria

## Output Format
You MUST output your plan in this EXACT format:

```
GOAL: [restate the goal clearly]

STEPS:
1. [First step description]
   Expected: [what should happen]
2. [Second step description]
   Expected: [what should happen]
...

SUCCESS CRITERIA:
- [First criterion]
- [Second criterion]
...
```

## Guidelines
- Be specific and actionable
- Each step should be one clear action
- Success criteria should be verifiable
- Estimate 5-15 steps for typical tasks
"""

PLANNING_TASK_TEMPLATE = """Create a detailed plan for the following goal:

GOAL: {goal}

APPLICATION: {app_name}

CONTEXT: {context}

Output your plan in the required format.
"""


class PlanningAgent:
    """Agent that creates and refines plans before execution."""
    
    def __init__(
        self,
        tool_registry: Optional[ToolRegistry] = None,
        verbose: bool = True,
    ):
        self._registry = tool_registry
        self._verbose = verbose
    
    def _log(self, msg: str) -> None:
        if self._verbose:
            print(f"[Planner] {msg}")
    
    def create_plan(
        self,
        goal: str,
        app_name: str = "",
        context: str = "",
    ) -> Plan:
        """Create a plan for the given goal."""
        self._log(f"Creating plan for: {goal[:50]}...")
        
        # Use LLM to generate plan
        from agents.core.autonomous_agent import _get_langchain_model
        
        model = _get_langchain_model(temperature=0.3)
        
        task_prompt = PLANNING_TASK_TEMPLATE.format(
            goal=goal,
            app_name=app_name or "N/A",
            context=context or "None",
        )
        
        messages = [
            {"role": "system", "content": PLANNING_SYSTEM_PROMPT},
            {"role": "user", "content": task_prompt},
        ]
        
        response = model.invoke(messages)
        plan_text = response.content
        
        # Parse the plan
        plan = self._parse_plan(plan_text, goal)
        self._log(f"Created plan with {len(plan.steps)} steps")
        
        return plan
    
    def refine_plan(
        self,
        plan: Plan,
        feedback: str,
    ) -> Plan:
        """Refine a plan based on user feedback."""
        self._log(f"Refining plan based on feedback...")
        
        from agents.core.autonomous_agent import _get_langchain_model
        
        model = _get_langchain_model(temperature=0.3)
        
        current_plan_text = plan.to_display_string()
        
        refine_prompt = f"""The user has provided feedback on this plan:

CURRENT PLAN:
{current_plan_text}

USER FEEDBACK:
{feedback}

Please create an UPDATED plan that addresses the feedback.
Output your revised plan in the required format.
"""
        
        messages = [
            {"role": "system", "content": PLANNING_SYSTEM_PROMPT},
            {"role": "user", "content": refine_prompt},
        ]
        
        response = model.invoke(messages)
        plan_text = response.content
        
        # Parse the refined plan
        refined_plan = self._parse_plan(plan_text, plan.goal)
        self._log(f"Refined plan has {len(refined_plan.steps)} steps")
        
        return refined_plan
    
    def _parse_plan(self, plan_text: str, fallback_goal: str) -> Plan:
        """Parse plan text into structured Plan object."""
        lines = plan_text.strip().split("\n")
        
        goal = fallback_goal
        steps: List[PlanStep] = []
        success_criteria: List[str] = []
        
        current_section = None
        current_step_desc = ""
        current_step_num = 0
        current_expected = ""
        
        for line in lines:
            line = line.strip()
            
            # Skip empty lines and formatting
            if not line or line.startswith("```") or line.startswith("━"):
                continue
            
            # Detect sections
            if line.upper().startswith("GOAL:"):
                goal = line[5:].strip()
                current_section = "goal"
            elif line.upper().startswith("STEPS:"):
                current_section = "steps"
            elif line.upper().startswith("SUCCESS CRITERIA:"):
                # Save pending step
                if current_step_desc:
                    steps.append(PlanStep(
                        step_number=current_step_num,
                        description=current_step_desc,
                        action="execute",
                        expected_outcome=current_expected,
                    ))
                current_section = "criteria"
            elif current_section == "steps":
                # Check if this is a new step (starts with number)
                if line and line[0].isdigit() and "." in line[:4]:
                    # Save previous step
                    if current_step_desc:
                        steps.append(PlanStep(
                            step_number=current_step_num,
                            description=current_step_desc,
                            action="execute",
                            expected_outcome=current_expected,
                        ))
                    
                    # Parse new step
                    parts = line.split(".", 1)
                    current_step_num = int(parts[0])
                    current_step_desc = parts[1].strip() if len(parts) > 1 else ""
                    current_expected = ""
                elif line.lower().startswith("expected:"):
                    current_expected = line[9:].strip()
                elif line.startswith("→"):
                    current_expected = line[1:].strip().replace("Expected:", "").strip()
            elif current_section == "criteria":
                if line.startswith("-") or line.startswith("•"):
                    success_criteria.append(line[1:].strip())
                elif line and not line.upper().startswith("SUCCESS"):
                    success_criteria.append(line)
        
        # Save final step
        if current_step_desc:
            steps.append(PlanStep(
                step_number=current_step_num,
                description=current_step_desc,
                action="execute",
                expected_outcome=current_expected,
            ))
        
        # Ensure at least some default criteria if none parsed
        if not success_criteria:
            success_criteria = ["All steps completed successfully"]
        
        return Plan(
            goal=goal,
            success_criteria=success_criteria,
            steps=steps,
            estimated_actions=len(steps) * 3,  # Rough estimate
        )


def get_user_plan_approval(plan: Plan) -> tuple[bool, str]:
    """
    Interactive prompt for user to approve, modify, or reject a plan.
    
    Returns:
        (approved: bool, feedback: str)
    """
    print("\n" + plan.to_display_string())
    print()
    
    while True:
        try:
            response = input("[?] Approve plan? [y]es / [n]o / [m]odify: ").strip().lower()
            
            if response in ("y", "yes"):
                return (True, "")
            elif response in ("n", "no"):
                return (False, "User rejected the plan")
            elif response in ("m", "modify"):
                feedback = input("[?] What changes would you like? ").strip()
                return (False, feedback)
            else:
                print("Please enter 'y', 'n', or 'm'")
        except (KeyboardInterrupt, EOFError):
            print("\nCancelled by user")
            return (False, "Cancelled by user")

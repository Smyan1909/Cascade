"""LLM-based planner for selecting and ordering skills to achieve a task."""

from __future__ import annotations

import re
from typing import List, Optional

from pydantic import BaseModel

from agents.explorer.skill_map import SkillMap
from mcp_server.llm_integration import plan_with_tools
from mcp_server.tool_registry import ToolRegistry


class PlannedSkill(BaseModel):
    """Represents one skill invocation in a plan."""

    skill_id: str
    rationale: str = ""
    inputs: dict = {}


def plan_skill_execution(
    task: str,
    available_skills: List[SkillMap],
    llm_client=None,
    max_steps: int = 5,
    mcp_registry: Optional[ToolRegistry] = None,
) -> List[PlannedSkill]:
    """
    Plan which skills to use, in what order, to complete a task.

    If llm_client and mcp_registry are provided, uses MCP tool calling.
    Otherwise falls back to heuristic planning.
    """
    if not available_skills:
        return []

    if llm_client is None or mcp_registry is None:
        return _heuristic_plan(task, available_skills, max_steps)

    try:
        plan_dicts = plan_with_tools(llm_client, task, available_skills, mcp_registry)
        return [
            PlannedSkill(**item)
            for item in plan_dicts
            if item.get("skill_id") and len(plan_dicts) <= max_steps
        ]
    except Exception:
        return _heuristic_plan(task, available_skills, max_steps)


def _heuristic_plan(
    task: str, skills: List[SkillMap], max_steps: int
) -> List[PlannedSkill]:
    """Simple keyword-based selection fallback."""
    terms = set(re.findall(r"[a-zA-Z0-9]+", task.lower()))
    scored = []
    for skill in skills:
        text = f"{skill.metadata.capability} {skill.metadata.description}".lower()
        overlap = len(terms.intersection(set(text.split())))
        scored.append((overlap, skill))
    scored.sort(key=lambda x: x[0], reverse=True)
    plan = []
    for score, skill in scored[:max_steps]:
        if score == 0 and plan:
            break
        plan.append(
            PlannedSkill(
                skill_id=skill.metadata.skill_id,
                rationale="heuristic match",
                inputs=skill.metadata.inputs or {},
            )
        )
    return plan


def _build_prompt(task: str, skills: List[SkillMap], max_steps: int) -> str:
    """Construct a concise planning prompt."""
    skills_summary = []
    for skill in skills:
        skills_summary.append(
            {
                "skill_id": skill.metadata.skill_id,
                "capability": skill.metadata.capability,
                "description": skill.metadata.description,
                "inputs": skill.metadata.inputs,
                "outputs": skill.metadata.outputs,
                "preconditions": skill.metadata.preconditions,
                "postconditions": skill.metadata.postconditions,
            }
        )
    return (
        "You are a planner that chooses which skills to execute to finish a task.\n"
        f"Task: {task}\n"
        f"Max steps: {max_steps}\n"
        "Skills:\n"
        f"{skills_summary}\n"
        "Return JSON list of steps: [{\"skill_id\": str, \"rationale\": str, \"inputs\": object}]."
    )


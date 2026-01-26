"""Lightweight planning utilities for the deterministic Worker runtime.

The Worker runtime can either:
- execute a specific `skill_id` directly, or
- plan a short list of skills to try based on the user task and available Skill Maps.

This module intentionally keeps planning deterministic and explainable so it can
be unit-tested easily (see `python/tests/unit/test_worker_planner.py`).
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import List, Optional

from agents.explorer.skill_map import SkillMap


@dataclass(frozen=True)
class PlannedSkill:
    """A single planned skill execution."""

    skill_id: str
    reason: str = ""


def plan_skill_execution(
    *,
    task: str,
    available_skills: List[SkillMap],
    llm_client=None,
) -> List[PlannedSkill]:
    """
    Produce a conservative execution plan from available skills.

    If an LLM client is provided, this function may be upgraded later to use it.
    For now it is intentionally heuristic-only so tests remain stable.
    """

    task_norm = (task or "").lower()
    if not available_skills:
        return []

    def _score(skill: SkillMap) -> tuple[int, int]:
        # Higher is better.
        capability = (skill.metadata.capability or "").lower()
        description = (skill.metadata.description or "").lower()

        score = 0
        if capability and capability in task_norm:
            score += 10
        if description and description in task_norm:
            score += 5

        # Prefer explicit capability match, then shorter/cleaner ids for stability.
        return (score, -len(skill.metadata.skill_id))

    best = sorted(available_skills, key=_score, reverse=True)

    planned: List[PlannedSkill] = []
    for skill in best:
        capability = (skill.metadata.capability or "").lower()
        if capability and capability in task_norm:
            reason = f"matched capability '{skill.metadata.capability}'"
        elif (skill.metadata.description or "").lower() in task_norm and skill.metadata.description:
            reason = "matched description"
        else:
            reason = "fallback (no direct match)"
        planned.append(PlannedSkill(skill_id=skill.metadata.skill_id, reason=reason))

    # If nothing matched, keep only the first fallback (don’t explode execution).
    has_match = any("matched" in p.reason for p in planned)
    if not has_match and planned:
        return [planned[0]]

    # Keep plan short and predictable.
    return planned[:5]



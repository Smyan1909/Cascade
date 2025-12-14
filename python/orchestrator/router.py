"""Routing and subgoal planning for the orchestrator."""

from __future__ import annotations

import re
from typing import Iterable, List, Optional

from agents.explorer.skill_map import SkillMap

from orchestrator.state import RoutingDecision, Subgoal


def _normalize(text: str) -> str:
    return re.sub(r"\\s+", " ", text.lower()).strip()


class OrchestratorRouter:
    """
    Deterministic router for matching subgoals to skills or explorer.

    The router keeps simple, explainable heuristics so decisions are traceable
    and can be logged for debugging or fed back into LLM prompts later.
    """

    def __init__(self, skills: Iterable[SkillMap]):
        self._skills = list(skills)
        self._by_skill_id = {s.metadata.skill_id: s for s in self._skills}

    def plan_subgoals(
        self, goal: str, requested_skill_ids: Optional[List[str]] = None
    ) -> List[Subgoal]:
        """
        Produce a conservative set of subgoals.

        - If the caller provided skill ids, create one subgoal per skill.
        - Otherwise, create a single subgoal mirroring the goal; later routing
          can split further when more heuristics are added.
        """
        requested_skill_ids = requested_skill_ids or []
        if requested_skill_ids:
            subgoals: List[Subgoal] = []
            for skill_id in requested_skill_ids:
                skill = self._by_skill_id.get(skill_id)
                capability = skill.metadata.capability if skill else ""
                subgoals.append(
                    Subgoal(
                        description=f"Execute skill {skill_id}",
                        required_capabilities=[capability] if capability else [],
                        preferred_skill_ids=[skill_id],
                    )
                )
            return subgoals

        return [
            Subgoal(
                description=goal,
                required_capabilities=[],
                preferred_skill_ids=[],
            )
        ]

    def choose_executor(self, subgoal: Subgoal) -> RoutingDecision:
        """
        Pick worker vs explorer and, if worker, which skill to use.

        Heuristics:
        - Explicit preferred_skill_ids win if available.
        - Next, match capability keywords against the description.
        - Otherwise, fall back to explorer for discovery.
        """
        # Preferred explicit skill ids
        for skill_id in subgoal.preferred_skill_ids:
            if skill_id in self._by_skill_id:
                return RoutingDecision(
                    subgoal_id=subgoal.subgoal_id,
                    executor_type="worker",
                    skill_id=skill_id,
                    reason="explicit skill requested",
                )

        description = _normalize(subgoal.description)
        # Capability/description matching
        for skill in self._skills:
            capability = _normalize(skill.metadata.capability or "")
            if capability and capability in description:
                return RoutingDecision(
                    subgoal_id=subgoal.subgoal_id,
                    executor_type="worker",
                    skill_id=skill.metadata.skill_id,
                    reason=f"matched capability '{skill.metadata.capability}'",
                )
            # Looser match on description keywords
            skill_desc = _normalize(skill.metadata.description or "")
            if skill_desc and skill_desc in description:
                return RoutingDecision(
                    subgoal_id=subgoal.subgoal_id,
                    executor_type="worker",
                    skill_id=skill.metadata.skill_id,
                    reason="matched skill description",
                )

        return RoutingDecision(
            subgoal_id=subgoal.subgoal_id,
            executor_type="explorer",
            reason="no matching skill; delegate to explorer",
        )



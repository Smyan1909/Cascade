"""Helpers to load and describe skills for Worker planning."""

from __future__ import annotations

from typing import Dict, List

from cascade_client.auth.context import CascadeContext
from storage.firestore_client import FirestoreClient

from agents.explorer.skill_map import SkillMap


def load_all_skills(context: CascadeContext, fs: FirestoreClient | None = None) -> List[SkillMap]:
    """Load all skill maps for the given app/user context."""
    client = fs or FirestoreClient(context)
    raw = client.list_skill_maps()
    skills: List[SkillMap] = []
    for _, data in raw.items():
        try:
            skills.append(SkillMap.model_validate(data))
        except Exception:
            # Skip invalid skill entries to avoid blocking the planner
            continue
    return skills


def get_skill_descriptions(skills: List[SkillMap]) -> List[Dict[str, str]]:
    """Return compact descriptions for LLM planning."""
    summaries: List[Dict[str, str]] = []
    for skill in skills:
        summaries.append(
            {
                "skill_id": skill.metadata.skill_id,
                "capability": skill.metadata.capability or "",
                "description": skill.metadata.description or "",
                "inputs": str(skill.metadata.inputs or {}),
                "outputs": str(skill.metadata.outputs or {}),
                "preconditions": "; ".join(skill.metadata.preconditions),
                "postconditions": "; ".join(skill.metadata.postconditions),
            }
        )
    return summaries


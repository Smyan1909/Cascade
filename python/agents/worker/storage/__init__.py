"""Firestore helpers for Worker agent."""

from __future__ import annotations

from typing import Any, Dict, Optional

from cascade_client.auth.context import CascadeContext
from storage.firestore_client import FirestoreClient

from agents.explorer.skill_map import SkillMap


class WorkerStorage:
    """Wrapper around FirestoreClient for worker-specific collections."""

    def __init__(
        self,
        context: CascadeContext,
        firestore_client: Optional[FirestoreClient] = None,
    ):
        self._context = context
        self._fs = firestore_client or FirestoreClient(context)

    def load_skill_map(self, skill_id: str) -> SkillMap:
        """Fetch a skill map by id and hydrate into SkillMap model."""
        path = self._context.get_skill_map_path(skill_id)
        data = self._fs.get_document(path)
        if not data:
            raise ValueError(f"Skill map not found at {path}")
        return SkillMap.model_validate(data)

    def save_checkpoint(self, run_id: str, state: Dict[str, Any]) -> None:
        """Persist worker checkpoint under scoped path."""
        path = self._context.get_worker_checkpoint_path(run_id)
        self._fs.save_checkpoint(path, state)

    def load_checkpoint(self, run_id: str) -> Optional[Dict[str, Any]]:
        """Load worker checkpoint if present."""
        path = self._context.get_worker_checkpoint_path(run_id)
        return self._fs.load_checkpoint(path)

    @property
    def context(self) -> CascadeContext:
        return self._context


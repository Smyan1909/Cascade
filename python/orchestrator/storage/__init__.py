"""Firestore-backed storage helpers for the Orchestrator."""

from __future__ import annotations

from typing import Optional

from cascade_client.auth.context import CascadeContext
from storage.firestore_client import FirestoreClient

from orchestrator.state import OrchestratorCheckpoint


class OrchestratorStorage:
    """Persist orchestrator checkpoints and run summaries."""

    def __init__(self, context: CascadeContext, client: Optional[FirestoreClient] = None):
        self._context = context
        self._client = client or FirestoreClient(context)

    def checkpoint_path(self, run_id: str) -> str:
        """Return the scoped Firestore path for a checkpoint document."""
        return self._context.get_orchestrator_checkpoint_path(run_id)

    def save_checkpoint(self, checkpoint: OrchestratorCheckpoint) -> None:
        """Save the given checkpoint to Firestore."""
        path = self.checkpoint_path(checkpoint.run_id)
        self._client.save_checkpoint(path, checkpoint.model_dump(mode="json"))

    def load_checkpoint(self, run_id: str) -> Optional[OrchestratorCheckpoint]:
        """Load a checkpoint if present; returns None when missing."""
        path = self.checkpoint_path(run_id)
        data = self._client.load_checkpoint(path)
        if not data:
            return None
        try:
            return OrchestratorCheckpoint.model_validate(data)
        except Exception:
            return None



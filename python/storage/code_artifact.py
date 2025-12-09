"""Code artifact schema for generated worker/orchestrator code."""

from datetime import datetime, timezone
from typing import Any, Dict, List, Optional

from pydantic import BaseModel, Field


class CodeFile(BaseModel):
    """Single file in a generated artifact."""

    path: str
    content: str
    language: Optional[str] = None


class CodeArtifact(BaseModel):
    """Structured artifact stored in Firestore."""

    version: int = Field(default=1, ge=1)
    created_at: datetime = Field(default_factory=lambda: datetime.now(timezone.utc))
    skill_id: Optional[str] = Field(default=None)
    files: List[CodeFile] = Field(default_factory=list)
    dependencies: List[str] = Field(default_factory=list)
    notes: Optional[str] = None

    def to_firestore(self) -> Dict[str, Any]:
        return self.model_dump(mode="json")


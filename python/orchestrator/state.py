"""Pydantic models for orchestrator planning, routing, and checkpointing."""

from __future__ import annotations

import uuid
from typing import Any, Dict, List, Literal, Optional

from pydantic import BaseModel, Field


class Subgoal(BaseModel):
    """Atomic subgoal produced by planning."""

    subgoal_id: str = Field(default_factory=lambda: str(uuid.uuid4()))
    description: str
    required_capabilities: List[str] = Field(
        default_factory=list,
        description="Capabilities or app hints needed to satisfy this subgoal",
    )
    preferred_skill_ids: List[str] = Field(
        default_factory=list,
        description="Skills explicitly requested or strongly preferred",
    )
    inputs: Dict[str, Any] = Field(default_factory=dict)


class RoutingDecision(BaseModel):
    """Decision for how to satisfy a subgoal."""

    subgoal_id: str
    executor_type: Literal["worker", "explorer"]
    skill_id: Optional[str] = None
    reason: str = ""


class ExecutionRecord(BaseModel):
    """Observed status/result for a routed subgoal."""

    subgoal_id: str
    status: Literal["pending", "dispatched", "completed", "failed"] = "pending"
    routed_to: Optional[RoutingDecision] = None
    output: Dict[str, Any] = Field(default_factory=dict)
    error: Optional[str] = None


class OrchestratorCheckpoint(BaseModel):
    """Checkpoint payload persisted to Firestore for resume/retry."""

    run_id: str
    goal: str
    subgoals: List[Subgoal] = Field(default_factory=list)
    decisions: List[RoutingDecision] = Field(default_factory=list)
    records: List[ExecutionRecord] = Field(default_factory=list)
    current_index: int = 0
    completed: bool = False
    failed: bool = False
    last_error: Optional[str] = None

    def upsert_record(self, record: ExecutionRecord) -> None:
        """Replace or append a record for a subgoal."""
        for idx, existing in enumerate(self.records):
            if existing.subgoal_id == record.subgoal_id:
                self.records[idx] = record
                return
        self.records.append(record)



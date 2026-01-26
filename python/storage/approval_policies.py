"""Approval policy models and matching helpers.

Policies are stored under:
  /artifacts/{app_id}/users/{user_id}/approval_policies/{policy_id}

They are used to decide whether an agent may execute capabilities such as:
- network: host allowlist
- file_write: path prefix allowlist
- ui_action: click/type/etc
- com: prog_id access (Excel, Outlook)
"""

from __future__ import annotations

from datetime import datetime, timezone
from typing import Any, Dict, List, Literal, Optional

from pydantic import BaseModel, Field


Decision = Literal["allow", "deny"]


class ApprovalRule(BaseModel):
    """A single approval rule.

    Matching semantics:
    - capability_type must match exactly
    - for each key in parameters:
        - '*' matches anything
        - otherwise exact string match
    """

    rule_id: str
    capability_type: str
    parameters: Dict[str, str] = Field(default_factory=dict)
    decision: Decision = "deny"
    note: str = ""
    created_at: datetime = Field(default_factory=lambda: datetime.now(timezone.utc))

    def matches(self, capability_type: str, parameters: Dict[str, str]) -> bool:
        if capability_type != self.capability_type:
            return False
        for k, v in self.parameters.items():
            if v == "*":
                continue
            if parameters.get(k) != v:
                return False
        return True


class ApprovalPolicyMetadata(BaseModel):
    policy_id: str
    app_id: str
    user_id: str
    version: int = Field(default=1, ge=1)
    created_at: datetime = Field(default_factory=lambda: datetime.now(timezone.utc))
    updated_at: datetime = Field(default_factory=lambda: datetime.now(timezone.utc))


class ApprovalPolicy(BaseModel):
    metadata: ApprovalPolicyMetadata
    rules: List[ApprovalRule] = Field(default_factory=list)

    def to_firestore(self) -> Dict[str, Any]:
        return self.model_dump(mode="json")

    @classmethod
    def from_firestore(cls, data: Dict[str, Any]) -> "ApprovalPolicy":
        return cls.model_validate(data)

    def decide(self, capability_type: str, parameters: Dict[str, str]) -> Optional[Decision]:
        """Return allow/deny if a rule matches; otherwise None."""
        for rule in self.rules:
            if rule.matches(capability_type, parameters):
                return rule.decision
        return None

    def add_rule(self, rule: ApprovalRule) -> None:
        self.rules.append(rule)
        self.metadata.updated_at = datetime.now(timezone.utc)



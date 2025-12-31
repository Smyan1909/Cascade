"""Interactive approvals + Firestore-scoped policy persistence.

This is used to pause before executing sensitive capabilities (network, file I/O,
COM/native automation, UI actions) unless the user passes --auto-approve.
"""

from __future__ import annotations

import uuid
from dataclasses import dataclass
from typing import Dict, Optional, Tuple

from cascade_client.auth.context import CascadeContext

from storage.firestore_client import FirestoreClient
from storage.approval_policies import ApprovalPolicy, ApprovalPolicyMetadata, ApprovalRule


@dataclass(frozen=True)
class CapabilityRequest:
    capability_type: str
    parameters: Dict[str, str]
    reason: str = ""


def _default_policy_id() -> str:
    return "default"


def _build_default_policy(context: CascadeContext) -> ApprovalPolicy:
    return ApprovalPolicy(
        metadata=ApprovalPolicyMetadata(
            policy_id=_default_policy_id(),
            app_id=context.app_id,
            user_id=context.user_id,
        ),
        rules=[],
    )


def _build_similar_rule_params(capability_type: str, parameters: Dict[str, str]) -> Dict[str, str]:
    """Heuristic: keep a small set of stable keys, wildcard the rest."""
    sticky_keys = {"host", "prog_id", "platform", "action", "method", "url_prefix"}
    out: Dict[str, str] = {}
    for k, v in parameters.items():
        out[k] = v if k in sticky_keys else "*"
    return out


class ApprovalManager:
    def __init__(self, context: CascadeContext, fs: FirestoreClient, *, auto_approve: bool = False):
        self._context = context
        self._fs = fs
        self._auto_approve = auto_approve

    @property
    def auto_approve(self) -> bool:
        return self._auto_approve

    def _load_policy(self) -> ApprovalPolicy:
        raw = self._fs.get_approval_policy(_default_policy_id())
        if raw:
            try:
                return ApprovalPolicy.from_firestore(raw)
            except Exception:
                # Corrupt policy: reset to default (safe).
                return _build_default_policy(self._context)
        return _build_default_policy(self._context)

    def _save_policy(self, policy: ApprovalPolicy) -> None:
        self._fs.upsert_approval_policy(policy.metadata.policy_id, policy.to_firestore())

    def check(self, req: CapabilityRequest) -> Optional[str]:
        """Return 'allow'/'deny'/None if no rule."""
        policy = self._load_policy()
        return policy.decide(req.capability_type, req.parameters)

    def ensure_approved(self, req: CapabilityRequest) -> bool:
        """Check policy; if not decided, prompt user and persist decision."""
        if self._auto_approve:
            return True

        decision = self.check(req)
        if decision == "allow":
            return True
        if decision == "deny":
            return False

        # Interactive prompt.
        print("\n=== Cascade Approval Required ===")
        print(f"Capability: {req.capability_type}")
        if req.parameters:
            for k, v in req.parameters.items():
                print(f"- {k}: {v}")
        if req.reason:
            print(f"Reason: {req.reason}")
        print("")
        ans = input("Allow this action? [y/N]: ").strip().lower()
        allow_once = ans in ("y", "yes")
        if not allow_once:
            self._persist_rule(req, decision="deny", allow_similar=False)
            return False

        ans2 = input("Allow similar actions in the future (for this app/user)? [y/N]: ").strip().lower()
        allow_similar = ans2 in ("y", "yes")
        self._persist_rule(req, decision="allow", allow_similar=allow_similar)
        return True

    def _persist_rule(self, req: CapabilityRequest, *, decision: str, allow_similar: bool) -> None:
        policy = self._load_policy()
        params = req.parameters
        note = "one-time"
        if allow_similar:
            params = _build_similar_rule_params(req.capability_type, req.parameters)
            note = "similar"

        rule = ApprovalRule(
            rule_id=str(uuid.uuid4()),
            capability_type=req.capability_type,
            parameters=params,
            decision=decision,  # type: ignore[arg-type]
            note=note,
        )
        policy.add_rule(rule)
        self._save_policy(policy)



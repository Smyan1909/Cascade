"""Verifier performs non-destructive checks for selectors and APIs."""

from __future__ import annotations

from typing import Any, Dict

from cascade_client.grpc_client import CascadeGrpcClient
from cascade_client.models import Action, ActionType

from .tools.api_tester import ApiTester, ApiTestResult
from .skill_map import SkillStep


class Verifier:
    """Validation helpers for hypotheses."""

    def __init__(self, grpc_client: CascadeGrpcClient, api_tester: ApiTester | None = None):
        self._grpc = grpc_client
        self._api_tester = api_tester or ApiTester()

    def verify_selector(self, step: SkillStep) -> bool:
        if not step.selector:
            return False
        action = Action(action_type=ActionType.WAIT_VISIBLE, selector=step.selector)
        try:
            status = self._grpc.perform_action(action)
            return bool(status.success)
        except Exception:
            return False

    def verify_api(self, step: SkillStep) -> ApiTestResult | None:
        if not step.api_endpoint:
            return None
        ep = step.api_endpoint
        return self._api_tester.test(
            ep.method,
            ep.url,
            headers=ep.headers,
            params=ep.query,
            json=ep.body_schema if isinstance(ep.body_schema, dict) else None,
        )


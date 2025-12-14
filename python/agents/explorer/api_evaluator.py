"""Decision logic for choosing API vs UI automation."""

from __future__ import annotations

from typing import Any, Dict, List, Literal

PreferredMethod = Literal["api", "ui"]


class ApiEvaluator:
    """Evaluate which approach to take based on reliability and prefs."""

    def __init__(self, default_method: PreferredMethod = "api"):
        self.default_method = default_method

    def decide(
        self,
        instructions: Dict[str, Any],
        api_success_rate: float,
        ui_confidence: float,
    ) -> PreferredMethod:
        # Explicit user instruction wins
        method_hint = instructions.get("preferred_method")
        if method_hint in ("api", "ui"):
            return method_hint  # respect user preference

        # Evaluate quality
        if api_success_rate >= 0.6 and api_success_rate >= ui_confidence:
            return "api"
        if ui_confidence >= 0.6:
            return "ui"
        return self.default_method

    def explain(
        self,
        method: PreferredMethod,
        api_success_rate: float,
        ui_confidence: float,
    ) -> str:
        return (
            f"Chose {method} (api_success_rate={api_success_rate:.2f}, "
            f"ui_confidence={ui_confidence:.2f})"
        )


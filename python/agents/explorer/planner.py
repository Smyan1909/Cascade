"""Planner maps manual tasks to API/UI hypotheses."""

from __future__ import annotations

from typing import Any, Dict, List, Optional

from cascade_client.models import Selector, PlatformSource

from clients.llm_client import LlmClient, LlmMessage
from .prompts import SYSTEM_PLANNER, planner_user_prompt
from .skill_map import ApiEndpoint, SkillStep


class Planner:
    """Create execution hypotheses for tasks."""

    def __init__(self, llm: Optional[LlmClient] = None):
        self._llm = llm

    def plan_from_manual_tasks(
        self, tasks: List[str], api_docs: Optional[List[Dict[str, Any]]] = None
    ) -> List[SkillStep]:
        steps: List[SkillStep] = []
        for task in tasks:
            # Simple heuristic: if API docs available, create API step with placeholder URL
            api_endpoint = None
            if api_docs:
                doc = api_docs[0]
                api_endpoint = ApiEndpoint(
                    method="GET",
                    url=doc.get("url", ""),
                    confidence=0.4,
                    evidence=doc.get("title", ""),
                )
            selector = Selector(
                platform_source=PlatformSource.WEB,
                path=[],
                text_hint=task[:40],
            )
            steps.append(
                SkillStep(
                    action="CallAPI" if api_endpoint else "Click",
                    selector=None if api_endpoint else selector,
                    api_endpoint=api_endpoint,
                    confidence=0.4 if api_endpoint else 0.3,
                )
            )

        if self._llm and tasks:
            try:
                messages = [
                    LlmMessage(role="system", content=SYSTEM_PLANNER),
                    LlmMessage(role="user", content=planner_user_prompt("\n".join(tasks))),
                ]
                _ = self._llm.generate(messages, temperature=0.2)
                # For now we do not parse; placeholder to allow future structured parsing.
            except Exception:
                pass

        return steps


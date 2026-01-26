"""LLM-based intent classification for CLI continuations.

This is used by CLI entrypoints to decide whether the user input is:
- a continuation/refinement of the existing goal/task, or
- a brand new goal/task to run next.
"""

from __future__ import annotations

import json
from dataclasses import dataclass
from typing import Optional

from clients.llm_client import LlmMessage, load_summarization_client_from_env


@dataclass(frozen=True)
class IntentDecision:
    """Decision from the intent classifier."""

    intent: str  # "continue" | "new"
    normalized_text: str


_INTENTS = {"continue", "new"}


def classify_next_input_intent(
    *,
    current_objective: str,
    user_input: str,
    summarized_conversation_history: Optional[str] = None,
) -> IntentDecision:
    """Classify whether `user_input` continues `current_objective` or starts a new one.

    Uses the lightweight client (`CASCADE_SUMMARY_*`) for speed/cost.
    """
    user_input = (user_input or "").strip()
    current_objective = (current_objective or "").strip()

    if not user_input:
        # Caller should handle empty as "quit"; but keep this predictable.
        return IntentDecision(intent="continue", normalized_text="")

    llm = load_summarization_client_from_env()

    system_prompt = (
        "You are an intent classifier for a CLI that runs autonomous agents.\n"
        "Decide whether the user's next input is a CONTINUATION/REFINEMENT of the current objective, "
        "or a BRAND NEW objective.\n\n"
        "Rules:\n"
        "- Output ONLY valid JSON.\n"
        "- JSON schema:\n"
        '  {"intent":"continue"|"new","normalized_text":"..."}\n'
        "- intent=continue if the user is refining, clarifying, correcting, or asking to do more on the SAME objective.\n"
        "- intent=new if the user is switching to a different task/goal.\n"
        "- normalized_text: rewrite the user's input as a concise instruction (if continue) or as the new task/goal (if new).\n"
        "- Do not include markdown or commentary.\n"
    )

    parts = []
    if summarized_conversation_history:
        parts.append("SUMMARIZED_CONVERSATION_HISTORY:\n" + summarized_conversation_history.strip())
    parts.append("CURRENT_OBJECTIVE:\n" + (current_objective or ""))
    parts.append("USER_INPUT:\n" + user_input)
    user_prompt = "\n\n".join(parts)

    resp = llm.generate(
        messages=[
            LlmMessage(role="system", content=system_prompt),
            LlmMessage(role="user", content=user_prompt),
        ],
        temperature=0.0,
        max_tokens=200,
    ).content.strip()

    # Parse strictly, but be resilient to occasional formatting issues.
    try:
        data = json.loads(resp)
    except Exception:
        # Best-effort extraction: try to locate the first JSON object.
        start = resp.find("{")
        end = resp.rfind("}")
        if start == -1 or end == -1 or end <= start:
            return IntentDecision(intent="continue", normalized_text=user_input)
        try:
            data = json.loads(resp[start : end + 1])
        except Exception:
            return IntentDecision(intent="continue", normalized_text=user_input)

    intent = str(data.get("intent", "continue")).strip().lower()
    normalized_text = str(data.get("normalized_text", user_input)).strip()

    if intent not in _INTENTS:
        intent = "continue"
    if not normalized_text:
        normalized_text = user_input

    return IntentDecision(intent=intent, normalized_text=normalized_text)



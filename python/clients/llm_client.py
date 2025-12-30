"""Pluggable LLM client with provider-agnostic interface."""

from __future__ import annotations

import os
from dataclasses import dataclass
from typing import Any, Iterable, List, Optional, Protocol


@dataclass
class LlmMessage:
    """Single chat message."""

    role: str
    content: str


@dataclass
class LlmResponse:
    """LLM response payload."""

    content: str
    raw: Any = None


class LlmClient(Protocol):
    """Protocol for LLM clients."""

    def generate(
        self,
        messages: Iterable[LlmMessage],
        *,
        tools: Optional[List[dict]] = None,
        temperature: float = 0.2,
        max_tokens: Optional[int] = None,
        stop: Optional[List[str]] = None,
    ) -> LlmResponse:
        ...


class _OpenAIClient:
    """Thin OpenAI client wrapper."""

    def __init__(self, model: str, api_key: str, endpoint: Optional[str] = None):
        try:
            import openai  # type: ignore
        except ImportError as exc:
            raise ImportError("openai package is required for OpenAI provider") from exc
        self._client = openai.OpenAI(api_key=api_key, base_url=endpoint)
        self._model = model

    def generate(
        self,
        messages: Iterable[LlmMessage],
        *,
        tools: Optional[List[dict]] = None,
        temperature: float = 0.2,
        max_tokens: Optional[int] = None,
        stop: Optional[List[str]] = None,
    ) -> LlmResponse:
        payload = {
            "model": self._model,
            "messages": [{"role": m.role, "content": m.content} for m in messages],
            "temperature": temperature,
            "max_tokens": max_tokens,
            "stop": stop,
        }
        if tools:
            payload["tools"] = tools
        resp = self._client.chat.completions.create(**{k: v for k, v in payload.items() if v is not None})
        content = resp.choices[0].message.content or ""
        return LlmResponse(content=content, raw=resp)


def load_llm_client_from_env() -> LlmClient:
    """Create an LLM client from env configuration."""
    provider = (os.getenv("CASCADE_MODEL_PROVIDER") or "").lower()
    model = os.getenv("CASCADE_MODEL_NAME")
    api_key = os.getenv("CASCADE_MODEL_API_KEY")
    endpoint = os.getenv("CASCADE_MODEL_ENDPOINT")

    if not provider:
        raise ValueError("CASCADE_MODEL_PROVIDER is required for LLM client")
    if provider == "openai":
        if not api_key or not model:
            raise ValueError("CASCADE_MODEL_API_KEY and CASCADE_MODEL_NAME are required for OpenAI provider")
        return _OpenAIClient(model=model, api_key=api_key, endpoint=endpoint)

    raise ValueError(f"Unsupported LLM provider: {provider}")


def load_summarization_client_from_env() -> LlmClient:
    """Create a lightweight LLM client for conversation summarization from env configuration."""
    provider = (os.getenv("CASCADE_SUMMARY_MODEL_PROVIDER") or "openai").lower()
    model = os.getenv("CASCADE_SUMMARY_MODEL_NAME") or "gpt-4o-mini"
    api_key = os.getenv("CASCADE_SUMMARY_MODEL_API_KEY") or os.getenv("CASCADE_MODEL_API_KEY")
    endpoint = os.getenv("CASCADE_SUMMARY_MODEL_ENDPOINT") or os.getenv("CASCADE_MODEL_ENDPOINT")

    if provider == "openai":
        if not api_key:
            raise ValueError("CASCADE_SUMMARY_MODEL_API_KEY or CASCADE_MODEL_API_KEY is required for summarization client")
        return _OpenAIClient(model=model, api_key=api_key, endpoint=endpoint)

    raise ValueError(f"Unsupported summarization LLM provider: {provider}")

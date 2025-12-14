"""Embedding loader with env-configurable providers."""

from __future__ import annotations

import os
from typing import Callable, List


def load_embedding_fn() -> Callable[[str], List[float]]:
    """Return an embedding function based on env; falls back to hashing."""
    provider = (os.getenv("CASCADE_EMBED_PROVIDER") or "").lower()
    model = os.getenv("CASCADE_EMBED_MODEL") or "text-embedding-3-small"
    api_key = os.getenv("CASCADE_MODEL_API_KEY") or os.getenv("CASCADE_EMBED_API_KEY")

    if provider == "openai":
        try:
            import openai
        except ImportError as exc:
            raise ImportError("openai package required for embedding provider") from exc

        client = openai.OpenAI(api_key=api_key, base_url=os.getenv("CASCADE_MODEL_ENDPOINT"))

        def embed(text: str) -> List[float]:
            resp = client.embeddings.create(model=model, input=text)
            return list(resp.data[0].embedding)

        return embed

    def fallback(text: str) -> List[float]:
        tokens = text.lower().split()
        dims = 64
        vec = [0.0] * dims
        for token in tokens:
            idx = hash(token) % dims
            vec[idx] += 1.0
        return vec

    return fallback


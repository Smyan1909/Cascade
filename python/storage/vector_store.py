"""Lightweight vector store used by manual reader."""

from __future__ import annotations

import math
from typing import List, Tuple


def _cosine(a: List[float], b: List[float]) -> float:
    if not a or not b or len(a) != len(b):
        return 0.0
    dot = sum(x * y for x, y in zip(a, b))
    norm_a = math.sqrt(sum(x * x for x in a))
    norm_b = math.sqrt(sum(x * x for x in b))
    if norm_a == 0 or norm_b == 0:
        return 0.0
    return dot / (norm_a * norm_b)


class InMemoryVectorStore:
    """Very small in-memory vector store for relevance search."""

    def __init__(self):
        self._items: List[Tuple[str, List[float], str]] = []

    def add(self, item_id: str, embedding: List[float], text: str) -> None:
        self._items.append((item_id, embedding, text))

    def similarity_search(self, embedding: List[float], k: int = 5) -> List[str]:
        scored = [
            (item_id, _cosine(embedding, stored_embedding))
            for item_id, stored_embedding, _ in self._items
        ]
        scored.sort(key=lambda x: x[1], reverse=True)
        return [item for item, _ in scored[:k]]


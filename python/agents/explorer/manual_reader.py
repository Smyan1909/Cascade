"""Manual reader that chunks and indexes documentation."""

from __future__ import annotations

import re
from typing import Callable, List, Tuple

from storage.vector_store import InMemoryVectorStore


class ManualReader:
    """Parses manuals (PDF/text) and prepares chunks + embeddings."""

    def __init__(
        self,
        vector_store: InMemoryVectorStore | None = None,
        embed_fn: Callable[[str], List[float]] | None = None,
    ):
        self._vs = vector_store or InMemoryVectorStore()
        self._embed_fn = embed_fn or self._embed

    def read_pdf(self, path: str) -> str:
        try:
            import pypdf
        except ImportError:
            return ""
        reader = pypdf.PdfReader(path)
        text = ""
        for page in reader.pages:
            text += page.extract_text() or ""
            text += "\n"
        return text

    def chunk_text(self, text: str, chunk_size: int = 800, overlap: int = 100) -> List[str]:
        if not text:
            return []
        tokens = re.split(r"\s+", text)
        chunks = []
        start = 0
        while start < len(tokens):
            end = min(len(tokens), start + chunk_size)
            chunk = " ".join(tokens[start:end]).strip()
            if chunk:
                chunks.append(chunk)
            start = end - overlap
            if start < 0:
                start = 0
        return chunks

    def _embed(self, text: str) -> List[float]:
        # Lightweight embedding: use hashed token frequencies for deterministic ordering
        tokens = text.lower().split()
        dims = 64
        vec = [0.0] * dims
        for token in tokens:
            idx = hash(token) % dims
            vec[idx] += 1.0
        return vec

    def index_chunks(self, chunks: List[str]) -> InMemoryVectorStore:
        for idx, chunk in enumerate(chunks):
            embedding = self._embed_fn(chunk)
            self._vs.add(f"chunk-{idx}", embedding, chunk)
        return self._vs

    def top_chunks(self, query: str, k: int = 5) -> List[str]:
        if not query:
            return []
        embedding = self._embed_fn(query)
        ids = self._vs.similarity_search(embedding, k=k)
        # Recover text for ids
        return [text for item_id, _, text in self._vs._items if item_id in ids]

    def extract_tasks(self, chunks: List[str], max_tasks: int = 10) -> List[str]:
        tasks: List[str] = []
        for chunk in chunks:
            sentences = re.split(r"[.?!]\s+", chunk)
            for sentence in sentences:
                if "click" in sentence.lower() or "enter" in sentence.lower():
                    tasks.append(sentence.strip())
                if len(tasks) >= max_tasks:
                    return tasks
        return tasks


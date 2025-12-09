"""Instruction parsing for Explorer agent."""

from typing import Any, Dict, Optional


class InstructionParser:
    """Merge instructions from params and Firestore or other sources."""

    def __init__(self, firestore_fetcher=None):
        self._fetcher = firestore_fetcher

    def load_from_firestore(self, path: Optional[str]) -> Dict[str, Any]:
        if not path or not self._fetcher:
            return {}
        return self._fetcher(path) or {}

    def merge(self, primary: Dict[str, Any], fallback: Dict[str, Any]) -> Dict[str, Any]:
        merged = {}
        merged.update(fallback or {})
        merged.update(primary or {})
        return merged

    def parse(self, params: Dict[str, Any], firestore_path: Optional[str] = None) -> Dict[str, Any]:
        firestore_data = self.load_from_firestore(firestore_path)
        return self.merge(params, firestore_data)


"""Observer fetches semantic tree and proposes selectors."""

from __future__ import annotations

from typing import List

from cascade_client.grpc_client import CascadeGrpcClient
from cascade_client.models import Selector, PlatformSource, SemanticTree


class Observer:
    """Fetches UI structure via Body and proposes selectors."""

    def __init__(self, grpc_client: CascadeGrpcClient):
        self._grpc = grpc_client

    def fetch_semantic_tree(self) -> SemanticTree:
        return self._grpc.get_semantic_tree()

    def propose_selectors(self, tree: SemanticTree, text_hint: str, max_results: int = 3) -> List[Selector]:
        candidates: List[Selector] = []
        for elem in tree.elements:
            if text_hint.lower() in (elem.name or "").lower():
                candidates.append(
                    Selector(
                        platform_source=elem.platform_source or PlatformSource.PLATFORM_SOURCE_UNSPECIFIED,
                        path=[elem.id],
                        name=elem.name,
                        control_type=elem.control_type,
                        text_hint=text_hint,
                    )
                )
            if len(candidates) >= max_results:
                break
        return candidates


"""API discovery utilities."""

from __future__ import annotations

from typing import Any, Dict, List, Optional

from .tools.web_search import WebSearchClient
from .tools.api_tester import ApiTester, ApiTestResult
from clients.llm_client import LlmClient, LlmMessage


class ApiDiscovery:
    """Discover APIs from web search and optional code hints."""

    def __init__(
        self,
        search_client: Optional[WebSearchClient] = None,
        api_tester: Optional[ApiTester] = None,
        llm: Optional[LlmClient] = None,
    ):
        self._search = search_client or WebSearchClient()
        self._tester = api_tester or ApiTester()
        self._llm = llm

    def discover_via_web(self, app_name: str, top_k: int = 5) -> List[Dict[str, Any]]:
        query = f"{app_name} API documentation"
        try:
            results = self._search.search(query, top_k=top_k)
            return results
        except Exception as e:
            print(f"[ApiDiscovery] Web search unavailable: {e}")
            return []

    def probe_endpoint(
        self,
        method: str,
        url: str,
        *,
        headers: Optional[Dict[str, str]] = None,
        params: Optional[Dict[str, Any]] = None,
        json: Optional[Dict[str, Any]] = None,
    ) -> ApiTestResult:
        return self._tester.test(method, url, headers=headers, params=params, json=json)

    def shortlist_endpoints(
        self, docs: List[Dict[str, str]], keywords: Optional[List[str]] = None
    ) -> List[Dict[str, str]]:
        if not keywords:
            return docs[:5]
        filtered: List[Dict[str, str]] = []
        for doc in docs:
            title = doc.get("title", "").lower()
            if any(keyword.lower() in title for keyword in keywords):
                filtered.append(doc)
        return filtered

    def summarize_docs(self, docs: List[Dict[str, str]]) -> Optional[str]:
        if not self._llm or not docs:
            return None
        joined = "\n".join(f"{d.get('title','')}: {d.get('url','')}" for d in docs)
        try:
            resp = self._llm.generate(
                [
                    LlmMessage(
                        role="system",
                        content="Summarize API docs and highlight endpoints if present.",
                    ),
                    LlmMessage(role="user", content=joined),
                ],
                temperature=0.1,
            )
            return resp.content
        except Exception:
            return None


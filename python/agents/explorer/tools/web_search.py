"""Configurable web search client."""

import os
from typing import Any, Dict, List, Optional


class WebSearchError(Exception):
    """Raised when search fails."""


class WebSearchClient:
    """Unified interface over configurable providers."""

    def __init__(
        self,
        provider: Optional[str] = None,
        api_key: Optional[str] = None,
        http_client: Optional[Any] = None,
    ):
        self.provider = provider or os.getenv("CASCADE_WEB_SEARCH_PROVIDER", "").lower()
        self.api_key = api_key or os.getenv("CASCADE_WEB_SEARCH_API_KEY")
        self._http = http_client

    def search(self, query: str, top_k: int = 5) -> List[Dict[str, str]]:
        """Perform a search. This uses simple provider-specific HTTP calls."""
        if not self.provider:
            raise WebSearchError("Web search provider not configured")
        provider = self.provider
        if provider == "tavily":
            return self._search_tavily(query, top_k)
        if provider == "serper":
            return self._search_serper(query, top_k)
        if provider in ("google", "google_search"):
            return self._search_google_serpapi(query, top_k)
        raise WebSearchError(f"Unsupported provider: {provider}")

    def _get_http(self):
        if self._http:
            return self._http
        try:
            import requests
        except ImportError as exc:
            raise WebSearchError("requests is required for web search") from exc
        return requests

    def _search_tavily(self, query: str, top_k: int) -> List[Dict[str, str]]:
        http = self._get_http()
        url = "https://api.tavily.com/search"
        headers = {"Content-Type": "application/json"}
        payload = {"api_key": self.api_key, "query": query, "num_results": top_k}
        resp = http.post(url, json=payload, timeout=10)
        if resp.status_code != 200:
            raise WebSearchError(f"Tavily error: {resp.text}")
        data = resp.json()
        return [{"title": item.get("title", ""), "url": item.get("url", "")} for item in data.get("results", [])]

    def _search_serper(self, query: str, top_k: int) -> List[Dict[str, str]]:
        http = self._get_http()
        url = "https://google.serper.dev/search"
        headers = {"X-API-KEY": self.api_key, "Content-Type": "application/json"}
        payload = {"q": query, "num": top_k}
        resp = http.post(url, json=payload, headers=headers, timeout=10)
        if resp.status_code != 200:
            raise WebSearchError(f"Serper error: {resp.text}")
        data = resp.json()
        organic = data.get("organic", []) or []
        return [{"title": item.get("title", ""), "url": item.get("link", "")} for item in organic[:top_k]]

    def _search_google_serpapi(self, query: str, top_k: int) -> List[Dict[str, str]]:
        if not self.api_key:
            raise WebSearchError("CASCADE_WEB_SEARCH_API_KEY is required for google provider")
        try:
            from google_search_results import GoogleSearch
        except ImportError as exc:
            raise WebSearchError("google-search-results package is required for google provider") from exc
        params = {"q": query, "api_key": self.api_key, "num": top_k}
        search = GoogleSearch(params)
        result = search.get_dict()
        results = result.get("organic_results", []) or []
        return [
            {"title": item.get("title", ""), "url": item.get("link", "")}
            for item in results[:top_k]
        ]


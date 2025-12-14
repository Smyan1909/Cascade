"""API tester with basic safeguards."""

from dataclasses import dataclass
from typing import Any, Dict, Optional


class ApiTestError(Exception):
    """Raised when API testing fails."""


@dataclass
class ApiTestResult:
    """Result of an API call test."""

    ok: bool
    status_code: Optional[int]
    latency_ms: Optional[float]
    response_snippet: Optional[str]
    error: Optional[str] = None


class ApiTester:
    """Utility to probe API endpoints safely."""

    def __init__(self, http_client: Optional[Any] = None, timeout: float = 8.0):
        self._timeout = timeout
        self._http = http_client

    def _get_http(self):
        if self._http:
            return self._http
        try:
            import requests
        except ImportError as exc:
            raise ApiTestError("requests is required for API testing") from exc
        return requests

    def test(
        self,
        method: str,
        url: str,
        *,
        headers: Optional[Dict[str, str]] = None,
        params: Optional[Dict[str, Any]] = None,
        json: Optional[Dict[str, Any]] = None,
    ) -> ApiTestResult:
        """Run a single request with timeouts and small response sample."""
        http = self._get_http()
        try:
            resp = http.request(
                method=method.upper(),
                url=url,
                headers=headers,
                params=params,
                json=json,
                timeout=self._timeout,
            )
            snippet = resp.text[:200] if resp.text else ""
            return ApiTestResult(
                ok=resp.ok,
                status_code=resp.status_code,
                latency_ms=resp.elapsed.total_seconds() * 1000.0,
                response_snippet=snippet,
                error=None if resp.ok else snippet,
            )
        except Exception as exc:
            return ApiTestResult(
                ok=False,
                status_code=None,
                latency_ms=None,
                response_snippet=None,
                error=str(exc),
            )


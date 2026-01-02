"""Skill-related MCP tools.

This module exists to keep `python/mcp_server/cli.py` stable. Autonomous agents
register their own skill/documentation tools directly today.
"""

from __future__ import annotations

from typing import Any


def register_skill_tools(registry: Any, context: Any = None, grpc_client: Any = None) -> None:
    """Register skill tools (currently a no-op).

    NOTE: The autonomous Worker/Orchestrator register skill-context tools in-process.
    This placeholder avoids import errors when running the standalone MCP server CLI.
    """

    _ = (registry, context, grpc_client)
    return



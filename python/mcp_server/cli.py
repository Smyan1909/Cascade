"""CLI entry point for MCP server."""

import os
import sys

from cascade_client.auth.context import CascadeContext
from cascade_client.grpc_client import CascadeGrpcClient

from .body_tools import register_body_tools
from .explorer_tools import register_explorer_tools
from .server import MCPServer
from .skill_tools import register_skill_tools
from .tool_registry import ToolRegistry


def main():
    """Start MCP server on stdio."""
    if not os.getenv("CASCADE_MCP_ENABLED", "true").lower() == "true":
        sys.exit(0)

    # Initialize context and clients
    try:
        context = CascadeContext.from_env()
        grpc_endpoint = os.getenv("CASCADE_GRPC_ENDPOINT")
        if not grpc_endpoint:
            print('{"error": "CASCADE_GRPC_ENDPOINT is required"}', file=sys.stderr, flush=True)
            sys.exit(1)
        grpc_client = CascadeGrpcClient(endpoint=grpc_endpoint)
    except Exception as e:
        print(f'{{"error": "Failed to initialize: {str(e)}"}}', file=sys.stderr, flush=True)
        sys.exit(1)

    # Setup tool registry
    registry = ToolRegistry()
    register_body_tools(registry, grpc_client)
    register_explorer_tools(registry)
    register_skill_tools(registry, context, grpc_client)

    # Start server
    server = MCPServer(registry)
    server.run()


if __name__ == "__main__":
    main()


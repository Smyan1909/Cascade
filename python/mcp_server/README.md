# mcp_server

Cascade’s internal MCP (Model Context Protocol) server and tool registry.

This layer is how Cascade exposes capabilities to LLM-driven agents in a consistent way:
- Body automation via gRPC (click/type/tree/screenshot)
- Firestore persistence helpers (skills, documentation)
- Optional HTTP/API tools and code execution tools

## Key concepts

### ToolRegistry

`tool_registry.py` contains a lightweight registry that stores:
- tool name
- description
- JSON schema for inputs
- a Python handler callable

Agents convert the registry into the tool format required by LangChain/LangGraph in `agents/core/autonomous_agent.py`.

### MCP server (stdio JSON-RPC)

`server.py` implements a stdio JSON-RPC server that supports:
- `initialize`
- `tools/list`
- `tools/call`

This is useful when running Cascade tools through an external MCP-compatible runtime.

## Tool packs

- `body_tools.py`: gRPC-backed tools (Session/Automation/Vision).
- `explorer_tools.py`: Firestore-backed tools (save skill map, save documentation, etc.).
- `api_tools.py`: HTTP/web tools used by Worker/Explorer in some flows.
- `llm_integration.py`: Glue for LLM tool invocation (when needed).
- `cli.py`: MCP server CLI entrypoint.

## Adding a new tool

1) Decide which tool pack it belongs in (`body_tools`, `explorer_tools`, `api_tools`, …).
2) Register it with:
   - a stable name
   - a precise JSON input schema (keep it minimal)
   - a handler that returns a JSON-serializable result payload
3) Add tests for the tool behavior and schema under `python/tests/unit/` (and integration tests if it touches gRPC or Firestore).

## Design rules

- Keep tool boundaries explicit: if something has side effects, it should be a tool call.
- Prefer small, composable tools over one “god tool”.
- Return structured error results (and avoid raising unless truly exceptional).



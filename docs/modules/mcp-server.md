# MCP server + tools

Cascade uses an internal tool layer so agents can safely and consistently call capabilities.

Code:
- `python/mcp_server/`

## What this is (and isn’t)

- This is a **tool registry** and an optional **stdio JSON-RPC MCP server**.
- Agents typically use the `ToolRegistry` directly (converted to LangChain tools).
- The stdio MCP server exists for MCP-compatible runtimes that want to call Cascade tools out-of-process.

## Components

- `tool_registry.py`: registers tools with name/description/input schema/handler
- `server.py`: stdio JSON-RPC server (`initialize`, `tools/list`, `tools/call`)
- `body_tools.py`: base automation tools (now routed to gRPC/UIA for desktop and Python Playwright for web/electron)
- `playwright_tools.py`: extended `pw_*` Playwright tools (navigation, selectors, evaluation, etc.)
- `explorer_tools.py`: persistence helpers (skills/docs)
- `api_tools.py`: HTTP/API and code execution helper tools

## Tool design rules

- Keep tools **small** and **composable**.
- Return structured error payloads; don’t crash the agent loop with uncaught exceptions.
- Side effects should be **explicit tools**, not hidden inside planning code.
- Avoid tools that require interactive input.

## Adding a tool (checklist)

1) Implement the handler function.
2) Register the tool with a stable name and JSON schema.
3) Add unit tests (and integration tests if it touches gRPC or Firestore).
4) Update relevant READMEs and (if user-facing) the agent prompts that mention available tools.



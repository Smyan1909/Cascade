# agents/orchestrator (Autonomous Orchestrator)

This package contains the **LLM tool-driven** orchestrator (“autonomous mode”).

Important: there is also a **deterministic orchestrator** in `python/orchestrator/` (LangGraph + A2A dispatch). See `AGENTS.md` for a side-by-side comparison.

## Entrypoint

- CLI: `python -m agents.orchestrator.cli --goal "..." [--grpc-endpoint host:port] [--auto-approve]`

## How it works

- The orchestrator uses the shared ReAct loop in `agents/core/autonomous_agent.py`.
- It assembles a tool registry with:
  - Body tools (`mcp_server/body_tools.py`) for UI observation and actions
  - Explorer tools (`mcp_server/explorer_tools.py`) for persistence and discovery helpers
  - Orchestration tools implemented in `autonomous_orchestrator.py`:
    - `run_explorer(app_name, instructions=...)`
    - `run_worker(task, app_name=...)`
    - `list_skills()`
- Flow is **Plan → (optional approval) → Execute**:
  - Planning uses `agents/core/planning_agent.py`
  - Execution is LLM-driven, coordinating Explorer/Worker through tools

## When to use this orchestrator

- You want rapid experimentation where the LLM coordinates end-to-end.
- You are OK with less determinism in exchange for flexibility.

For predictable, testable orchestration and A2A-based dispatch, prefer `python/orchestrator/`.



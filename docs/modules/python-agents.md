# Python agents (Brain)

This page explains how the Python Brain is structured and how the agents actually run.

For system-wide architecture and invariants (Firestore scoping, gRPC boundaries), see `AGENTS.md` and `docs/00-overview.md`.

## Packages at a glance

- `python/agents/core/`: shared agent infrastructure (ReAct loop, planning, summarization)
- `python/agents/explorer/`: Explorer (teacher) for discovering UI/app capabilities and writing Skill Maps + documentation to Firestore
- `python/agents/worker/`: Worker (executor) for running tasks using saved skills and tools
- `python/orchestrator/`: deterministic orchestrator (LangGraph + A2A dispatch)
- `python/agents/orchestrator/`: autonomous orchestrator (LLM tool-driven, calls Explorer/Worker through tools)
- `python/cascade_client/`: gRPC + A2A client SDK used by agents
- `python/storage/`: Firestore utilities and shared persistence helpers
- `python/mcp_server/`: tool registry + optional stdio MCP server

## The agent loop (`agents/core/autonomous_agent.py`)

Most autonomous behavior uses the shared `AutonomousAgent` loop:

- It receives a **task prompt** (string) and optional structured **context** (JSON).
- The model emits tool calls; the agent runs tools via an internal `ToolRegistry`.
- Execution is bounded by `AgentConfig.max_iterations`.
- When `enable_verification=True`, an evaluator can decide whether the task is truly complete.

This loop is used by:
- `agents/explorer/autonomous_explorer.py` (verification on)
- `agents/worker/autonomous_worker.py` (verification off by default)
- `agents/orchestrator/autonomous_orchestrator.py` (verification depends on config)

## Agent responsibilities

### Explorer (teacher)

Explorer’s job is to **create reusable assets**:
- Skill Maps (repeatable procedures)
- Documentation objects (human-readable workflows and UI guides)

Persistence is Firestore-only, scoped under:

- `/artifacts/{app_id}/users/{user_id}/skill_maps/{skillId}`
- `/artifacts/{app_id}/users/{user_id}/documentation/{docId}` (exact collection name depends on implementation)

### Worker (executor)

Worker’s job is to **solve a user task**:
- Load skills + documentation from Firestore
- Interact with the Body via gRPC tools (semantic tree, actions, screenshots)
- Optionally call APIs and/or code execution tools (when configured)

### Orchestrator (supervisor)

There are two orchestrators:

- Deterministic (`python/orchestrator/`): plans/routs subgoals and dispatches via A2A.
- Autonomous (`python/agents/orchestrator/`): LLM creates a plan and coordinates execution via tools like `run_explorer` and `run_worker`.

## Where to start when changing behavior

- **Tool behavior**: `python/mcp_server/*_tools.py`
- **Core loop behavior**: `python/agents/core/autonomous_agent.py`
- **Skill formats / persistence**: `python/agents/explorer/skill_map.py`, `python/storage/firestore_client.py`
- **A2A semantics**: `python/cascade_client/a2a.py`



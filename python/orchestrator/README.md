# Orchestrator
Deterministic supervisor that decomposes goals, routes subgoals to Explorer/Worker, and checkpoints state in Firestore.

This is the **deterministic** orchestrator implementation (LangGraph + A2A dispatch).

Important: there is also an **autonomous** (LLM tool-driven) orchestrator in `python/agents/orchestrator/`.
See `AGENTS.md` for a side-by-side comparison.

Design reference: `docs/phase6-orchestrator.md`.

## Entrypoint

- CLI: `python -m orchestrator.cli --goal "..." [--grpc-endpoint host:port] [--dry-run] [--resume] [--trace]`

## How it works (high level)

The orchestrator graph is intentionally simple and deterministic:

1) **Load checkpoint** (optional)
   - If `--resume` is set, loads `/orchestrator_checkpoints/{runId}` from Firestore.

2) **Plan subgoals**
   - `OrchestratorRouter.plan_subgoals(...)` produces a conservative set of subgoals.
   - If you pass `--skills`, it will create one subgoal per requested skill id.

3) **Route**
   - `OrchestratorRouter.choose_executor(...)` decides:
     - execute via **worker** with a specific `skill_id`, or
     - delegate to **explorer** for discovery when no matching skill exists.

4) **Dispatch via A2A**
   - Uses `cascade_client.a2a.AgentA2AClient` to send JSON payloads to either the `worker` or `explorer` role.
   - Delivery is **at-least-once**, so handlers must be **idempotent** on `message_id`.

5) **Checkpoint**
   - After each dispatch step, stores an `OrchestratorCheckpoint` to Firestore so runs are resumable.

## Key files

- `cli.py`: CLI parsing, context creation, graph invocation
- `graph.py`: LangGraph wiring and checkpoint behavior
- `router.py`: deterministic routing heuristics
- `state.py`: Pydantic models (`Subgoal`, `RoutingDecision`, `ExecutionRecord`, `OrchestratorCheckpoint`)
- `storage/`: Firestore read/write helpers

## Running notes

- `--dry-run` plans and checkpoints but does not send A2A messages.
- `--resume` expects `--run-id` to identify which checkpoint to load.
- For local integration testing, run the Firestore emulator and set `FIRESTORE_EMULATOR_HOST`.



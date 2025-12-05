# Phase 6 — Orchestrator (Supervisor) with Firestore Checkpoints

Purpose: define the top-level supervisor that decomposes goals, routes data between Workers, and persists state per user. No code yet—only design and steps.

## Deliverables
- Directory scaffold: `python/orchestrator/`
  - `graph.py` (Supervisor wiring)
  - `router.py` (data routing/subgoal handling)
  - `state.py` (shared state shapes)
  - `cli.py` (entrypoint spec)
  - `storage/` (shared Firestore helpers)

## Firestore Persistence
- Root: `/artifacts/{__app_id}/users/{userId}`
- Orchestrator checkpoints: `/orchestrator_checkpoints/{runId}` — LangGraph supervisor state for resume/retry.
- May record run summaries/metrics under `/orchestrator_runs/{runId}` (optional) with links to worker runIds.
- All operations require `__initial_auth_token`; no local state files.

## Orchestration Flow
1) Input: high-level goal + `userId` + `__app_id` + `auth_token`.
2) Planning: decompose into subgoals; map to required skills (query Firestore for available Skill Maps).
3) Worker Instantiation: load workers via `WorkerAgent.load(skillId, userId, appId, auth_token)`.
4) Execution:
   - Run parallel/serial branches as needed.
   - Route outputs between workers (e.g., Email → SAP).
   - Apply retries/backoff; escalate/human-in-loop hooks.
5) Checkpoint:
   - Persist supervisor graph state after key transitions to `/orchestrator_checkpoints/{runId}`.
   - Store worker run references for traceability.
6) Completion: record outcome in run summary; surface logs/metrics.

## CLI Expectations
- Command: `cascade orchestrate --goal "..." --user-id ... --app-id ... --auth-token ... --skills skillId1,skillId2 --dry-run? --trace?`
- Flags for: gRPC endpoint, Firestore project/creds, model provider/name/endpoint.
- Supports dry-run (plan only) and verbose tracing for debugging.

## Design Notes
- Keep orchestration deterministic where possible; log decision rationale.
- Failure handling: bounded retries, circuit breakers for repeated failures, and escalation path.
- Isolation: each run tied to `userId`/`appId`; no cross-user data access.

## Testing Plan
- Unit:
  - Router decision logic; subgoal-to-skill matching.
  - Failure handling paths and retry policies (mock workers).
  - CLI argument parsing and config assembly.
- Integration (Firestore emulator + stub workers):
  - Simulate two-worker flow with shared data routing; verify checkpoints stored under `/orchestrator_checkpoints/{runId}`.
  - Resume from checkpoint and complete.
  - Dry-run mode produces a plan without gRPC side effects.


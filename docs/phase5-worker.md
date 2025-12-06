# Phase 5 — Worker Agent (Specialist Runtime) with Firestore State

Purpose: define how a Worker loads a Skill Map from Firestore, executes it via gRPC, and checkpoints state per user. No code yet—only design and steps.

## Deliverables
- Directory scaffold: `python/agents/worker/`
  - `runtime.py`
  - `graph.py`
  - `api.py`
  - `storage/` (shared Firestore helpers)
- Execution semantics and checkpointing plan.

## Firestore Persistence
- Root: `/artifacts/{__app_id}/users/{userId}`
- Skill Map fetch: `/skill_maps/{skillId}` (created by Explorer).
- Worker checkpoints: `/worker_checkpoints/{runId}` — contains LangGraph state and step progress for resume/retry.
- All operations use `__initial_auth_token` with user/app context; no local file storage.

## Runtime Flow
1) Load: `WorkerAgent.load(skillId, userId, appId, auth_token)` fetches Skill Map from Firestore.
2) Initialize LangGraph with steps from Skill Map; attach context (gRPC client, selectors).
3) Execute steps:
   - Perform action via gRPC (normalized selector).
   - Handle waits, retries, fallbacks per step definition.
   - Emit telemetry (structured logs).
4) Checkpoint after each step or branch to `/worker_checkpoints/{runId}` for resumability.
5) Completion: mark run status and final outputs (if any) back to Firestore or return upstream.

## Design Notes
- Determinism: keep step order stable; record which fallback was used.
- Telemetry: include `runId`, `skillId`, `userId`, `appId` in logs; capture failures with structured reasons.
- Safety: support dry-run mode (no clicks) for validation/testing.

## Testing Plan
- Unit:
  - Step executor with mocked gRPC client (success/failure paths).
  - Checkpoint serializer/deserializer (Firestone wrapper mocked or emulator).
  - Fallback selection logic.
- Integration (Firestore emulator + mock Body):
  - Load a fixture Skill Map; execute; verify writes to `/worker_checkpoints/{runId}`.
  - Resume from a mid-run checkpoint and finish.
  - Ensure no local file writes during the run.


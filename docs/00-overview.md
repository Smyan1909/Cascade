# Cascade Overview

Purpose: high-level architecture, data boundaries, and conventions before writing code. This doc is the contract for all other phase guides.

## Architecture at a Glance
- Processes: Python Brain (Explorer, Worker, Orchestrator) + C# Body (gRPC). Communication via `cascade.proto`.
- Persistence: Firestore only, user-scoped at `/artifacts/{__app_id}/users/{userId}/{collection}`. No local JSON/YAML for skills or checkpoints.
- Auth: `__initial_auth_token` initializes Firestore and binds `userId` + `__app_id`. All persistence and agent init must include this context.
- Model policy: LLM-agnostic; select provider/name/endpoint via env at runtime. Never hardcode model IDs.
- Platforms: Windows/Java via UIA3 (FlaUI); Web via Playwright. OCR via Windows.Media.Ocr; input fallback via InputSimulatorPlus.

## Data & Paths
- Skill Maps: `/artifacts/{__app_id}/users/{userId}/skill_maps/{skillId}`
- Worker checkpoints: `/artifacts/{__app_id}/users/{userId}/worker_checkpoints/{runId}`
- Explorer checkpoints: `/artifacts/{__app_id}/users/{userId}/explorer_checkpoints/{runId}`
- Orchestrator checkpoints: `/artifacts/{__app_id}/users/{userId}/orchestrator_checkpoints/{runId}`
- Other collections (metrics/logs) must also nest under the same user/app path.

## Configuration (env-first)
- Firestore: `GOOGLE_APPLICATION_CREDENTIALS` or token env; `CASCADE_APP_ID`, `CASCADE_USER_ID`, `CASCADE_AUTH_TOKEN`.
- Models: `CASCADE_MODEL_PROVIDER`, `CASCADE_MODEL_NAME`, `CASCADE_MODEL_ENDPOINT`, `CASCADE_MODEL_API_KEY` (if needed).
- gRPC endpoint: `CASCADE_GRPC_ENDPOINT` (host:port).
- Runtime toggles: headless (Playwright), timeouts, retries, log level.

## Logging & Telemetry
- Structured JSON logs; include `userId`, `appId`, `runId`, correlation IDs.
- Separate interaction logs (actions, selectors) from model prompts; redact sensitive fields.

## Testing Strategy (summary)
- Unit per layer, contract tests for proto, provider smoke tests (UIA3/Playwright), Firestore emulator tests for persistence, integration per agent, optional E2E via docker-compose.

## Deliverables for this Phase
- This overview doc, referenced by all other phase docs.
- Agreement on naming, env keys, and Firestore path structure.


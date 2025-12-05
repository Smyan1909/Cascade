# Testing Strategy (All Phases)

Purpose: define how to validate each layer independently and together, with emphasis on Firestore user scoping and gRPC contracts. No code yet—only plan.

## Test Environments
- Firestore emulator for integration tests; production Firestore only in gated environments.
- Optional UI surfaces for provider smoke (sample WPF app, simple web page).
- docker-compose for E2E smoke (Body + Brain).

## Test Types
- Unit:
  - Pure logic (selectors, planners, routers, retries, config parsing).
  - Schema validation (Skill Map, checkpoints).
- Contract:
  - Proto lint and golden stubs; backward-compat check (additive changes only).
  - Round-trip gRPC calls against a mock server.
- Integration:
  - Firestore emulator CRUD under `/artifacts/{__app_id}/users/{userId}/...`
  - Explorer: manual → plan → observe (mock Body) → save Skill Map → reload.
  - Worker: load Skill Map → execute steps via mock Body → checkpoint → resume.
  - Orchestrator: stub workers → route data → checkpoints → resume.
- Provider Smoke (optional lane):
  - UIA3: sample WPF; assert tree contains expected controls; click succeeds.
  - Playwright: simple page; type/click; DOM tree shape validated.
- E2E (opt-in):
  - Compose up Body+Brain (optionally Firestore emulator container).
  - Run a scripted flow hitting Automation/Vision RPCs and a minimal orchestration dry-run.

## Cross-Cutting Checks
- No local persistence: assert no writes to filesystem for skills/checkpoints.
- User scoping: all Firestore paths must include `/artifacts/{__app_id}/users/{userId}`; tests should fail if missing.
- Logging: structured JSON with correlation IDs; redaction of sensitive fields.
- Retry/backoff: deterministic tests with injected clocks/fakes.

## Tooling & Fixtures
- Python: pytest with fixtures for Firestore emulator, mock gRPC server, fake Body.
- C#: xUnit/NUnit for helpers; minimal gRPC test host for contract checks.
- Data seeds: fixture Skill Maps, semantic trees, and manual snippets for reproducible tests.

## CI Considerations
- Split lanes:
  - Fast: unit + contract (no external deps).
  - Medium: integration with Firestore emulator + mock gRPC.
  - Slow/optional: provider smoke, E2E compose.
- Artifacts: store logs/traces for failed runs; persist golden stubs for proto.


# Phase 3 — Python Client SDK (shared)

Purpose: define the Python-side gRPC client, selector utilities, and auth/context handling used by Explorer, Worker, and Orchestrator. No code yet—only design and steps.

## Deliverables
- Directory scaffold: `python/cascade_client/`
  - `grpc_client.py` (channel, retries, deadlines)
  - `models.py` (pydantic mirrors of proto)
  - `selectors.py` (normalized selector builder)
  - `vision.py` (marked screenshot helpers)
  - `auth/context.py` (user/app/auth token context)
- Supporting docs: how to configure env, how to run health checks.

## Responsibilities
- Provide ergonomic sync/async wrappers over gRPC stubs with retry/backoff.
- Expose selector builder that stays platform-neutral (id/name/control_type/path/index/text_hint).
- Carry `userId`, `__app_id`, and `__initial_auth_token` in a shared context object for higher layers (used to init Firestore, not sent over gRPC unless later needed).
- Offer health-check utility to validate connectivity to the Body server.

## Design
1) Channel Management
   - Lazy channel creation from `CASCADE_GRPC_ENDPOINT`.
   - Deadlines: short for health, medium for semantic tree, longer for screenshots.
   - Retries with backoff on UNAVAILABLE/DEADLINE_EXCEEDED.
2) Models
   - Pydantic classes mirroring proto structures; conversion helpers from proto messages.
   - Semantic tree utilities: to graph (networkx-like) for traversal.
3) Selector Builder
   - Functions to create selectors by id/name/control_type/path/index; optional text hint.
   - No provider-specific fields; outputs proto Selector shape.
4) Vision Helpers
   - Convenience to call `GetMarkedScreenshot`, decode image bytes, and return marks list.
5) Auth/Context
   - Context object holding `userId`, `__app_id`, `auth_token`.
   - Helper to initialize Firestore clients in higher layers using this context.

## Testing Plan
- Unit:
  - Selector builder (inputs → selector objects).
  - Retry policy/backoff logic with stubbed channels.
  - Proto-to-pydantic conversions.
  - Health-check helper with a fake server.
- Integration (CI-friendly):
  - Use a mock gRPC server to exercise all RPCs with dummy payloads.
  - Optional: point to local Body instance (if available) for smoke.


# Phase 7 — Deployment with Docker Compose

Purpose: define containerization for Python (Brain) and C# (Body) plus orchestration via docker-compose. No code yet—only design and steps.

## Deliverables
- `docker/` directory with:
  - `Dockerfile.python` (runtime for Explorer/Worker/Orchestrator CLI)
  - `Dockerfile.csharp` (published Body gRPC server)
- Root `docker-compose.yml` wiring both services.
- Runbook for bring-up and smoke tests.

## Container Requirements
- Python image:
  - Base: python:3.11-slim (or similar).
  - Install requirements (langgraph, langchain, grpcio, Firestore client, model drivers).
  - Entrypoints for explorer/worker/orchestrator CLI.
  - Env: `CASCADE_GRPC_ENDPOINT`, `CASCADE_APP_ID`, `CASCADE_USER_ID`, `CASCADE_AUTH_TOKEN`, `CASCADE_MODEL_*`, Firestore creds (`GOOGLE_APPLICATION_CREDENTIALS` or token).
- C# image:
  - Build/publish: `dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true`.
  - Expose gRPC port (e.g., 50051).
  - Env: headless toggle (for Playwright), timeouts, log level.

## docker-compose Wiring (conceptual)
- Services:
  - `body`: builds from `Dockerfile.csharp`; ports: `50051`; healthcheck via gRPC or tcp.
  - `brain`: builds from `Dockerfile.python`; depends_on `body`; env points `CASCADE_GRPC_ENDPOINT=body:50051`.
- Shared network: default bridge; optional named network.
- Volumes: mount Firestore creds (read-only); optional logs volume.
- Health/Startup:
  - Healthcheck for `body` with a noop gRPC.
  - `brain` entrypoint may include wait-for-port logic or rely on `depends_on` + healthcheck.

## Runbook
1) Build: `docker-compose build`
2) Up: `docker-compose up -d`
3) Smoke:
   - From `brain` container, run a health-check CLI hitting `body`.
   - Optional: run a minimal orchestration dry-run.
4) Logs: `docker-compose logs -f body brain`
5) Tear down: `docker-compose down`

## Testing Plan
- Compose smoke: ensure both services start, healthcheck passes.
- Integration (optional CI lane):
  - Use Firestore emulator + mock Playwright/UIA surfaces if available.
  - Run a scripted flow that lists semantic tree and requests a marked screenshot.
- Security/config checks:
  - Verify envs are present; fail fast if creds missing.
  - Ensure no secrets baked into images; creds only via env/volume.


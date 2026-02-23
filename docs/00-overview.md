# Cascade Overview

This document describes the architecture, data boundaries, and conventions that Cascade follows. It is the single source of truth for how the system fits together.

## Architecture at a Glance
- **Body (C#)**: hosts UI automation providers for Windows UIA3, Java UIA, and Playwright (web).
- **Brain (Python)**: Explorer, Worker, and Orchestrator agents that plan and execute tasks.
- **OpenClaw plugin**: exposes Cascade tools so users can drive automation with natural language.
- **Persistence (optional)**: Firestore stores skills and checkpoints under strict user/app scoping.

## Core Contracts
- **gRPC contract**: `proto/cascade.proto` defines selectors, actions, and tool payloads.
- **Selector stability**: prefer `element_id` (runtime ID) and `automation_id` when available; fall back to name/control type/path.
- **Text entry**: `text_entry_mode` controls append vs replace.

## Data & Paths (Firestore)
All data is scoped under:
- `/artifacts/{app_id}/users/{user_id}/...`

Common collections:
- `skill_maps`
- `explorer_checkpoints`
- `worker_checkpoints`
- `orchestrator_checkpoints`

## Configuration (env-first)
- **gRPC**: `CASCADE_GRPC_ENDPOINT`
- **Firestore**: `CASCADE_APP_ID`, `CASCADE_USER_ID`, `CASCADE_AUTH_TOKEN`, `GOOGLE_APPLICATION_CREDENTIALS`
- **Models**: `CASCADE_MODEL_PROVIDER`, `CASCADE_MODEL_NAME`, `CASCADE_MODEL_ENDPOINT`, `CASCADE_MODEL_API_KEY`
- **Runtime**: headless mode, action timeouts, log levels

## Security & Safety
- UI automation runs locally; use remote endpoints only if you trust the network.
- A2A is opt-in and allow-listed.
- Firestore credentials are user-supplied and never embedded in code.

## Testing Summary
- gRPC contract tests for `proto/cascade.proto`
- Provider smoke tests (UIA3 + Playwright)
- Agent unit + integration tests
- Firestore emulator tests for persistence

# gRPC: Brain ↔ Body contract

The gRPC surface is the stable boundary between the Python Brain and C# Body.

Single source of truth:
- `proto/cascade.proto`

Related docs:
- `docs/phase1-grpc.md`
- `docs/phase2-csharp-body.md`
- `docs/phase3-python-sdk.md`

## Services (overview)

Body-implemented services:
- **SessionService**: start/reset app lifecycle (`StartApp`, `ResetState`)
- **AutomationService**: query UI + perform actions (`GetSemanticTree`, `PerformAction`)
- **VisionService**: marked screenshot (`GetMarkedScreenshot`)
- **AgentCommService**: A2A messaging for agents (`RegisterAgent`, `SendAgentMessage`, `StreamAgentInbox`, `AckAgentMessage`)

Brain-implemented service:
- **WorkerService**: server-streaming execution events (`StartWorkerRun`, `ResumeWorkerRun`)

Optional/future:
- **CodeExecutionService**: execute a stored code artifact via the Body

## Selector model (why it matters)

All UI interaction is mediated through a **platform-neutral selector**:
- `platform_source`: WINDOWS | JAVA | WEB
- optional filters: `id`, `name`, `control_type`, `index`, `text_hint`
- `path`: provider-specific path components (normalized into strings)

Rule: **no provider-specific fields should leak into Python prompts** beyond these normalized fields.

## Code generation

### Python

Use:
- `python/generate_proto.ps1` (Windows)
- `python/generate_proto.sh` (Linux/Mac)

Outputs are committed under:
- `python/cascade_client/proto/`

### C#

Body uses `option csharp_namespace = "Cascade.Proto";` and can generate stubs using `Grpc.Tools`/`dotnet-grpc`.

## Compatibility rules

- Additive-only changes are safe (new fields with new field numbers).
- Never reuse/renumber fields.
- Prefer adding new RPCs over changing the semantics of existing RPCs.

## Testing

Recommended lanes:
- **Python unit tests**: gRPC client conversions/retry behavior (see `python/tests/unit/`)
- **Python integration tests**: minimal round-trips against a running Body (`python/tests/integration/test_grpc_integration.py`)
- **C# tests**: provider + service tests under `src/Body.Tests/`



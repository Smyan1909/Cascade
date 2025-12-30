# C# Body (.NET gRPC host)

The Body is the “actuator” side of Cascade: it talks to Windows UI automation (UIA3), Playwright (web), and OCR, and exposes a normalized surface over gRPC.

Code:
- `src/Body/`

Design docs:
- `docs/phase2-csharp-body.md`
- `docs/phase1-grpc.md`

## Entry point and hosting

- `src/Body/Program.cs` configures Kestrel to listen on HTTP/2 (default port `50051`) and registers:
  - `SessionService`, `AutomationService`, `VisionService`, `AgentCommService`
  - automation providers via `IAutomationProvider`

## Providers

Providers implement a normalized automation contract:
- UIA3 provider (Windows + Java surface)
- Playwright provider (web surface)

Routing happens through `AutomationRouter` based on `selector.platform_source` (or a configured default platform).

## Vision and OCR

- Vision returns a screenshot plus numeric marks mapped to `UIElement.id`.
- OCR (Windows.Media.Ocr) can be used to enrich missing labels/value text, controlled via config.

## Configuration

Configuration is env-first via `appsettings.json` and environment variables.
See `src/Body/README.md` for the current option keys.

## Testing

- Build: `dotnet build src/Body/Body.csproj`
- Tests: `dotnet test src/Body.Tests/Body.Tests.csproj`

Recommended lanes:
- Pure unit tests (normalization, routing, marker logic)
- Provider smoke tests (UIA3 + Playwright) where environment supports it



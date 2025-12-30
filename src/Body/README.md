# src/Body/

ASP.NET Core gRPC host that implements the Phase 1 contract (`proto/cascade.proto`) and providers for UIA3 (Windows/Java) and Playwright (Web). See `docs/phase2-csharp-body.md` and `docs/00-overview.md` for architecture and data paths.

For the end-to-end architecture (Brain↔Body↔Firestore) and dev rules, see `AGENTS.md`.

## Run
- Restore/build: `dotnet build src/Body/Body.csproj`
- Run: `dotnet run --project src/Body/Body.csproj`
- Default listens on `localhost:50051` over HTTP/2 (no TLS in dev).

## Configuration (env-first)
Values are read from env vars or `appsettings.json`.
- `Body:DefaultPlatform` (`WINDOWS`|`JAVA`|`WEB`) — used when requests omit platform.
- `Body:DefaultUrl` — page to open on startup for web.
- `Playwright:Headless`, `Playwright:ActionTimeoutMs`, `Playwright:BrowserChannel`, `Playwright:MaxNodes`, `Playwright:TreeDepth`.
- `UIA3:ActionTimeoutMs`, `UIA3:MaxNodes`, `UIA3:TreeDepth`.
- `Vision:MaxWidth`, `Vision:MaxHeight`, `Vision:FontSize`, `Vision:StrokeWidth`, `Vision:EnableVisionOcr`.
- `Ocr:Enabled`, `Ocr:LanguageTag` — Windows.Media.Ocr fallback for missing labels.
- `Kestrel:Port` — gRPC port (defaults to 50051).

## Services & Providers
- `SessionService`: `StartApp` (web → Playwright, else UIA3 by default), `ResetState`.
- `AutomationService`: `PerformAction` + `GetSemanticTree` routed by selector.platform_source or default platform.
- `VisionService`: `GetMarkedScreenshot` overlays numeric marks if not provided by provider.
- Providers:
  - UIA3 (FlaUI): launches apps, extracts semantic tree, basic actions (click/type/hover/focus/scroll/wait).
  - Playwright: navigates to URL, extracts DOM interactives, basic actions.

## Code map

For details, see the submodule READMEs:
- `src/Body/Automation/README.md`
- `src/Body/Services/README.md`
- `src/Body/Providers/README.md`
- `src/Body/Vision/README.md`
- `src/Body/Configuration/README.md`

## Testing notes
- Contract: ensure `proto/cascade.proto` stays in sync; regenerate stubs as part of CI contract tests.
- Smoke: start the host and perform one RPC round-trip per service with dummy payloads.
- Provider smoke (optional): UIA3 with sample WPF app; Playwright with simple page.


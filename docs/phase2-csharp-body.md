# Phase 2 — C# Body Server (.NET 8 gRPC CLI)

Purpose: design the Body that normalizes UIA3/Playwright surfaces, executes actions, and produces tagged screenshots. No implementation yet—only architecture and steps.

## Deliverables
- Directory scaffold: `src/Body/`
  - `Program.cs` (host wiring)
  - `Services/` (SessionService, AutomationService, VisionService)
  - `Automation/IAutomationProvider.cs`
  - `Providers/UIA3Provider/`
  - `Providers/PlaywrightProvider/`
  - `Vision/` (tagging utilities)
  - `Configuration/` (options, logging)
- Config stub: `appsettings.json` template (documented only).
- Test plan (unit, provider smoke, contract).

## Responsibilities
- Serve gRPC per `cascade.proto`.
- Normalize UI elements into generic `UIElement` tree (stable IDs, bounds).
- Execute actions (Click/Type/Hover/Focus/Scroll/WaitVisible) routed by PlatformSource.
- Provide marked screenshots with numeric tags for interactives.
- Enrich text via OCR when accessible text is missing.

## Design
1) Hosting
   - Kestrel gRPC; reflection in dev; structured logging; graceful shutdown.
   - DI for providers; health check endpoint (optional).
2) Providers
   - `IAutomationProvider` contract: `Supports(platform)`, `GetSemanticTree()`, `PerformAction(Action)`, `Capture()` (bounds/screenshot as needed).
   - UIA3Provider (FlaUI):
     - Detect Java via Access Bridge (window class/name) but still via UIA3 surface.
     - Normalize AutomationElement → UIElement (id/name/control_type/bounding_box/parent_id/platform_source).
   - PlaywrightProvider:
     - Headless toggle; persistent context optional.
     - Normalize DOM → UIElement; map normalized selector to CSS/XPath as needed.
3) Action Routing
   - AutomationService dispatches by selector.platform_source.
   - Translate normalized selector (path + filters) to provider locator.
   - Fallback to InputSimulatorPlus when direct action fails.
4) Vision Tagging
   - Capture screen(s) to bitmap.
   - Traverse semantic tree for interactives (buttons/inputs/links/menu items).
   - Overlay numeric marks in-memory (System.Drawing); return PNG/JPEG bytes + Mark list.
5) OCR Integration
   - Windows.Media.Ocr on captured regions when name/value missing.
   - Merge OCR text into UIElement fields (e.g., value_text).
6) Configuration
   - Env/appsettings for: gRPC port, headless mode, timeouts, retries, log level, max screenshot size.
   - Optional: Playwright browser channel, UIA3 timeouts, OCR enable flag.

## Testing Plan
- Unit:
  - Selector translation helpers, ID stability functions, tagging logic (with synthetic trees).
  - Config binding and error handling.
- Provider Smoke (optional lane):
  - UIA3: launch sample WPF app; validate tree extraction and click on a known button.
  - Playwright: open simple page; validate DOM tree and click/type.
- Contract:
  - Validate generated stubs vs proto snapshot.
  - Start lightweight server instance; perform one round-trip per RPC with dummy payloads.
- Performance/Resilience (later):
  - Large tree traversal timing; screenshot size limits; retries on transient UIA/Playwright failures.


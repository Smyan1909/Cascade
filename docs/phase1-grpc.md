# Phase 1 — gRPC Contract (Bridge)

Purpose: specify the protocol so Python (Brain) and C# (Body) interoperate without platform leakage. No code generation yet—only schema and testing plan.

## Deliverables
- `proto/cascade.proto` (placeholder until implemented).
- Codegen commands (documented here) for C# and Python.
- Contract testing plan and backward-compat policy.

## Scope & Requirements
- Services:
  - `SessionService`: `StartApp(AppName)`, `ResetState()`.
  - `AutomationService`: `PerformAction(Action)`, `GetSemanticTree()`.
  - `VisionService`: `GetMarkedScreenshot()`.
- Messages:
  - `UIElement { id, name, control_type, bounding_box, parent_id, platform_source, aria_role?, automation_id?, value_text? }`
  - `SemanticTree { repeated UIElement elements }`
  - `Action { ActionType action_type; Selector selector; oneof payload { string text; double number; string json_payload; } }`
  - `Selector { platform_source; path[]; filters (id/name/control_type/index); text_hint? }`
  - `Screenshot { bytes image; ImageFormat format; repeated Mark marks }`
  - `Status { bool success; string message }`
  - `Mark { element_id; label }`
  - `NormalizedRectangle { x, y, width, height }`
- Enums:
  - `ActionType { CLICK, TYPE_TEXT, HOVER, FOCUS, SCROLL, WAIT_VISIBLE }`
  - `PlatformSource { WINDOWS, JAVA, WEB }`
  - `ControlType { BUTTON, INPUT, COMBO, MENU, TREE, TABLE, CUSTOM }`
  - `ImageFormat { PNG, JPEG }`

## Design Notes
- Selector abstraction must be platform-neutral; no DOM/UIA specifics leak to Python.
- Action payload uses `oneof` to support text/number/json; keep optional to avoid breaking changes.
- Bounding boxes use normalized rectangles to allow multi-monitor and scaling.
- IDs must be stable within a session; providers are responsible for consistent mapping.

## Codegen (to run later)
- C#: `dotnet-grpc` or `Grpc.Tools`—generate into `Cascade.Proto` namespace.
- Python: `python -m grpc_tools.protoc -I proto --python_out=. --grpc_python_out=. proto/cascade.proto`
- Add a Makefile/PowerShell helper later for reproducibility.

## Testing Plan (contract)
- Lint proto (buf or protoc lint) with CI gate.
- Golden-file test: generate stubs in CI and compare to committed snapshots to detect breaking changes.
- Backward compatibility rule: only additive changes unless major version bump; never reuse field numbers.
- Interop smoke: tiny mock server/client in CI to ensure channel creation and one round-trip per RPC with dummy payloads.


# Cascade

> Desktop automation meets conversational AI.

Cascade is an open-source desktop automation platform for Windows. It combines a C# UI Automation "Body" (UIA3 + Playwright) with a Python "Brain" (Explorer/Worker/Orchestrator agents) and an OpenClaw plugin so you can control apps and browsers with natural language.

## Highlights
- Windows desktop automation via Microsoft UI Automation (UIA3) and FlaUI.
- Web automation via Playwright.
- Screenshot tagging + OCR to help agents reason about UI.
- Skill learning with Explorer + Firestore persistence (optional).
- OpenClaw plugin with desktop, web, API, sandbox, and A2A tools.

## Quick Start

### Prerequisites
- Windows 10/11
- .NET 8 SDK
- Node.js 18+
- Python 3.12+ (required for agent workflows; optional for basic OpenClaw UI automation)

### 1) Clone and run the Body
```bash
git clone https://github.com/Smyan1909/Cascade.git
cd cascade
dotnet run --project src/Body/Body.csproj
```
The gRPC server listens on `localhost:50051` by default.

### 2) Install the OpenClaw plugin
```bash
openclaw plugins install openclaw-cascade-plugin
```

### 3) Minimal OpenClaw config
OpenClaw may block postinstall edits. If you see a missing config warning, add this to `~/.openclaw/openclaw.json`:
```json5
{
  plugins: {
    entries: {
      openclaw-cascade-plugin: {
        enabled: true,
        config: {
          cascadeGrpcEndpoint: "localhost:50051"
        }
      }
    }
  }
}
```

### 4) Try it
```bash
openclaw "Open Calculator"
openclaw "Click the 7 button"
openclaw "Type 25 * 4 and press Enter"
openclaw "Take a screenshot of the current window"
```

## Architecture
```
OpenClaw CLI
   │
   ▼
openclaw-cascade-plugin (TypeScript)
   │
   ├─ Desktop tools  → gRPC → C# Body (UIA3)
   ├─ Web tools      → Playwright
   └─ A2A tools      → gRPC → Python Agents

Python Brain (Explorer/Worker/Orchestrator)
   │
   └─ Firestore (optional, user/app scoped)
```

## Tools Overview
Cascade exposes 24 OpenClaw tools:
- **Automation (21)**: click, type (append/replace), hover, focus, scroll, wait, toggle, expand/collapse, select, range value, send keys, window state, move, resize, semantic tree, screenshot, start app.
- **A2A (3)**: Explorer, Worker, Orchestrator (opt-in).

For detailed schemas and examples (including web via `platform_source: WEB`), see `docs/openclaw-integration.md`.

## Security & Safety
- UI automation is local to your machine; gRPC stays on `localhost` unless you reconfigure it.
- A2A agent calls are opt-in and allow-listed.
- Firestore is optional and uses your own credentials and user/app scoping.
- Avoid destructive commands unless explicitly required in your task.

## Documentation
- `docs/README.md` — docs index + FAQ + roadmap
- `docs/openclaw-integration.md` — OpenClaw setup, configuration, and tool usage
- `docs/troubleshooting.md` — common issues and fixes
- `CONTRIBUTING.md` — development setup and contribution workflow
- `CHANGELOG.md` — release history

## Demo
- [![Demo video](https://img.youtube.com/vi/NACA-dGzPBc/0.jpg)](https://www.youtube.com/watch?v=NACA-dGzPBc)

## Contributing
We welcome issues and PRs. See `CONTRIBUTING.md` for setup, tests, and code style.

## Roadmap (short)
- Expand desktop action coverage and smarter selectors
- Improve agent planning/verification loops
- Add more end-to-end examples and tutorials

## License
MIT — see `LICENSE`.

## Acknowledgments
- OpenClaw
- FlaUI
- Playwright
- Firebase (optional for skill storage)

# OpenClaw Cascade Plugin

This plugin connects OpenClaw to Cascade’s desktop + web automation stack. It loads quickly and only initializes heavier dependencies (Python, Playwright) when needed.

## Install
```bash
openclaw plugins install openclaw-cascade-plugin
```

## Quick Usage
```bash
openclaw "Open Calculator"
openclaw "Click the 9 button"
openclaw "Type 8 + 3"
openclaw "Press Enter"
```

## Configuration (Optional)
The plugin defaults to `localhost:50051`. Add this to `~/.openclaw/openclaw.json` only if you need to override defaults:
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

Optional settings:
- `cascadePythonPath`
- `cascadePythonModulePath`
- `firestoreProjectId`, `firestoreCredentialsPath`
- `headless`, `actionTimeoutMs`
- `screenshotMode`, `screenshotDir`
- `enableA2A`, `allowedAgents`, `requireAgentConfirmation`

## Tool Coverage
- **Automation (21)**: UIA3 click/type/hover/focus/scroll/wait + toggle, expand/collapse, select, range value, send keys, window state, move/resize. These actions also power web automation via `platform_source: WEB`.
- **A2A (3)**: Explorer/Worker/Orchestrator (opt-in).

For full details, see `../docs/openclaw-integration.md`.

## Development
```bash
npm install
npm test
npm run build
```

## License
MIT

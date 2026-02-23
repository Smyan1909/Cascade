# OpenClaw Integration

This guide explains how to install, configure, and use the Cascade OpenClaw plugin.

## Overview
The plugin exposes 24 tools:
- **Automation (21)**: UIA3-based Windows automation actions (also used for web via `platform_source: WEB`).
- **A2A (3)**: Explorer/Worker/Orchestrator agent calls (opt-in).

## Installation
Desktop automation requires Windows and a running Body instance.

1) Install OpenClaw:
```bash
curl -fsSL https://openclaw.ai/install.sh | bash
```

2) Install the plugin:
```bash
openclaw plugins install openclaw-cascade-plugin
```

3) Run the C# Body:
```bash
git clone https://github.com/Smyan1909/Cascade.git
cd cascade
dotnet run --project src/Body/Body.csproj
```

## Basic Configuration
If OpenClaw blocks postinstall edits, add this entry to `~/.openclaw/openclaw.json`:
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

Optional config keys:
- `cascadePythonPath`
- `cascadePythonModulePath`
- `firestoreProjectId`
- `firestoreCredentialsPath`
- `headless`
- `actionTimeoutMs`
- `screenshotMode`
- `screenshotDir`
- `enableA2A`, `allowedAgents`, `requireAgentConfirmation`

## Desktop Automation
Typical usage:
```bash
openclaw "Open Calculator"
openclaw "Click the 5 button"
openclaw "Type 25 * 4"
openclaw "Press Enter"
```

### Selector fields (UIA)
When the model needs to be precise, you can reference these selector fields:
- `element_id` (runtime ID from the semantic tree)
- `automation_id`
- `class_name`
- `framework_id`
- `help_text`
- `name`, `control_type`, `path`, `index`, `text_hint`

### Text entry mode
`cascade_type_text` accepts `text_entry_mode`:
- `APPEND` (default) — inserts at caret
- `REPLACE` — replaces the current value

### Advanced UIA actions
Desktop tools include:
- Toggle, expand/collapse, select
- Range value (sliders/spinners)
- Send key chords (`CTRL+S`, `ALT+F4`, `ENTER`)
- Window state (min/max/restore/close), move, resize

## Web Automation
Web automation uses the same actions as desktop automation but sets `platform_source: WEB` in the selector. For example, you can ask for a semantic tree and then click/type using web element selectors.

## A2A (Agent-to-Agent)
A2A allows OpenClaw to invoke Explorer/Worker/Orchestrator and is **disabled by default**.
```json5
{
  plugins: {
    entries: {
      openclaw-cascade-plugin: {
        config: {
          enableA2A: true,
          allowedAgents: ["explorer", "worker", "orchestrator"],
          requireAgentConfirmation: true
        }
      }
    }
  }
}
```

## Troubleshooting
See `docs/troubleshooting.md` for common issues and fixes.

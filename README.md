# Cascade + OpenClaw

> **Desktop automation meets conversational AI**

[![Tests](https://img.shields.io/badge/tests-74%20passing-brightgreen)](./openclaw-plugin)
[![Version](https://img.shields.io/badge/version-1.0.3-blue)](./openclaw-plugin/package.json)
[![License](https://img.shields.io/badge/license-MIT-green)](./LICENSE)

Cascade is an open-source desktop automation platform that integrates with OpenClaw to provide natural language control over Windows applications, web browsers, and file processing workflows.

**Key Features:**
- 🖥️ **Desktop Automation** - Control Windows apps (Calculator, Notepad, Excel, etc.)
- 🌐 **Web Automation** - Playwright-powered browser control
- 📸 **Visual Understanding** - Screenshot analysis with element detection
- 🧠 **Skill Learning** - Explorer agent learns reusable workflows
- 💬 **Natural Language** - No coding required - just describe what you want
- 🔁 **Bi-Directional** - OpenClaw can call Explorer, Worker, and Orchestrator agents

## Quick Start

### Prerequisites
- Windows 10/11
- Node.js 18+
- Python 3.10+
- .NET 8 SDK (for C# Body)

### Installation

```bash
# 1. Install OpenClaw
curl -fsSL https://openclaw.ai/install.sh | bash

# 2. Install Cascade plugin
openclaw plugins install openclaw-cascade-plugin

# 3. Start Cascade Body (in separate terminal)
git clone https://github.com/yourusername/cascade.git
cd cascade
dotnet run --project src/Body/Body.csproj
```

### Configuration

Add to your OpenClaw config (`~/.openclaw/openclaw.json`). The plugin entry key must match the manifest id (`openclaw-cascade-plugin`).

If you previously installed `cascade` (v1.0.0), uninstall it and reinstall v1.0.3:
```bash
openclaw plugins uninstall cascade
openclaw plugins install openclaw-cascade-plugin@1.0.3
```

OpenClaw may block postinstall auto-editing for security. If the install reports missing `cascadeGrpcEndpoint`, add the plugin entry manually (see below).

To make the install fully automatic (including `PYTHONPATH`), set your repo path before installing:
```bash
export CASCADE_REPO_PATH=/path/to/cascade
openclaw plugins install openclaw-cascade-plugin@1.0.3
```



```json5
{
  plugins: {
    entries: {
      openclaw-cascade-plugin: {
        enabled: true,
        config: {
          cascadeGrpcEndpoint: "localhost:50051",
          firestoreProjectId: "your-project-id"
        }
      }
    }
  }
}
```

### Usage Examples

```bash
# Desktop automation
openclaw "Open Calculator and calculate 25 times 4"
openclaw "Take a screenshot of the current window"
openclaw "Click the Save button in Notepad"

# Web automation
openclaw "Navigate to example.com and fill out the contact form"
openclaw "Get all the links on the current page"

# Execute learned skills
openclaw "Run my Excel data processing workflow"

# Learn new skills (requires A2A)
openclaw "Explore how to create a pivot table in Excel"

> Note: CLI commands are still `cascade:status` and `cascade:tools`.
```

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    OpenClaw Gateway                         │
│                      (TypeScript)                           │
└───────────────────────┬─────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────────────────┐
│              openclaw-cascade-plugin                        │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │ 29 Tools     │  │ MCP Client   │  │ A2A Client   │      │
│  │              │  │              │  │              │      │
│  │ • Desktop: 9 │  │ JSON-RPC     │  │ Agent Comm   │      │
│  │ • Web: 15    │  │ over stdio   │  │ gRPC         │      │
│  │ • API: 2     │  │              │  │              │      │
│  │ • Sandbox: 1 │  │              │  │              │      │
│  │ • A2A: 3     │  │              │  │              │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
└─────────────────────────────────────────────────────────────┘
                        │
        ┌───────────────┴───────────────┐
        │                               │
        ▼                               ▼
┌───────────────────┐      ┌──────────────────────┐
│   Python Brain    │      │     C# Body          │
│                   │      │                      │
│ • Explorer Agent  │      │ • UIA3 (Windows)     │
│ • Worker Agent    │◄────►│ • Playwright (Web)   │
│ • Orchestrator    │      │ • OCR                │
└───────────────────┘      └──────────────────────┘
```

## Documentation

- [Integration Guide](./docs/openclaw-integration.md) - Detailed setup and configuration
- [Plugin Development](./docs/plugin-development.md) - Contributing guide
- [Skills Reference](./docs/skills-reference.md) - Available OpenClaw skills
- [Troubleshooting](./docs/troubleshooting.md) - Common issues and solutions
- [API Documentation](./docs/api-reference.md) - Tool reference

## Available Tools (29 Total)

### Desktop Automation (9)
- `cascade_click_element` - Click UI elements
- `cascade_type_text` - Type text into fields
- `cascade_hover_element` - Hover over elements
- `cascade_focus_element` - Focus on elements
- `cascade_scroll_element` - Scroll elements
- `cascade_wait_visible` - Wait for visibility
- `cascade_get_semantic_tree` - Get UI structure
- `cascade_get_screenshot` - Capture screenshots
- `cascade_start_app` - Launch applications

### Web Automation (15)
- `cascade_pw_goto` - Navigate to URL
- `cascade_pw_back` - Go back
- `cascade_pw_forward` - Go forward
- `cascade_pw_reload` - Reload page
- `cascade_pw_wait_for_url` - Wait for URL pattern
- `cascade_pw_locator_count` - Count elements
- `cascade_pw_locator_text` - Get element text
- `cascade_pw_click` - Click web elements
- `cascade_pw_fill` - Fill form fields
- `cascade_pw_press` - Press keys
- `cascade_pw_select_option` - Select dropdown options
- `cascade_pw_eval` - Execute JavaScript
- `cascade_pw_eval_on_selector` - Evaluate on elements
- `cascade_pw_list_frames` - List iframes
- `cascade_pw_get_cookies` - Get cookies

### API Tools (2)
- `cascade_web_search` - Search the web
- `cascade_call_http_api` - HTTP API requests

### Sandbox Tools (1)
- `cascade_execute_sandbox_skill` - Python code execution

### A2A Tools (3) - Requires opt-in
- `cascade_run_explorer` - Learn applications
- `cascade_run_worker` - Execute tasks
- `cascade_run_orchestrator` - Coordinate workflows

## Configuration

### Full Configuration Options

```json5
{
  plugins: {
    entries: {
      openclaw-cascade-plugin: {
        enabled: true,
        config: {
          // Required
          cascadeGrpcEndpoint: "localhost:50051",
          
          // Optional - Python
          cascadePythonPath: "/usr/bin/python3",
          
          // Optional - Firestore for skills
          firestoreProjectId: "your-project",
          firestoreCredentialsPath: "/path/to/creds.json",
          
          // Optional - Web automation
          headless: false,
          actionTimeoutMs: 8000,
          
          // Optional - A2A (explicit opt-in)
          enableA2A: true,
          allowedAgents: ["explorer", "worker", "orchestrator"],
          requireAgentConfirmation: true,
          
          // Optional - Debugging
          verbose: false,
          
          // Optional - Screenshots
          screenshotMode: "auto", // "embed", "disk", or "auto"
          screenshotDir: "~/.openclaw/screenshots"
        }
      }
    }
  }
}
```

## Development

### Setup

```bash
cd openclaw-plugin
npm install
npm test
npm run build
```

### Running Tests

```bash
# Run all tests
npm test

# Run specific test file
npm test -- --testNamePattern="Desktop Automation"

# Run with coverage
npm run test:coverage
```

### Project Structure

```
openclaw-plugin/
├── src/
│   ├── tools/
│   │   ├── desktop-automation.ts    # 9 desktop tools
│   │   ├── web-automation.ts        # 15 web tools
│   │   ├── api-tools.ts             # 2 API tools
│   │   ├── sandbox-tools.ts         # 1 sandbox tool
│   │   ├── a2a-tools.ts             # 3 A2A tools
│   │   ├── tool-registry.ts         # Tool management
│   │   └── response-helpers.ts      # Response formatting
│   ├── python-manager.ts            # Python detection/installation
│   ├── cascade-client.ts            # MCP client
│   ├── a2a-client.ts                # Agent communication
│   ├── config.ts                    # Configuration management
│   ├── types/                       # TypeScript types
│   └── test-utils/                  # Testing utilities
├── tests/                           # Test files
├── openclaw.plugin.json             # Plugin manifest
└── package.json                     # NPM package
```

## Contributing

We welcome contributions! Please see [CONTRIBUTING.md](./CONTRIBUTING.md) for guidelines.

### Areas for Contribution

- 🐛 Bug fixes
- ✨ New tools
- 📚 Documentation improvements
- 🧪 Additional tests
- 🌍 Platform support (macOS, Linux)

## License

MIT License - see [LICENSE](./LICENSE) for details.

## Support

- 📖 [Documentation](./docs/)
- 🐛 [Issue Tracker](https://github.com/yourusername/cascade/issues)
- 💬 [Discussions](https://github.com/yourusername/cascade/discussions)

## Acknowledgments

- Built with [OpenClaw](https://openclaw.ai)
- Desktop automation powered by [FlaUI](https://github.com/FlaUI/FlaUI) (C#)
- Web automation powered by [Playwright](https://playwright.dev)
- Skills stored in [Firebase](https://firebase.google.com)

---

**Made with ❤️ for the OpenClaw community**

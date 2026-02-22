# OpenClaw Integration Guide

Complete guide for integrating Cascade with OpenClaw for conversational desktop automation.

## Table of Contents

1. [Overview](#overview)
2. [Installation](#installation)
3. [Configuration](#configuration)
4. [Basic Usage](#basic-usage)
5. [Advanced Configuration](#advanced-configuration)
6. [Agent Communication](#agent-communication)
7. [Troubleshooting](#troubleshooting)

## Overview

Cascade integrates with OpenClaw to provide natural language control over desktop and web applications. The plugin adds 29 tools to OpenClaw:

- **9 Desktop tools** - Control Windows applications
- **15 Web tools** - Browser automation with Playwright
- **2 API tools** - Web search and HTTP requests
- **1 Sandbox tool** - Python code execution
- **3 A2A tools** - Agent communication (opt-in)

## Installation

### Step 1: Install OpenClaw

```bash
# macOS/Linux
curl -fsSL https://openclaw.ai/install.sh | bash

# Windows (PowerShell)
iwr -useb https://openclaw.ai/install.ps1 | iex
```

Verify installation:
```bash
openclaw --version
```

### Step 2: Install Cascade Plugin

```bash
openclaw plugins install openclaw-cascade-plugin
```

### Step 3: Clone and Start Cascade Body

```bash
git clone https://github.com/yourusername/cascade.git
cd cascade

# Start the C# Body (required for Windows automation)
dotnet run --project src/Body/Body.csproj
```

The Body will start on port 50051 by default.

### Step 4: Verify Python (Optional)

Cascade auto-detects Python, but you can specify a path:

```bash
# Check Python version
python3 --version  # Should be 3.10+

# Or install Python 3.12 if needed
# Windows: Download from python.org
# macOS: brew install python@3.12
# Linux: sudo apt install python3.12
```

## Configuration

### Basic Configuration

OpenClaw may block postinstall auto-editing for security. If the install reports missing `cascadeGrpcEndpoint`, add the plugin entry manually (see below). The entry key must match the manifest id (`openclaw-cascade-plugin`).

If `mcp_server` is not found, set `cascadePythonModulePath` to your Cascade repo's `python/` directory.

If you previously installed `cascade` (v1.0.0), uninstall it and reinstall v1.0.3:
```bash
openclaw plugins uninstall cascade
openclaw plugins install openclaw-cascade-plugin@1.0.3
```

For one-command install (auto-config + PYTHONPATH):
```bash
export CASCADE_REPO_PATH=/path/to/cascade
openclaw plugins install openclaw-cascade-plugin@1.0.3
```



```json5
{
  plugins: {
    enabled: true,
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

### Restart OpenClaw

```bash
openclaw restart
```

### Verify Installation

```bash
# Check plugin status
openclaw plugins list
openclaw cascade:status

# List available tools
openclaw cascade:tools
```

## Basic Usage

### Desktop Automation

```bash
# Start an application
openclaw "Open Calculator"

# Interact with UI elements
openclaw "Click the 5 button in Calculator"
openclaw "Type 25 * 4 into Calculator"
openclaw "Click the equals button"

# Take screenshots
openclaw "Take a screenshot of the current window"
```

### Web Automation

```bash
# Navigate and interact
openclaw "Navigate to google.com"
openclaw "Type 'OpenClaw' into the search box"
openclaw "Press Enter"
openclaw "Click the first result"

# Get information
openclaw "Get the page title"
openclaw "List all links on the current page"
```

### Semantic Tree

Before interacting with complex applications, get the UI structure:

```bash
openclaw "Get the semantic tree of the current window"
```

This shows all available elements with their IDs, making automation easier.

## Advanced Configuration

### Firestore Integration

For skill learning and persistence:

```json5
{
  plugins: {
    entries: {
      openclaw-cascade-plugin: {
        config: {
          cascadeGrpcEndpoint: "localhost:50051",
          firestoreProjectId: "your-project-id",
          firestoreCredentialsPath: "$HOME/.config/gcloud/credentials.json"
        }
      }
    }
  }
}
```

### Custom Python Path

If Python isn't in your PATH:

```json5
{
  plugins: {
    entries: {
      openclaw-cascade-plugin: {
        config: {
          cascadePythonPath: "/usr/local/bin/python3.12"
        }
      }
    }
  }
}
```

### Web Automation Settings

```json5
{
  plugins: {
    entries: {
      openclaw-cascade-plugin: {
        config: {
          // Run browsers in headless mode (no visible window)
          headless: true,
          
          // Increase action timeout for slow apps
          actionTimeoutMs: 15000
        }
      }
    }
  }
}
```

### Screenshot Handling

```json5
{
  plugins: {
    entries: {
      openclaw-cascade-plugin: {
        config: {
          // Mode: "embed", "disk", or "auto"
          // embed: Always embed in response
          // disk: Always save to disk
          // auto: Embed if <4MB, save to disk if larger
          screenshotMode: "auto",
          
          // Custom screenshot directory
          screenshotDir: "~/Documents/cascade-screenshots"
        }
      }
    }
  }
}
```

### Verbose Mode

For debugging, enable verbose error messages:

```json5
{
  plugins: {
    entries: {
      openclaw-cascade-plugin: {
        config: {
          verbose: true
        }
      }
    }
  }
}
```

This includes stack traces and raw error details in error responses.

## Agent Communication (A2A)

**⚠️ Explicit Opt-In Required**

Agent-to-Agent communication allows OpenClaw to call Cascade's Explorer, Worker, and Orchestrator agents.

### Enable A2A

```json5
{
  plugins: {
    entries: {
      openclaw-cascade-plugin: {
        config: {
          cascadeGrpcEndpoint: "localhost:50051",
          enableA2A: true,  // Required!
          allowedAgents: ["explorer", "worker", "orchestrator"],
          requireAgentConfirmation: true  // Ask before calling agents
        }
      }
    }
  }
}
```

### Usage

```bash
# Learn a new application
openclaw "Explore how to use Excel"

# Execute a learned skill
openclaw "Run my expense report skill"

# Complex task coordination
openclaw "Process all the invoices in the Downloads folder"
```

### Security

A2A is disabled by default and requires explicit opt-in. When enabled:

- You can restrict which agents OpenClaw can call
- You can require confirmation before each agent invocation
- All communication goes through your local gRPC connection
- No external API calls for agent communication

## Troubleshooting

### "Plugin not found"

```bash
# Reinstall the plugin
openclaw plugins uninstall cascade
openclaw plugins install openclaw-cascade-plugin
openclaw restart
```

### "Cannot connect to Cascade Body"

1. Verify Body is running:
   ```bash
   # Check if port 50051 is listening
   netstat -an | grep 50051
   ```

2. Verify configuration:
   ```bash
openclaw cascade:status

> Note: CLI commands remain `cascade:status` and `cascade:tools` even though the plugin id is `openclaw-cascade-plugin`.
   ```

3. Check firewall settings (port 50051)

### "Python not found"

Cascade will auto-install Python if not found. To manually specify:

```json5
{
  plugins: {
    entries: {
      openclaw-cascade-plugin: {
        config: {
          cascadePythonPath: "C:\\Python312\\python.exe"
        }
      }
    }
  }
}
```

### "Element not found"

1. Get the semantic tree first:
   ```bash
   openclaw "Get the semantic tree"
   ```

2. Use the exact element ID or name from the tree
3. Check if the element is actually visible

### "Screenshot too large"

Screenshots >4MB are automatically saved to disk instead of embedded. Check:
- `~/.openclaw/screenshots/` (default)
- Or your custom `screenshotDir`

### Getting Help

```bash
# Check plugin help
openclaw help cascade

# Check status
openclaw cascade:status

# List all tools
openclaw cascade:tools

# Enable verbose mode for debugging
# (Add "verbose: true" to config)
```

## Examples

### Example 1: Calculator Automation

```bash
openclaw "Open Calculator"
openclaw "Click the 1 button"
openclaw "Click the plus button" 
openclaw "Click the 2 button"
openclaw "Click the equals button"
openclaw "Take a screenshot"
```

### Example 2: Web Form Filling

```bash
openclaw "Navigate to example.com/contact"
openclaw "Fill the name field with 'John Doe'"
openclaw "Fill the email field with 'john@example.com'"
openclaw "Fill the message field with 'Hello!'"
openclaw "Click the submit button"
openclaw "Wait for the success page"
```

### Example 3: File Processing

```bash
openclaw "Open Excel"
openclaw "Click the Open button"
openclaw "Type 'C:\\Documents\\data.xlsx'"
openclaw "Press Enter"
openclaw "Get the semantic tree to see the sheet structure"
```

## Next Steps

- Read the [Skills Reference](./skills-reference.md) to learn about OpenClaw skills
- Check [Plugin Development](./plugin-development.md) if you want to contribute
- See [Troubleshooting](./troubleshooting.md) for more help

---

**Need more help?** Open an issue on GitHub or join our Discord community.

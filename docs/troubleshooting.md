# Troubleshooting Guide

Common issues and solutions for Cascade OpenClaw Plugin.

## Installation Issues

### Plugin Installation Fails

**Error:** `Failed to install openclaw-cascade-plugin`

**Solutions:**
1. Check Node.js version: `node --version` (need 18+)
2. Clear npm cache: `npm cache clean --force`
3. Try direct install: 
   ```bash
   cd ~/.openclaw/extensions
   npm install openclaw-cascade-plugin
   ```

### OpenClaw Not Found

**Error:** `openclaw: command not found`

**Solutions:**
1. Reinstall OpenClaw:
   ```bash
   curl -fsSL https://openclaw.ai/install.sh | bash
   ```
2. Add to PATH:
   ```bash
   # Add to ~/.bashrc or ~/.zshrc
   export PATH="$HOME/.openclaw/bin:$PATH"
   ```

## Connection Issues

### Cannot Connect to Cascade Body

**Error:** `Error: 14 UNAVAILABLE: Connection refused`

**Solutions:**
1. Verify Body is running:

---

### Plugin ID Mismatch

**Error:** `plugin id mismatch` or `plugin not found: openclaw-cascade-plugin`

**Cause:** Config entry key doesn't match the plugin manifest id.

**Fix:**
1. Remove any legacy `cascade` entry from your config.
2. Ensure your config uses `openclaw-cascade-plugin`:
   ```json5
   {
     plugins: {
       entries: {
         openclaw-cascade-plugin: {
           enabled: true,
           config: { cascadeGrpcEndpoint: "localhost:50051" }
         }
       }
     }
   }
   ```
3. Reinstall the plugin (v1.0.3+):
   ```bash
   openclaw plugins uninstall cascade
   openclaw plugins install openclaw-cascade-plugin@1.0.3
   ```

---
   ```bash
   # Windows
   netstat -an | findstr 50051
   
   # macOS/Linux
   netstat -an | grep 50051
   ```

2. Check firewall settings (allow port 50051)

3. Verify configuration:
   ```json5
   {
     plugins: {
        entries: {
          openclaw-cascade-plugin: {
            config: {
              cascadeGrpcEndpoint: "localhost:50051"
            }
          }
        }
     }
   }
   ```

4. Restart both Body and OpenClaw

### Python Module Not Found

**Error:** `No module named 'mcp_server'`

**Solutions:**
1. Set module path in config:
   ```json5
   {
     plugins: {
       entries: {
         openclaw-cascade-plugin: {
           config: {
             cascadePythonModulePath: "/path/to/cascade/python"
           }
         }
       }
     }
   }
   ```
2. Or set env var before running OpenClaw:
   ```bash
   export CASCADE_REPO_PATH=/path/to/cascade
   ```

---

### Python Not Found

**Error:** `Python 3.10+ not found`

**Solutions:**
1. Install Python 3.12:
   ```bash
   # Windows: Download from python.org
   # macOS:
   brew install python@3.12
   
   # Ubuntu/Debian:
   sudo apt install python3.12 python3.12-venv
   ```

2. Specify Python path in config:
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

## Tool Execution Issues

### Element Not Found

**Error:** `Element not found: button1`

**Solutions:**
1. Get semantic tree first:
   ```bash
   openclaw "Get the semantic tree"
   ```

2. Use correct selector format:
   ```bash
   # Use ID
   openclaw "Click the element with id 'button-5'"
   
   # Use name
   openclaw "Click the Calculate button"
   ```

3. Wait for element to appear:
   ```bash
   openclaw "Wait for the loading spinner to disappear"
   ```

### Screenshot Too Large

**Behavior:** Screenshot saved to disk instead of embedded

**Explanation:** Screenshots >4MB are automatically saved to disk to avoid message size limits.

**Solutions:**
1. Check screenshot location:
   - Default: `~/.openclaw/screenshots/`
   - Or your custom `screenshotDir`

2. Use screenshot mode:
   ```json5
   {
     plugins: {
       entries: {
         openclaw-cascade-plugin: {
           config: {
             screenshotMode: "embed"  // Always embed
           }
         }
       }
     }
   }
   ```

### Web Automation Timeout

**Error:** `Timeout 8000ms exceeded`

**Solutions:**
1. Increase timeout:
   ```json5
   {
     plugins: {
       entries: {
         openclaw-cascade-plugin: {
           config: {
             actionTimeoutMs: 15000
           }
         }
       }
     }
   }
   ```

2. Check if element is visible:
   ```bash
   openclaw "Check if the submit button is visible"
   ```

## A2A (Agent) Issues

### A2A Not Working

**Error:** `A2A is not enabled`

**Solutions:**
1. Enable A2A in config (requires explicit opt-in):
   ```json5
   {
     plugins: {
       entries: {
         openclaw-cascade-plugin: {
           config: {
             enableA2A: true,
             allowedAgents: ["explorer", "worker", "orchestrator"]
           }
         }
       }
     }
   }
   ```

2. Restart OpenClaw after config change

### Agent Confirmation Not Appearing

**Error:** `Agent executed without confirmation`

**Solutions:**
1. Enable confirmation:
   ```json5
   {
     plugins: {
       entries: {
         openclaw-cascade-plugin: {
           config: {
             requireAgentConfirmation: true
           }
         }
       }
     }
   }
   ```

## Configuration Issues

### Environment Variables Not Expanding

**Error:** `$HOME not expanded`

**Solutions:**
1. Use correct syntax:
   - Unix: `$HOME` or `${HOME}`
   - Windows: `%USERPROFILE%`

2. Example:
   ```json5
   {
     plugins: {
       entries: {
         openclaw-cascade-plugin: {
           config: {
             firestoreCredentialsPath: "$HOME/.config/gcloud/creds.json"
           }
         }
       }
     }
   }
   ```

### Configuration Validation Failed

**Error:** `cascadeGrpcEndpoint is required`

**Solutions:**
1. Check configuration file location:
   - Linux/macOS: `~/.openclaw/config.json`
   - Windows: `%USERPROFILE%\.openclaw\config.json`

2. Validate JSON syntax:
   ```bash
   # Linux/macOS
   cat ~/.openclaw/config.json | python3 -m json.tool
   
   # Windows
   type %USERPROFILE%\.openclaw\config.json | python -m json.tool
   ```

## Performance Issues

### Slow Response Times

**Causes & Solutions:**

1. **Python auto-installation**
   - Solution: Pre-install Python and set `cascadePythonPath`

2. **Large screenshots**
   - Solution: Use `screenshotMode: "disk"`

3. **Firestore latency**
   - Solution: Use local emulator for development:
     ```bash
     firebase emulators:start --only firestore
     ```

### High Memory Usage

**Solutions:**
1. Limit concurrent operations
2. Use headless mode for web automation:
   ```json5
   {
     plugins: {
       entries: {
         openclaw-cascade-plugin: {
           config: {
             headless: true
           }
         }
       }
     }
   }
   ```

## Debug Mode

### Enable Verbose Logging

Add to configuration:
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

This includes:
- Stack traces in error messages
- Raw API responses
- Timing information

### Check Plugin Status

```bash
openclaw cascade:status
```

Expected output:
```
Cascade Plugin Status:
  Connected: true
  Tools: 29
  Python: /usr/bin/python3
  gRPC: localhost:50051
  A2A: disabled
```

### List Available Tools

```bash
openclaw cascade:tools
```

## Getting More Help

### Check Logs

```bash
# OpenClaw logs
tail -f ~/.openclaw/logs/openclaw.log

# Plugin logs (if verbose mode enabled)
tail -f ~/.openclaw/logs/cascade.log
```

### Run Diagnostic Commands

```bash
# Test connection
openclaw cascade:status

# Test tools
openclaw "Take a screenshot"

# Check Python
openclaw "Get the semantic tree"
```

### Report Issues

When reporting issues, include:
1. OpenClaw version: `openclaw --version`
2. Plugin version: `openclaw plugins info cascade`
3. Configuration (remove sensitive data)
4. Error message
5. Steps to reproduce

Open an issue at: https://github.com/yourusername/cascade/issues

---

**Still stuck?** Join our Discord community for real-time help!

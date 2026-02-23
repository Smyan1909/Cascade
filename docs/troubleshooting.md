# Troubleshooting

Common issues and fixes for the Cascade OpenClaw plugin.

## Install & Setup
**OpenClaw not found**
- Reinstall OpenClaw: `curl -fsSL https://openclaw.ai/install.sh | bash`
- Ensure `~/.openclaw/bin` is on your PATH.

**Plugin install fails**
- Verify Node.js 18+: `node --version`
- Reinstall: `openclaw plugins uninstall openclaw-cascade-plugin && openclaw plugins install openclaw-cascade-plugin`

**Missing `cascadeGrpcEndpoint`**
Add this to `~/.openclaw/openclaw.json`:
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

## Connection Issues
**Cannot connect to Cascade Body**
- Ensure Body is running: `dotnet run --project src/Body/Body.csproj`
- Verify port `50051` is listening.
- Check firewall settings.

**Python module not found (`mcp_server`)**
Set `cascadePythonModulePath` to the repo `python/` directory:
```json5
{
  plugins: {
    entries: {
      openclaw-cascade-plugin: {
        config: { cascadePythonModulePath: "C:/path/to/cascade/python" }
      }
    }
  }
}
```

## UI Automation Issues
**Element not found**
- Fetch the semantic tree first: `openclaw "Get the semantic tree"`.
- Use `element_id` or `automation_id` from the tree for precise targeting.
- If the UI changes, refresh the tree and retry.

**Typing replaces existing text**
- Use `text_entry_mode: "APPEND"` with `cascade_type_text` to append instead of replace.

**Send keys not working**
- Ensure the element can receive focus (`cascade_focus_element`).
- Use canonical chords like `CTRL+S`, `ALT+F4`, `ENTER`.

## Screenshots
**Screenshot too large**
- Images >4MB are saved to disk when `screenshotMode` is `auto`.
- Default location: `~/.openclaw/screenshots`.

## Agent (A2A) Issues
**A2A tools not available**
- Enable A2A in config and allow desired agents.
- Restart OpenClaw after changing config.

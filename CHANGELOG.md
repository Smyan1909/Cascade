# Changelog

All notable changes to the Cascade OpenClaw Plugin will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.3] - 2024-02-19

### Added
- Postinstall script to auto-update `~/.openclaw/openclaw.json` with plugin entry.
- `cascadePythonModulePath` support for locating `mcp_server` modules.

### Fixed
- Automatically pick config from `openclaw-cascade-plugin` or legacy `cascade` entries.

## [1.0.2] - 2024-02-19

### Fixed
- Load config from `openclaw-cascade-plugin` entries key (with `cascade` fallback).
- Improve error message when plugin config is missing.

## [1.0.1] - 2024-02-19

### Fixed
- Align plugin manifest `id` with published package name (`openclaw-cascade-plugin`).
- Update configuration docs to use `plugins.entries.openclaw-cascade-plugin`.

### Updated
- README installation instructions and config examples.
- Integration guide and troubleshooting references.

## [1.0.0] - 2024-02-18

### Added

#### Core Features
- **OpenClaw Integration** - Full plugin integration with OpenClaw framework
- **29 Tools** - Comprehensive tool suite for desktop and web automation
  - 9 Desktop automation tools
  - 15 Web automation tools (Playwright)
  - 2 API tools
  - 1 Sandbox execution tool
  - 3 A2A (Agent-to-Agent) communication tools

#### Desktop Automation Tools
- `cascade_click_element` - Click UI elements by selector
- `cascade_type_text` - Type text into form fields
- `cascade_hover_element` - Hover over elements
- `cascade_focus_element` - Focus on input elements
- `cascade_scroll_element` - Scroll scrollable elements
- `cascade_wait_visible` - Wait for element visibility
- `cascade_get_semantic_tree` - Extract UI structure
- `cascade_get_screenshot` - Capture screenshots with intelligent handling
- `cascade_start_app` - Launch applications

#### Web Automation Tools
- `cascade_pw_goto` - Navigate to URLs
- `cascade_pw_back` - Navigate back in browser history
- `cascade_pw_forward` - Navigate forward in browser history
- `cascade_pw_reload` - Reload current page
- `cascade_pw_wait_for_url` - Wait for URL pattern match
- `cascade_pw_locator_count` - Count matching elements
- `cascade_pw_locator_text` - Get element text content
- `cascade_pw_click` - Click web elements
- `cascade_pw_fill` - Fill form fields
- `cascade_pw_press` - Press keyboard keys
- `cascade_pw_select_option` - Select dropdown options
- `cascade_pw_eval` - Execute JavaScript in page context
- `cascade_pw_eval_on_selector` - Execute JavaScript on specific element
- `cascade_pw_list_frames` - List all frames/iframes
- `cascade_pw_get_cookies` - Get browser cookies

#### API Tools
- `cascade_web_search` - Search the web
- `cascade_call_http_api` - Make HTTP API requests

#### Sandbox Tools
- `cascade_execute_sandbox_skill` - Execute Python in isolated sandbox

#### A2A Tools (Agent Communication)
- `cascade_run_explorer` - Launch Explorer agent to learn applications
- `cascade_run_worker` - Execute tasks with Worker agent
- `cascade_run_orchestrator` - Coordinate multi-step tasks

#### Infrastructure
- **Python Manager** - Auto-detect and install Python 3.10+
- **MCP Client** - JSON-RPC communication with Cascade Body
- **A2A Client** - Agent-to-Agent communication
- **Configuration System** - Environment variable expansion, validation
- **Error Handling** - Friendly error messages with suggestions
- **Screenshot Management** - Auto-embed or save to disk based on size

#### Testing
- 74+ unit tests with 80%+ coverage
- Comprehensive test utilities and mocks
- Jest testing framework configuration

#### Documentation
- Comprehensive README with quick start
- Integration guide
- Contributing guidelines
- Troubleshooting guide
- API reference

### Configuration Options
- `cascadeGrpcEndpoint` - gRPC connection endpoint
- `cascadePythonPath` - Custom Python path
- `firestoreProjectId` - Firebase project for skills
- `firestoreCredentialsPath` - Firebase credentials
- `headless` - Run browsers headlessly
- `actionTimeoutMs` - UI action timeout
- `enableA2A` - Enable agent communication (opt-in)
- `allowedAgents` - Restrict callable agents
- `requireAgentConfirmation` - Confirm before calling agents
- `verbose` - Debug error messages
- `screenshotMode` - Screenshot handling mode
- `screenshotDir` - Custom screenshot directory

### Security Features
- Explicit opt-in required for A2A
- Agent allowlisting
- User confirmation dialogs
- No external API calls for agent communication
- Local-only gRPC communication

### Developer Experience
- TypeScript with strict mode
- Full type exports
- Clear error messages with suggestions
- Comprehensive logging
- CLI commands for status and tools

## [Unreleased]

### Planned

#### Features
- [ ] macOS and Linux support
- [ ] More desktop automation tools (window management, clipboard)
- [ ] Advanced screenshot features (regions, full page)
- [ ] Skill learning improvements
- [ ] Webhook support for agent notifications

#### Improvements
- [ ] Performance optimizations
- [ ] Better error recovery
- [ ] Enhanced logging
- [ ] More configuration options

#### Documentation
- [ ] Video tutorials
- [ ] Example projects
- [ ] Best practices guide
- [ ] Migration guides

---

## Release History

### Versioning Scheme

We follow [Semantic Versioning](https://semver.org/):

- **MAJOR** - Breaking changes
- **MINOR** - New features (backward compatible)
- **PATCH** - Bug fixes (backward compatible)

### Support Policy

- Latest major version: Full support
- Previous major version: Critical bug fixes only
- Older versions: No support

---

## How to Upgrade

### From 0.x to 1.0

1. Update OpenClaw plugin:
   ```bash
   openclaw plugins update cascade
   ```

2. Update configuration (see [Integration Guide](./docs/openclaw-integration.md)):
   - Rename old config keys if any
   - Add new optional configurations

3. Test your workflows

4. Review [Breaking Changes](#breaking-changes) section if upgrading across major versions

### Breaking Changes

#### 1.0.0
- Initial stable release
- No breaking changes (first release)

---

## Contributors

Thank you to all contributors who made this release possible!

### Core Team
- [Your Name](https://github.com/yourusername) - Lead Developer

### Contributors
- [Contributor 1](https://github.com/contributor1)
- [Contributor 2](https://github.com/contributor2)

---

## Feedback

Found a bug? Have a feature request? Open an issue on GitHub!

- 🐛 [Bug Reports](https://github.com/yourusername/cascade/issues/new?template=bug_report.md)
- ✨ [Feature Requests](https://github.com/yourusername/cascade/issues/new?template=feature_request.md)

---

**Full Changelog**: [v1.0.0](https://github.com/yourusername/cascade/releases/tag/v1.0.0)

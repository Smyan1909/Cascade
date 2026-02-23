# Changelog

All notable changes to Cascade are documented here. Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [Unreleased]

## [1.1.0] - 2026-02-22
### Added
- Expanded UIA selectors: `element_id`, `automation_id`, `class_name`, `framework_id`, `help_text`.
- New UIA actions: toggle, expand/collapse, select, range value, send keys, window state, move/resize.
- Text entry modes for append vs replace.

### Documentation
- Simplified and consolidated open-source docs (README, integration, troubleshooting, contributing).

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

## [1.0.0] - 2024-02-18
### Added
- Initial OpenClaw plugin release with desktop, web, API, sandbox, and A2A tools.

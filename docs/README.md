# Cascade Documentation

This directory contains the core documentation for Cascade. Start here if you are evaluating or open-sourcing the project.

## Quick Links
- `00-overview.md` — architecture, data boundaries, and contracts
- `openclaw-integration.md` — install + configure the OpenClaw plugin
- `troubleshooting.md` — common issues and fixes
- `testing.md` — test commands and expectations

## Architecture & Modules
- `phase1-grpc.md` — gRPC contract and testing plan
- `phase2-csharp-body.md` — C# Body server design
- `phase3-python-sdk.md` — Python client SDK design
- `phase4-explorer.md` — Explorer agent persistence and workflows
- `phase5-worker.md` — Worker execution lifecycle
- `phase6-orchestrator.md` — Orchestrator design
- `phase7-deploy-compose.md` — deployment and compose runbook
- `modules/grpc.md` — contract, codegen, compatibility
- `modules/csharp-body.md` — providers, vision/OCR, tests
- `modules/python-agents.md` — Brain structure + agent loop
- `modules/mcp-server.md` — tool registry and MCP server

## FAQ
**Q: Do I need Firestore to use Cascade?**
A: No. Firestore is only required for skill learning and persistence. Basic OpenClaw automation works without it.

**Q: Is the automation local?**
A: Yes. UI automation runs locally via `localhost` gRPC unless you configure a remote endpoint.

**Q: What does “A2A” mean?**
A: Agent-to-Agent communication. It allows OpenClaw to invoke Explorer/Worker/Orchestrator and is opt-in.

**Q: What platforms are supported?**
A: Desktop automation is Windows-only. Web automation is available via `platform_source: WEB` using the same action tools.

## Roadmap (short)
- Broader UIA action coverage and smarter selectors
- More agent examples and verification workflows
- Expanded docs and tutorials

## How to Contribute
See `../CONTRIBUTING.md` for setup, tests, and contribution workflow.

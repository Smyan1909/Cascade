# agents/tester

Test-focused agent utilities.

This package exists to run agent behaviors in a controlled way (prompts + harness),
so you can validate the agent loop and tool wiring without running a full Explorer/Worker workflow.

## Entrypoint

- CLI: `python -m agents.tester.cli ...` (see `cli.py` for flags)

## Modules

- `test_agent.py`: Harness that drives an agent run and reports results.
- `prompts.py`: Prompts used by the tester harness.
- `cli.py`: CLI wrapper around the harness.

## When to use

- You changed the ReAct loop (`agents/core/autonomous_agent.py`) or tool registration (`python/mcp_server/*`) and want a fast sanity check.
- You want to reproduce a prompt/tool regression deterministically (as much as possible) before touching full app automation.



# Cascade Agents

Autonomous agents for UI automation with hierarchical skill learning.

For the repo-wide architecture and dev rules, see `AGENTS.md`.

## Quick Start

```powershell
# Prerequisites
firebase emulators:start --only firestore  # Terminal 1
dotnet run --project src\Body\Body.csproj   # Terminal 2

# Run agents
python -m agents.explorer.cli --app-name "calc" --instructions-file "..\instructions\instr_calculator.json"
python -m agents.worker.cli --task "Calculate 25 * 4" --app-name "calc"
python -m agents.orchestrator.cli --goal "Calculate 123 + 456"
```

## Architecture

```
agents/
├── core/                    # Shared agent infrastructure
│   ├── autonomous_agent.py  # Base ReAct agent with LangGraph
│   └── verify_prompts.py    # Verification prompts
├── explorer/                # Discovers UI and creates skills
│   ├── autonomous_explorer.py
│   ├── prompts_autonomous.py
│   └── skill_map.py         # Skill schema
├── worker/                  # Executes tasks using skills
│   ├── autonomous_worker.py
│   ├── graph.py             # StepExecutor
│   └── skill_loader.py
└── orchestrator/            # Coordinates high-level goals
    └── autonomous_orchestrator.py
```

### Orchestrator note (two implementations)

There are **two orchestrators** in this repo:

- `agents/orchestrator/`: **Autonomous orchestrator** (LLM tool-driven, rapid experimentation)
- `python/orchestrator/`: **Deterministic orchestrator** (LangGraph + A2A dispatch, predictable/testable)

## Skill Types

| Type | Steps | Use Case |
|------|-------|----------|
| `primitive` | 1 | Single button click |
| `composite` | Multiple | Navigation, form filling |

## Key Features

- **Infinite iterations**: Agents run until they signal completion
- **Hierarchical skills**: Primitives + composites with `composed_of` dependencies
- **Application-agnostic**: Works with any Windows desktop app
- **Window caching**: Fast UWP app support (Calculator, etc.)

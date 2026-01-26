# agents/core

Shared infrastructure used by the Python Brain agents (Explorer/Worker/Orchestrator).

If you are trying to understand Cascade end-to-end, start with:
- Repo overview: `AGENTS.md`
- Design contracts: `docs/00-overview.md`, `docs/phase4-explorer.md`, `docs/phase5-worker.md`, `docs/phase6-orchestrator.md`

## What’s in here

- `autonomous_agent.py`: **ReAct-style agent loop** built on LangGraph + LangChain tools.
  - Converts MCP `ToolRegistry` tools into LangChain tools
  - Runs a streamed loop collecting tool calls and model responses
  - Optionally runs an **LLM-based completion evaluator** when `enable_verification=True`
- `planning_agent.py`: Creates/refines human-readable plans and supports an approval loop (used by autonomous orchestrator flows).
- `intent_classifier.py`: Classifies whether a user’s follow-up is a “new goal” vs “additional instructions”.
- `summarization.py`: Summarizes conversation history to keep prompts bounded.
- `verify_prompts.py`: Prompt templates used by verification/evaluation steps.

## The AutonomousAgent loop (how execution works)

`AutonomousAgent.run(task, context=None)`:
- Builds a message list starting from the user task (+ optional JSON context).
- Streams LangGraph events; whenever the model emits tool calls, the tools are executed and their results are fed back into the loop.
- Tracks “no tool call” turns; if the model stops calling tools, an **evaluator** may decide:
  - **COMPLETE**: stop and return `AgentStatus.COMPLETED`
  - **INCOMPLETE**: inject a “Continue…” prompt to force progress
  - **STUCK**: stop to avoid infinite loops

Key knobs:
- `AgentConfig.max_iterations`: iteration/recursion limit (safety bound)
- `AgentConfig.enable_verification`: whether to use the evaluator to confirm completion
- `AgentConfig.thread_id`: LangGraph checkpoint thread id (useful for tracing)

## Developer notes

- **Tooling boundary**: add side effects as tools (click/type/firestore write) rather than hidden code paths.
- **Avoid infinite loops**: handle “no progress” by bounding retries/iterations and by returning actionable errors.
- **Keep prompts stable**: behavior changes should be driven by state and tools, not prompt churn.



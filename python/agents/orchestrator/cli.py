"""CLI entrypoint for Orchestrator agent (Autonomous mode only)."""

import argparse
from dataclasses import dataclass, field
from typing import Dict, List, Optional

from cascade_client.auth.context import CascadeContext
from cascade_client.grpc_client import CascadeGrpcClient
import os 
from pathlib import Path
from agents.core import classify_next_input_intent, summarize_conversation

try:
    from dotenv import load_dotenv
    env_path = Path(__file__).parents[3] / ".env"
    load_dotenv(env_path)
    print(f"[CLI] Loaded env from {env_path} (via dotenv)")
except ImportError:
    print("[CLI] python-dotenv not found, parsing .env manually...")
    env_path = Path(__file__).parents[3] / ".env"
    if env_path.exists():
        with open(env_path, "r") as f:
            for line in f:
                line = line.strip()
                if not line or line.startswith("#"):
                    continue
                if "=" in line:
                    key, value = line.split("=", 1)
                    key = key.strip()
                    value = value.strip().strip("'").strip('"')
                    os.environ[key] = value
        print(f"[CLI] Loaded env from {env_path} (manual parse)")

@dataclass
class OrchestratorConfig:
    """Configuration for the Autonomous Orchestrator."""
    max_orchestrator_iterations: int = 30
    max_explorer_iterations: int = 100
    max_worker_iterations: int = 50
    verbose: bool = True


def main():
    parser = argparse.ArgumentParser(description="Run Orchestrator agent (Autonomous)")
    parser.add_argument("--goal", required=True, help="High-level goal to accomplish")
    parser.add_argument("--grpc-endpoint", help="gRPC endpoint (host:port)")
    parser.add_argument("--max-iterations", type=int, default=30, help="Maximum orchestrator iterations")
    parser.add_argument("--max-explorer-iterations", type=int, default=100, help="Maximum Explorer iterations")
    parser.add_argument("--max-worker-iterations", type=int, default=50, help="Maximum Worker iterations")
    parser.add_argument("--quiet", action="store_true", help="Reduce output verbosity")
    parser.add_argument("--auto-approve", action="store_true", help="Skip plan approval step")
    args = parser.parse_args()

    from .autonomous_orchestrator import AutonomousOrchestrator

    context = CascadeContext.from_env()
    grpc_client = CascadeGrpcClient(endpoint=args.grpc_endpoint)

    config = OrchestratorConfig(
        max_orchestrator_iterations=args.max_iterations,
        max_explorer_iterations=args.max_explorer_iterations,
        max_worker_iterations=args.max_worker_iterations,
        verbose=not args.quiet,
    )

    print("[Orchestrator] Running in AUTONOMOUS mode")
    print(f"[Orchestrator] Goal: {args.goal}")
    print()

    orchestrator = AutonomousOrchestrator(context, grpc_client, config)

    current_goal = args.goal
    additional_instructions = ""
    summarized_conversation_history: Optional[str] = None
    raw_conversation_history: List[Dict[str, str]] = []

    def count_tokens(messages: List[Dict[str, str]]) -> int:
        # Rough heuristic: 4 chars ≈ 1 token
        text = "".join(str(msg.get("content", "")) for msg in messages)
        return len(text.encode("utf-8")) // 4

    while True:
        result = orchestrator.run(
            current_goal,
            additional_instructions=additional_instructions,
            summarized_conversation_history=summarized_conversation_history,
            raw_conversation_history=raw_conversation_history,
            auto_approve=args.auto_approve,
        )

        print("\n" + "=" * 60)
        print(f"Status: {result.status.value}")
        print(f"Iterations: {result.iterations}")
        print(f"Tool Calls: {len(result.tool_calls)}")
        print(f"Elapsed: {result.elapsed_seconds:.1f}s")
        if result.error:
            print(f"Error: {result.error}")
        print("=" * 60)
        print("\nFinal Response:")
        print(result.final_response)

        raw_conversation_history.append(
            {
                "role": "user",
                "content": current_goal
                + (("\n\nADDITIONAL_INSTRUCTIONS:\n" + additional_instructions) if additional_instructions else ""),
            }
        )
        raw_conversation_history.append({"role": "assistant", "content": result.final_response})

        if count_tokens(raw_conversation_history) > 4000:
            to_summarize: List[Dict[str, str]] = raw_conversation_history[-10:]
            if summarized_conversation_history:
                to_summarize = [{"role": "system", "content": summarized_conversation_history}] + to_summarize
            summarized_conversation_history = summarize_conversation(to_summarize)
            raw_conversation_history = raw_conversation_history[-10:]

        next_input = input("\nEnter next instruction/goal (blank to quit): ").strip()
        if not next_input:
            break

        decision = classify_next_input_intent(
            current_objective=current_goal,
            user_input=next_input,
            summarized_conversation_history=summarized_conversation_history,
        )
        if decision.intent == "new":
            current_goal = decision.normalized_text
            additional_instructions = ""
            print(f"\n[Orchestrator] New goal set: {current_goal}")
        else:
            additional_instructions = decision.normalized_text


if __name__ == "__main__":
    main()

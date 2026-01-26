"""CLI entrypoint for Worker agent (Autonomous mode only)."""

import argparse
import uuid
from pathlib import Path
from typing import Dict, List, Optional
from cascade_client.auth.context import CascadeContext
from cascade_client.grpc_client import CascadeGrpcClient

import os 
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

def main():
    parser = argparse.ArgumentParser(description="Run Worker agent (Autonomous)")
    parser.add_argument("--task", required=True, help="Task to execute")
    parser.add_argument("--app-name", help="Application name for context")
    parser.add_argument("--skill-id", help="Specific skill ID to use")
    parser.add_argument("--grpc-endpoint", help="gRPC endpoint (host:port)")
    parser.add_argument("--max-iterations", type=int, default=500, help="Maximum iterations (agent decides when done)")
    parser.add_argument("--auto-approve", action="store_true", help="Skip ALL approval prompts (full access)")
    args = parser.parse_args()

    from .autonomous_worker import AutonomousWorker
    from agents.core.autonomous_agent import AgentConfig
    
    context = CascadeContext.from_env()
    grpc_client = CascadeGrpcClient(endpoint=args.grpc_endpoint)
    
    config = AgentConfig(
        max_iterations=args.max_iterations,
        verbose=True,
    )
    
    print("[Worker] Running in AUTONOMOUS mode")
    worker = AutonomousWorker(context, grpc_client, config, auto_approve=args.auto_approve)

    current_task = args.task
    additional_context = ""
    summarized_conversation_history: Optional[str] = None
    raw_conversation_history: List[Dict[str, str]] = []

    def count_tokens(messages: List[Dict[str, str]]) -> int:
        # Rough heuristic: 4 chars ≈ 1 token
        text = "".join(str(msg.get("content", "")) for msg in messages)
        return len(text.encode("utf-8")) // 4

    while True:
        result = worker.execute(
            task=current_task,
            app_name=args.app_name,
            additional_context=additional_context,
            summarized_conversation_history=summarized_conversation_history,
            raw_conversation_history=raw_conversation_history,
        )

        print("\n" + "=" * 50)
        print(f"Status: {result.status.value}")
        print(f"Iterations: {result.iterations}")
        print(f"Tool Calls: {len(result.tool_calls)}")
        print(f"Elapsed: {result.elapsed_seconds:.1f}s")
        if result.error:
            print(f"Error: {result.error}")
        print("=" * 50)
        print("\nFinal Response:")
        print(result.final_response)

        # Persist conversation history for long-running workflows.
        raw_conversation_history.append({"role": "user", "content": current_task + ("\n\n" + additional_context if additional_context else "")})
        raw_conversation_history.append({"role": "assistant", "content": result.final_response})

        if count_tokens(raw_conversation_history) > 4000:
            # Summarize BEFORE trimming so we don't lose the dropped context.
            to_summarize: List[Dict[str, str]] = raw_conversation_history[-10:]
            if summarized_conversation_history:
                to_summarize = [{"role": "system", "content": summarized_conversation_history}] + to_summarize
            summarized_conversation_history = summarize_conversation(to_summarize)
            raw_conversation_history = raw_conversation_history[-10:]

        next_input = input("\nEnter next instruction/task (blank to quit): ").strip()
        if not next_input:
            break

        decision = classify_next_input_intent(
            current_objective=current_task,
            user_input=next_input,
            summarized_conversation_history=summarized_conversation_history,
        )

        if decision.intent == "new":
            current_task = decision.normalized_text
            additional_context = ""
        else:
            # Continuation: keep the same task; treat new input as extra context/refinement.
            additional_context = decision.normalized_text


if __name__ == "__main__":
    main()

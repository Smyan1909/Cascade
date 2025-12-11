"""CLI entrypoint for Orchestrator agent (Autonomous mode only)."""

import argparse
from dataclasses import dataclass, field
from typing import Optional

from cascade_client.auth.context import CascadeContext
from cascade_client.grpc_client import CascadeGrpcClient


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
    result = orchestrator.run(args.goal)

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


if __name__ == "__main__":
    main()

"""CLI entrypoint for Worker agent (Autonomous mode only)."""

import argparse
import uuid

from cascade_client.auth.context import CascadeContext
from cascade_client.grpc_client import CascadeGrpcClient


def main():
    parser = argparse.ArgumentParser(description="Run Worker agent (Autonomous)")
    parser.add_argument("--task", required=True, help="Task to execute")
    parser.add_argument("--app-name", help="Application name for context")
    parser.add_argument("--run-id", default=str(uuid.uuid4()), help="Run identifier")
    parser.add_argument("--skill-id", help="Specific skill ID to use")
    parser.add_argument("--grpc-endpoint", help="gRPC endpoint (host:port)")
    parser.add_argument("--max-iterations", type=int, default=50, help="Maximum iterations")
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
    worker = AutonomousWorker(context, grpc_client, config)
    result = worker.execute(
        task=args.task,
        app_name=args.app_name,
        run_id=args.run_id,
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


if __name__ == "__main__":
    main()

"""CLI entrypoint for Explorer agent."""

import argparse
import json
import uuid
import threading
import asyncio

from cascade_client.auth.context import CascadeContext
from cascade_client.a2a import AgentA2AClient
from cascade_client.grpc_client import CascadeGrpcClient

from .graph import build_explorer_graph


def main():
    parser = argparse.ArgumentParser(description="Run Explorer agent")
    parser.add_argument("--app-name", required=True, help="Application name/identifier")
    parser.add_argument("--run-id", default=str(uuid.uuid4()), help="Run identifier")
    parser.add_argument("--instructions", help="JSON string of instructions", default="{}")
    parser.add_argument("--grpc-endpoint", help="gRPC endpoint (host:port)")
    args = parser.parse_args()

    context = CascadeContext.from_env()
    grpc_client = CascadeGrpcClient(endpoint=args.grpc_endpoint)
    a2a_client = AgentA2AClient(context, grpc_client, role="explorer", run_id=args.run_id)

    # Start background A2A listener with idempotent handler.
    async def _handle(msg):
        print(f"[A2A][explorer] received {msg.message_id} from {msg.sender_role}")

    def _run_listener():
        asyncio.run(a2a_client.listen(_handle))

    listener_thread = threading.Thread(target=_run_listener, daemon=True)
    listener_thread.start()

    graph = build_explorer_graph(context, grpc_client)

    instructions = json.loads(args.instructions or "{}")
    state = {
        "run_id": args.run_id,
        "app_name": args.app_name,
        "instructions": instructions,
    }
    result = graph.invoke(state)
    print(json.dumps({"skill_map": result.get("skill_map").to_firestore() if result.get("skill_map") else None}, default=str))


if __name__ == "__main__":
    main()


"""CLI entrypoint for the Orchestrator supervisor."""

from __future__ import annotations

import argparse
import json
import os
import uuid

from cascade_client.auth.context import CascadeContext
from cascade_client.grpc_client import CascadeGrpcClient

from agents.worker.skill_loader import load_all_skills
from orchestrator.graph import build_orchestrator_graph
from orchestrator.router import OrchestratorRouter
from orchestrator.storage import OrchestratorStorage


def _build_context(args) -> CascadeContext:
    if args.user_id and args.app_id and args.auth_token:
        return CascadeContext(user_id=args.user_id, app_id=args.app_id, auth_token=args.auth_token)
    return CascadeContext.from_env()


def _configure_firestore(args) -> None:
    if args.firestore_emulator_host:
        os.environ["FIRESTORE_EMULATOR_HOST"] = args.firestore_emulator_host


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Run Cascade Orchestrator")
    parser.add_argument("--goal", required=True, help="High-level goal to orchestrate")
    parser.add_argument("--user-id", help="Override CASCADE_USER_ID")
    parser.add_argument("--app-id", help="Override CASCADE_APP_ID")
    parser.add_argument("--auth-token", help="Override CASCADE_AUTH_TOKEN")
    parser.add_argument("--skills", help="Comma-separated skill IDs to prefer", default="")
    parser.add_argument("--run-id", help="Run identifier", default=str(uuid.uuid4()))
    parser.add_argument("--grpc-endpoint", help="gRPC endpoint (host:port)")
    parser.add_argument("--dry-run", action="store_true", help="Plan only, no A2A dispatch")
    parser.add_argument("--trace", action="store_true", help="Enable verbose tracing")
    parser.add_argument(
        "--firestore-emulator-host",
        help="Use Firestore emulator (host:port). Set empty to disable.",
        default=os.getenv("FIRESTORE_EMULATOR_HOST", "localhost:8080"),
    )
    parser.add_argument(
        "--resume",
        action="store_true",
        help="Resume from existing checkpoint for the given run-id",
    )
    return parser.parse_args()


def main():
    args = parse_args()
    _configure_firestore(args)

    context = _build_context(args)
    grpc_client = CascadeGrpcClient(endpoint=args.grpc_endpoint)

    skills = load_all_skills(context)
    router = OrchestratorRouter(skills)
    storage = OrchestratorStorage(context)
    graph = build_orchestrator_graph(context, grpc_client, router, storage)

    requested_skill_ids = [s.strip() for s in args.skills.split(",") if s.strip()]
    initial_state = {
        "context": context,
        "run_id": args.run_id,
        "goal": args.goal,
        "requested_skill_ids": requested_skill_ids,
        "dry_run": args.dry_run,
        "trace": args.trace,
        "resume": args.resume,
    }

    result = graph.invoke(initial_state)
    print(
        json.dumps(
            {
                "run_id": args.run_id,
                "goal": args.goal,
                "subgoals": [sg.model_dump(mode="json") for sg in result.get("subgoals", [])],
                "decisions": [d.model_dump(mode="json") for d in result.get("decisions", [])],
                "records": [r.model_dump(mode="json") for r in result.get("records", [])],
                "last_error": result.get("last_error"),
            },
            default=str,
        )
    )


if __name__ == "__main__":
    main()



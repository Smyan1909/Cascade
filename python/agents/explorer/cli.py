"""CLI entrypoint for Explorer agent (Autonomous mode only)."""

import argparse
import json
import uuid
import os
from pathlib import Path

from cascade_client.auth.context import CascadeContext
from cascade_client.grpc_client import CascadeGrpcClient

# Load .env file
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

# Debug: Check critical env vars
print(f"[CLI] FIRESTORE_EMULATOR_HOST: {os.getenv('FIRESTORE_EMULATOR_HOST')}")
print(f"[CLI] WEB_SEARCH_PROVIDER: {os.getenv('CASCADE_WEB_SEARCH_PROVIDER')}")
has_key = bool(os.getenv('CASCADE_WEB_SEARCH_API_KEY'))
print(f"[CLI] WEB_SEARCH_API_KEY present: {has_key}")


def load_instructions(args) -> dict:
    """Load instructions from file or inline JSON."""
    if args.instructions_file:
        path = Path(args.instructions_file)
        if path.exists():
            print(f"[Explorer] Loading instructions from: {path}")
            return json.loads(path.read_text(encoding="utf-8"))
        else:
            print(f"[Explorer] Warning: File not found: {path}")
    
    if args.instructions and args.instructions != "{}":
        try:
            return json.loads(args.instructions)
        except json.JSONDecodeError as e:
            print(f"[Explorer] JSON parse error: {e}")
            print("[Explorer] Tip: Use --instructions-file instead of inline JSON")
    
    return {}


def main():
    parser = argparse.ArgumentParser(description="Run Explorer agent (Autonomous)")
    parser.add_argument("--app-name", required=True, help="Application name/identifier")
    parser.add_argument("--run-id", default=str(uuid.uuid4()), help="Run identifier")
    parser.add_argument("--instructions", help="JSON string of instructions", default="{}")
    parser.add_argument("--instructions-file", help="Path to JSON file with instructions (recommended)")
    parser.add_argument("--grpc-endpoint", help="gRPC endpoint (host:port)")
    parser.add_argument("--max-iterations", type=int, default=100, help="Maximum iterations")
    args = parser.parse_args()

    # Import here to avoid circular imports
    from .autonomous_explorer import HybridExplorer
    
    context = CascadeContext.from_env()
    grpc_client = CascadeGrpcClient(endpoint=args.grpc_endpoint)
    
    instructions = load_instructions(args)
    
    if not instructions:
        print("[Explorer] Warning: No instructions provided!")
        print("[Explorer] Use --instructions-file to specify what to explore")
    
    print("[Explorer] Running in AUTONOMOUS mode")
    explorer = HybridExplorer(
        context, 
        grpc_client, 
        max_verify_iterations=args.max_iterations,
        verbose=True,
    )
    result = explorer.explore(
        app_name=args.app_name,
        instructions=instructions,
        run_id=args.run_id,
    )
    
    print("\n" + "=" * 50)
    print(f"Status: {result.status.value}")
    print(f"Iterations: {result.iterations}")
    print(f"Tool Calls: {len(result.tool_calls)}")
    if hasattr(result, 'elapsed_seconds'):
        print(f"Elapsed: {result.elapsed_seconds:.1f}s")
    if result.error:
        print(f"Error: {result.error}")
    print("=" * 50)
    print("\nFinal Response:")
    print(result.final_response)


if __name__ == "__main__":
    main()

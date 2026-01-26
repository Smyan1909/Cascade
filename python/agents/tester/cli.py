"""CLI entry point for the Test Agent."""

import argparse
import sys

from cascade_client.auth.context import CascadeContext
from cascade_client.grpc_client import CascadeGrpcClient

from storage.firestore_client import FirestoreClient
from agents.explorer.skill_map import SkillMap
from agents.tester.test_agent import TestAgent


def main():
    parser = argparse.ArgumentParser(description="Test Agent - Execute and verify a skill")
    parser.add_argument(
        "--skill-id",
        required=True,
        help="ID of the skill to test"
    )
    parser.add_argument(
        "--app-id",
        default="cascade",
        help="Application ID (default: cascade)"
    )
    parser.add_argument(
        "--user-id",
        default="test-user",
        help="User ID (default: test-user)"
    )
    parser.add_argument(
        "--grpc-host",
        default="localhost",
        help="gRPC server host (default: localhost)"
    )
    parser.add_argument(
        "--grpc-port",
        type=int,
        default=50051,
        help="gRPC server port (default: 50051)"
    )
    parser.add_argument(
        "--max-iterations",
        type=int,
        default=50,
        help="Maximum agent iterations (default: 50)"
    )
    parser.add_argument(
        "--quiet",
        action="store_true",
        help="Suppress verbose output"
    )
    
    args = parser.parse_args()
    
    # Create context
    context = CascadeContext(
        app_id=args.app_id,
        user_id=args.user_id,
    )
    
    # Load the skill from Firestore
    print(f"Loading skill: {args.skill_id}")
    fs = FirestoreClient(context)
    skill_data = fs.get_skill_map(args.skill_id)
    
    if not skill_data:
        print(f"Error: Skill '{args.skill_id}' not found")
        sys.exit(1)
    
    skill = SkillMap.model_validate(skill_data)
    print(f"Skill loaded: {skill.metadata.description or skill.metadata.capability}")
    
    # Create gRPC client
    grpc_client = CascadeGrpcClient(host=args.grpc_host, port=args.grpc_port)
    
    # Create test agent
    tester = TestAgent(
        context=context,
        grpc_client=grpc_client,
        max_iterations=args.max_iterations,
        verbose=not args.quiet,
    )
    
    # Run the test
    print(f"\n{'='*50}")
    print("EXECUTING SKILL TEST")
    print(f"{'='*50}\n")
    
    result = tester.test_skill(skill, app_name=args.app_id)
    
    # Output result
    print(f"\n{'='*50}")
    print("TEST RESULT")
    print(f"{'='*50}")
    print(f"Status: {'SUCCESS ✓' if result.success else 'FAILURE ✗'}")
    print(f"Reasoning: {result.reasoning}")
    print(f"Iterations: {result.iterations}")
    print(f"Elapsed: {result.elapsed_seconds:.2f}s")
    
    if result.error:
        print(f"Error: {result.error}")
    
    # Exit with appropriate code
    sys.exit(0 if result.success else 1)


if __name__ == "__main__":
    main()

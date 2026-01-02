# Auto-generated executable skill (Python)
import os
from cascade_client.grpc_client import CascadeGrpcClient
from cascade_client.models import Action, ActionType

def run(inputs: dict):
    # Entrypoint executed by Brain Python executor. Uses CASCADE_GRPC_ENDPOINT.
    endpoint = os.environ.get('CASCADE_GRPC_ENDPOINT')
    if not endpoint:
        raise ValueError('CASCADE_GRPC_ENDPOINT is required')
    client = CascadeGrpcClient(endpoint=endpoint)
    # inputs may contain runtime parameters referenced by steps
    # Step 1: CallAPI
    return 'skill:wb_new'

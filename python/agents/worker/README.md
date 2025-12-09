# Worker Agent

LangGraph-based Worker that loads Explorer-generated Skill Maps from Firestore,
executes them via the Body gRPC API, and checkpoints after each step. See
`docs/phase5-worker.md`.

## Modules
- `runtime.py`: WorkerAgent with start/resume + streaming events
- `graph.py`: LangGraph wiring and step executor
- `api.py`: Python facade and gRPC servicer factory
- `storage/`: Firestore helpers scoped by user/app

## Usage (Python)
```python
from cascade_client.auth.context import CascadeContext
from cascade_client.grpc_client import CascadeGrpcClient
from agents.worker.runtime import WorkerAgent
from agents.worker.storage import WorkerStorage

ctx = CascadeContext.from_env()
storage = WorkerStorage(ctx)
grpc_client = CascadeGrpcClient()
agent = WorkerAgent(storage, grpc_client)

async for event in agent.start_run(skill_id="my-skill"):
    print(event)
```

## gRPC
The `WorkerService` (server-streaming) is defined in `proto/cascade.proto` with
`StartWorkerRun` and `ResumeWorkerRun`. Use `build_worker_servicer` to bind the
runtime to a `grpc.aio` server; C# clients can consume the stream directly.

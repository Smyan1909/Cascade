# Cascade Python SDK

Python client SDK and agent implementations for Cascade.

## Structure

- `cascade_client/`: Shared Python client SDK (Phase 3)
- `agents/explorer/`: Explorer agent (Phase 4)
- `agents/worker/`: Worker agent (Phase 5)
- `orchestrator/`: Orchestrator agent (Phase 6)
- `storage/`: Shared Firestore storage utilities

## Setup

### Prerequisites

- Python 3.12+ (latest stable)
- Virtual environment tool: `venv` (standard library)

### Installation

1. **Create and activate virtual environment:**

   Windows (PowerShell):
   ```powershell
   cd python
   .\setup_venv.ps1
   .\.venv\Scripts\Activate.ps1
   ```

   Linux/Mac:
   ```bash
   cd python
   ./setup_venv.sh
   source .venv/bin/activate
   ```

   Or manually:
   ```bash
   cd python
   python -m venv .venv
   source .venv/bin/activate  # Linux/Mac
   # or
   .venv\Scripts\activate  # Windows
   ```

2. **Install dependencies:**

   ```bash
   pip install -r requirements.txt
   ```

3. **Generate gRPC stubs:**

   Windows:
   ```powershell
   .\generate_proto.ps1
   ```

   Linux/Mac:
   ```bash
   ./generate_proto.sh
   ```

   Or manually:
   ```bash
   python -m grpc_tools.protoc -I ../proto --python_out=cascade_client/proto --grpc_python_out=cascade_client/proto ../proto/cascade.proto
   ```

## Configuration

Set the following environment variables:

### Required

- `CASCADE_GRPC_ENDPOINT`: gRPC endpoint (host:port), e.g., `localhost:50051`
- `CASCADE_USER_ID`: User ID for Firestore scoping
- `CASCADE_APP_ID`: Application ID for Firestore scoping
- `CASCADE_AUTH_TOKEN`: Authentication token for Firestore

### Optional

- `CASCADE_MODEL_PROVIDER`: LLM provider (e.g., `openai`, `anthropic`)
- `CASCADE_MODEL_NAME`: Model name
- `CASCADE_MODEL_ENDPOINT`: Model endpoint URL
- `CASCADE_MODEL_API_KEY`: API key for model provider

## Usage

### Cascade Client SDK

See [cascade_client/README.md](cascade_client/README.md) for detailed usage examples.

Quick example:

```python
from cascade_client import CascadeGrpcClient, CascadeContext, by_id, PlatformSource, Action, ActionType

# Create context
context = CascadeContext.from_env()

# Create client
client = CascadeGrpcClient()

# Create selector and perform action
selector = by_id(PlatformSource.WEB, "btn_submit")
action = Action(action_type=ActionType.CLICK, selector=selector)
status = client.perform_action(action)
```

## Testing

Run tests with pytest:

```bash
# All tests
pytest tests/

# Unit tests only
pytest tests/unit/

# Integration tests only
pytest tests/integration/

# With coverage
pytest tests/ --cov=cascade_client --cov-report=html
```

## Development

### Project Structure

```
python/
├── cascade_client/      # Shared client SDK
│   ├── __init__.py
│   ├── grpc_client.py   # gRPC client with retry logic
│   ├── models.py        # Pydantic models
│   ├── selectors.py     # Selector builders
│   ├── vision.py        # Screenshot helpers
│   ├── tools.py         # LLM tool functions
│   ├── auth/
│   │   └── context.py   # Context management
│   └── proto/           # Generated gRPC stubs
├── agents/              # Agent implementations
├── tests/               # Test suite
│   ├── unit/           # Unit tests
│   └── integration/     # Integration tests
├── requirements.txt     # Python dependencies
├── generate_proto.ps1   # Proto generation (Windows)
├── generate_proto.sh    # Proto generation (Linux/Mac)
└── setup_venv.ps1       # Setup script (Windows)
```

### Code Style

- Follow PEP 8
- Use type hints
- Add docstrings to all public functions/classes
- Run linters: `ruff check .` (if configured)

### Adding New Features

1. Add implementation in appropriate module
2. Add unit tests in `tests/unit/`
3. Add integration tests if needed in `tests/integration/`
4. Update documentation in README files
5. Ensure all tests pass

## Troubleshooting

### Proto Stubs Not Generated

If you see `ImportError: Proto stubs not generated`, run the proto generation script:

```bash
python generate_proto.ps1  # Windows
# or
./generate_proto.sh  # Linux/Mac
```

### gRPC Connection Errors

- Verify `CASCADE_GRPC_ENDPOINT` is set correctly
- Ensure the Body server is running
- Check network connectivity
- For local development, ensure the endpoint uses `localhost` or `127.0.0.1`

### Import Errors

- Ensure virtual environment is activated
- Verify all dependencies are installed: `pip install -r requirements.txt`
- Check that you're running from the correct directory

## License

See main project LICENSE file.


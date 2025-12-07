# Cascade Python Client SDK

Shared Python client SDK for gRPC access, selector utilities, and auth/context handling. Used by Explorer, Worker, and Orchestrator agents.

## Overview

The `cascade_client` package provides:

- **gRPC Client** (`grpc_client.py`): Ergonomic sync/async wrappers over gRPC stubs with retry/backoff
- **Models** (`models.py`): Pydantic models mirroring proto messages with conversion helpers
- **Selectors** (`selectors.py`): Platform-neutral selector builder functions
- **Vision** (`vision.py`): Marked screenshot helper functions
- **Tools** (`tools.py`): High-level tool functions for LLM integration
- **Auth/Context** (`auth/context.py`): User/app/auth token context management

## Quick Start

```python
from cascade_client import CascadeGrpcClient, CascadeContext, by_id, PlatformSource

# Create context from environment variables
context = CascadeContext.from_env()

# Create gRPC client
client = CascadeGrpcClient(endpoint="localhost:50051")

# Create a selector
selector = by_id(PlatformSource.WEB, "btn_submit")

# Perform an action
from cascade_client import Action, ActionType
action = Action(action_type=ActionType.CLICK, selector=selector)
status = client.perform_action(action)
print(f"Success: {status.success}, Message: {status.message}")
```

## Configuration

Set the following environment variables:

- `CASCADE_GRPC_ENDPOINT`: gRPC endpoint (host:port), e.g., `localhost:50051`
- `CASCADE_USER_ID`: User ID for Firestore scoping
- `CASCADE_APP_ID`: Application ID for Firestore scoping
- `CASCADE_AUTH_TOKEN`: Authentication token for Firestore

## Usage Examples

### gRPC Client

```python
from cascade_client import CascadeGrpcClient

# Create client (endpoint from env or explicit)
client = CascadeGrpcClient(endpoint="localhost:50051")

# Session operations
status = client.start_app("calculator")
status = client.reset_state()

# Automation operations
tree = client.get_semantic_tree()
for elem in tree.elements:
    print(f"Element: {elem.name} ({elem.control_type})")

# Vision operations
screenshot_proto = client.get_marked_screenshot()

# Async operations
import asyncio
async def main():
    tree = await client.get_semantic_tree_async()
    status = await client.start_app_async("calculator")

# Health check
is_healthy = client.health_check()

# Clean up
client.close()
```

### Selectors

```python
from cascade_client import (
    by_id, by_name, by_control_type, by_path, by_index,
    with_text_hint, PlatformSource, ControlType
)

# By ID
selector = by_id(PlatformSource.WEB, "btn_submit")

# By name
selector = by_name(PlatformSource.WINDOWS, "Submit Button")

# By control type
selector = by_control_type(PlatformSource.WEB, ControlType.BUTTON)

# By path
selector = by_path(PlatformSource.WEB, ["html", "body", "div", "button"])

# By index
selector = by_index(PlatformSource.WEB, 0)

# With text hint (modifier)
selector = with_text_hint(selector, "Click me")

# Combined criteria
selector = by_id(
    PlatformSource.WEB,
    "btn_submit",
    name="Submit",
    control_type=ControlType.BUTTON,
    index=0
)
```

### Vision Helpers

```python
from cascade_client import CascadeGrpcClient, get_marked_screenshot, save_screenshot

client = CascadeGrpcClient(endpoint="localhost:50051")

# Get marked screenshot
image_bytes, marks, format = get_marked_screenshot(client)
print(f"Screenshot: {len(image_bytes)} bytes, {len(marks)} marks")

# Save screenshot
save_screenshot(image_bytes, "screenshot.png", format)

# Find marks
from cascade_client.vision import get_mark_by_element_id
mark = get_mark_by_element_id(marks, "elem1")
if mark:
    print(f"Found mark: {mark.label}")
```

### Tools (LLM Integration)

```python
from cascade_client import (
    CascadeGrpcClient, click_element, type_text, get_semantic_tree,
    get_tool_schemas, by_id, PlatformSource
)

client = CascadeGrpcClient(endpoint="localhost:50051")

# Use tool functions
selector = by_id(PlatformSource.WEB, "btn_submit")
result = click_element(client, selector)
print(result)  # {"success": True, "message": "..."}

# Type text
selector = by_id(PlatformSource.WEB, "input1")
result = type_text(client, selector, "Hello, World!")

# Get semantic tree
tree_data = get_semantic_tree(client)
print(f"Found {len(tree_data['elements'])} elements")

# Get tool schemas for LLM frameworks
schemas = get_tool_schemas()
# Use schemas with OpenAI function calling, LangChain, etc.
```

### Auth Context

```python
from cascade_client import CascadeContext

# Create from environment variables
context = CascadeContext.from_env()

# Or create explicitly
context = CascadeContext(
    user_id="user1",
    app_id="app1",
    auth_token="token123"
)

# Get Firestore configuration
firestore_config = context.get_firestore_config()

# Get Firestore paths
skill_map_path = context.get_skill_map_path("skill1")
# Returns: "/artifacts/app1/users/user1/skill_maps/skill1"

checkpoint_path = context.get_worker_checkpoint_path("run1")
# Returns: "/artifacts/app1/users/user1/worker_checkpoints/run1"
```

## Models

All proto messages have corresponding Pydantic models:

- `Status`: Operation status
- `StartAppRequest`: Application start request
- `SemanticTree`: UI element tree
- `UIElement`: Individual UI element
- `Selector`: Element selector
- `Action`: Action to perform
- `Screenshot`: Marked screenshot
- `Mark`: Screenshot mark
- `NormalizedRectangle`: Normalized bounding box

### Model Conversion

```python
from cascade_client.models import UIElement, ControlType, PlatformSource

# Create model
element = UIElement(
    id="elem1",
    name="Button",
    control_type=ControlType.BUTTON,
    platform_source=PlatformSource.WEB
)

# Convert to proto (requires generated stubs)
proto_msg = element.to_proto()

# Convert from proto
element = UIElement.from_proto(proto_msg)
```

### Semantic Tree Utilities

```python
from cascade_client import SemanticTree

tree = client.get_semantic_tree()

# Convert to graph structure
graph = tree.to_graph()
# Returns: {element_id: {"element": UIElement, "children": [...], "parent": ...}}

# Get element by ID
element = tree.get_element_by_id("elem1")

# Get elements by control type
buttons = tree.get_elements_by_control_type(ControlType.BUTTON)
```

## Error Handling

The gRPC client includes automatic retry logic for retryable errors:

- `UNAVAILABLE`: Service unavailable
- `DEADLINE_EXCEEDED`: Request deadline exceeded
- `RESOURCE_EXHAUSTED`: Resource exhausted

Non-retryable errors (e.g., `INVALID_ARGUMENT`) are not retried.

```python
from cascade_client.grpc_client import CascadeGrpcClient, RetryableError, NonRetryableError

client = CascadeGrpcClient(endpoint="localhost:50051")

try:
    status = client.start_app("calculator")
except RetryableError as e:
    print(f"Retryable error after retries: {e}")
except NonRetryableError as e:
    print(f"Non-retryable error: {e}")
```

## Testing

Run tests with pytest:

```bash
# Run all tests
pytest python/tests/

# Run unit tests only
pytest python/tests/unit/

# Run integration tests only
pytest python/tests/integration/

# Run with coverage
pytest python/tests/ --cov=cascade_client --cov-report=html
```

## API Reference

See docstrings in individual modules for detailed API documentation:

- `grpc_client.py`: gRPC client with retry logic
- `models.py`: Pydantic models and conversion helpers
- `selectors.py`: Selector builder functions
- `vision.py`: Screenshot and mark utilities
- `tools.py`: LLM tool functions and schemas
- `auth/context.py`: Context management

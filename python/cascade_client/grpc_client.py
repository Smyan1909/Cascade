"""
gRPC client for Cascade Body server.

Provides ergonomic sync/async wrappers over gRPC stubs with retry/backoff,
deadline management, and lazy channel creation.
"""

import asyncio
import os
import time
from typing import Optional, TYPE_CHECKING

import grpc

from cascade_client.models import (
    Action,
    SemanticTree,
    StartAppRequest,
    Status,
)

if TYPE_CHECKING:
    from cascade_client.proto import cascade_pb2, cascade_pb2_grpc
else:
    try:
        from cascade_client.proto import cascade_pb2, cascade_pb2_grpc
    except ImportError:
        # Proto stubs not generated yet
        cascade_pb2 = None
        cascade_pb2_grpc = None


class GrpcError(Exception):
    """Base exception for gRPC errors."""

    pass


class RetryableError(GrpcError):
    """Error that should be retried."""

    pass


class NonRetryableError(GrpcError):
    """Error that should not be retried."""

    pass


class CascadeGrpcClient:
    """
    gRPC client for Cascade Body server.

    Provides sync and async wrappers for all RPCs with automatic retry,
    configurable deadlines, and lazy channel creation.
    """

    # Deadline constants (in seconds)
    DEADLINE_HEALTH = 5.0  # Short deadline for health checks
    DEADLINE_SEMANTIC_TREE = 30.0  # Medium deadline for semantic tree
    DEADLINE_SCREENSHOT = 60.0  # Longer deadline for screenshots
    DEADLINE_ACTION = 30.0  # Medium deadline for actions
    DEADLINE_SESSION = 30.0  # Medium deadline for session operations

    # Retry configuration
    MAX_RETRIES = 3
    INITIAL_BACKOFF = 1.0  # Initial backoff in seconds
    MAX_BACKOFF = 10.0  # Maximum backoff in seconds
    BACKOFF_MULTIPLIER = 2.0  # Exponential backoff multiplier

    def __init__(
        self,
        endpoint: Optional[str] = None,
        max_retries: int = MAX_RETRIES,
        initial_backoff: float = INITIAL_BACKOFF,
        max_backoff: float = MAX_BACKOFF,
        backoff_multiplier: float = BACKOFF_MULTIPLIER,
    ):
        """
        Initialize gRPC client.

        Args:
            endpoint: gRPC endpoint (host:port). If None, reads from CASCADE_GRPC_ENDPOINT env var.
            max_retries: Maximum number of retry attempts
            initial_backoff: Initial backoff delay in seconds
            max_backoff: Maximum backoff delay in seconds
            backoff_multiplier: Multiplier for exponential backoff
        """
        self._endpoint = endpoint or os.getenv("CASCADE_GRPC_ENDPOINT")
        if not self._endpoint:
            raise ValueError(
                "gRPC endpoint must be provided or set via CASCADE_GRPC_ENDPOINT env var"
            )

        self._max_retries = max_retries
        self._initial_backoff = initial_backoff
        self._max_backoff = max_backoff
        self._backoff_multiplier = backoff_multiplier

        self._channel: Optional[grpc.Channel] = None
        self._session_stub: Optional[cascade_pb2_grpc.SessionServiceStub] = None
        self._automation_stub: Optional[
            cascade_pb2_grpc.AutomationServiceStub
        ] = None
        self._vision_stub: Optional[cascade_pb2_grpc.VisionServiceStub] = None

    def _get_channel(self) -> grpc.Channel:
        """Get or create gRPC channel (lazy initialization)."""
        if self._channel is None:
            # Create insecure channel (for local dev; production should use TLS)
            self._channel = grpc.insecure_channel(self._endpoint)
        return self._channel

    def _get_session_stub(self):
        """Get or create session service stub."""
        if cascade_pb2_grpc is None:
            raise ImportError(
                "Proto stubs not generated. Run generate_proto.ps1 or generate_proto.sh"
            )
        if self._session_stub is None:
            self._session_stub = cascade_pb2_grpc.SessionServiceStub(
                self._get_channel()
            )
        return self._session_stub

    def _get_automation_stub(self):
        """Get or create automation service stub."""
        if cascade_pb2_grpc is None:
            raise ImportError(
                "Proto stubs not generated. Run generate_proto.ps1 or generate_proto.sh"
            )
        if self._automation_stub is None:
            self._automation_stub = cascade_pb2_grpc.AutomationServiceStub(
                self._get_channel()
            )
        return self._automation_stub

    def _get_vision_stub(self):
        """Get or create vision service stub."""
        if cascade_pb2_grpc is None:
            raise ImportError(
                "Proto stubs not generated. Run generate_proto.ps1 or generate_proto.sh"
            )
        if self._vision_stub is None:
            self._vision_stub = cascade_pb2_grpc.VisionServiceStub(
                self._get_channel()
            )
        return self._vision_stub

    def _is_retryable_error(self, error: grpc.RpcError) -> bool:
        """Check if error is retryable."""
        if error.code() in (
            grpc.StatusCode.UNAVAILABLE,
            grpc.StatusCode.DEADLINE_EXCEEDED,
            grpc.StatusCode.RESOURCE_EXHAUSTED,
        ):
            return True
        return False

    def _calculate_backoff(self, attempt: int) -> float:
        """Calculate backoff delay for retry attempt."""
        backoff = self._initial_backoff * (self._backoff_multiplier ** (attempt - 1))
        return min(backoff, self._max_backoff)

    def _retry_call(self, func, deadline: float, *args, **kwargs):
        """
        Execute function with retry logic.

        Args:
            func: Function to call
            deadline: Deadline in seconds
            *args: Positional arguments for func
            **kwargs: Keyword arguments for func

        Returns:
            Result from func

        Raises:
            RetryableError: If all retries exhausted for retryable errors
            NonRetryableError: If error is not retryable
        """
        last_error = None

        for attempt in range(1, self._max_retries + 1):
            try:
                return func(*args, **kwargs)
            except grpc.RpcError as e:
                last_error = e

                if not self._is_retryable_error(e):
                    raise NonRetryableError(
                        f"Non-retryable error: {e.code()} - {e.details()}"
                    ) from e

                if attempt < self._max_retries:
                    backoff = self._calculate_backoff(attempt)
                    time.sleep(backoff)
                else:
                    # Last attempt failed
                    raise RetryableError(
                        f"Retryable error after {self._max_retries} attempts: {e.code()} - {e.details()}"
                    ) from e
            except Exception as e:
                # Non-gRPC errors are not retryable
                raise NonRetryableError(f"Non-retryable error: {str(e)}") from e

        # Should not reach here, but just in case
        if last_error:
            raise RetryableError(
                f"Failed after {self._max_retries} attempts: {last_error}"
            ) from last_error

    async def _retry_call_async(self, func, deadline: float, *args, **kwargs):
        """
        Execute async function with retry logic.

        Args:
            func: Async function to call
            deadline: Deadline in seconds
            *args: Positional arguments for func
            **kwargs: Keyword arguments for func

        Returns:
            Result from func

        Raises:
            RetryableError: If all retries exhausted for retryable errors
            NonRetryableError: If error is not retryable
        """
        last_error = None

        for attempt in range(1, self._max_retries + 1):
            try:
                return await func(*args, **kwargs)
            except grpc.RpcError as e:
                last_error = e

                if not self._is_retryable_error(e):
                    raise NonRetryableError(
                        f"Non-retryable error: {e.code()} - {e.details()}"
                    ) from e

                if attempt < self._max_retries:
                    backoff = self._calculate_backoff(attempt)
                    await asyncio.sleep(backoff)
                else:
                    # Last attempt failed
                    raise RetryableError(
                        f"Retryable error after {self._max_retries} attempts: {e.code()} - {e.details()}"
                    ) from e
            except Exception as e:
                # Non-gRPC errors are not retryable
                raise NonRetryableError(f"Non-retryable error: {str(e)}") from e

        # Should not reach here, but just in case
        if last_error:
            raise RetryableError(
                f"Failed after {self._max_retries} attempts: {last_error}"
            ) from last_error

    # Session Service - Sync
    def start_app(self, app_name: str) -> Status:
        """
        Start an application (sync).

        Args:
            app_name: Application name/identifier

        Returns:
            Status response
        """
        if cascade_pb2 is None:
            raise ImportError("Proto stubs not generated")

        request = StartAppRequest(app_name=app_name)
        proto_request = request.to_proto()

        def _call():
            return self._get_session_stub().StartApp(
                proto_request, timeout=self.DEADLINE_SESSION
            )

        proto_response = self._retry_call(_call, self.DEADLINE_SESSION)
        return Status.from_proto(proto_response)

    def reset_state(self) -> Status:
        """
        Reset transient state (sync).

        Returns:
            Status response
        """
        if cascade_pb2 is None:
            raise ImportError("Proto stubs not generated")

        from google.protobuf.empty_pb2 import Empty

        def _call():
            return self._get_session_stub().ResetState(
                Empty(), timeout=self.DEADLINE_SESSION
            )

        proto_response = self._retry_call(_call, self.DEADLINE_SESSION)
        return Status.from_proto(proto_response)

    # Session Service - Async
    async def start_app_async(self, app_name: str) -> Status:
        """
        Start an application (async).

        Args:
            app_name: Application name/identifier

        Returns:
            Status response
        """
        if cascade_pb2 is None:
            raise ImportError("Proto stubs not generated")

        request = StartAppRequest(app_name=app_name)
        proto_request = request.to_proto()

        async def _call():
            # Run synchronous gRPC call in thread pool
            loop = asyncio.get_event_loop()
            return await loop.run_in_executor(
                None,
                lambda: self._get_session_stub().StartApp(
                    proto_request, timeout=self.DEADLINE_SESSION
                ),
            )

        proto_response = await self._retry_call_async(
            _call, self.DEADLINE_SESSION
        )
        return Status.from_proto(proto_response)

    async def reset_state_async(self) -> Status:
        """
        Reset transient state (async).

        Returns:
            Status response
        """
        if cascade_pb2 is None:
            raise ImportError("Proto stubs not generated")

        from google.protobuf.empty_pb2 import Empty

        async def _call():
            loop = asyncio.get_event_loop()
            return await loop.run_in_executor(
                None,
                lambda: self._get_session_stub().ResetState(
                    Empty(), timeout=self.DEADLINE_SESSION
                ),
            )

        proto_response = await self._retry_call_async(
            _call, self.DEADLINE_SESSION
        )
        return Status.from_proto(proto_response)

    # Automation Service - Sync
    def perform_action(self, action: Action) -> Status:
        """
        Perform an action (sync).

        Args:
            action: Action to perform

        Returns:
            Status response
        """
        proto_action = action.to_proto()

        def _call():
            return self._get_automation_stub().PerformAction(
                proto_action, timeout=self.DEADLINE_ACTION
            )

        proto_response = self._retry_call(_call, self.DEADLINE_ACTION)
        return Status.from_proto(proto_response)

    def get_semantic_tree(self) -> SemanticTree:
        """
        Get semantic tree (sync).

        Returns:
            SemanticTree response
        """
        from google.protobuf.empty_pb2 import Empty

        def _call():
            return self._get_automation_stub().GetSemanticTree(
                Empty(), timeout=self.DEADLINE_SEMANTIC_TREE
            )

        proto_response = self._retry_call(_call, self.DEADLINE_SEMANTIC_TREE)
        return SemanticTree.from_proto(proto_response)

    # Automation Service - Async
    async def perform_action_async(self, action: Action) -> Status:
        """
        Perform an action (async).

        Args:
            action: Action to perform

        Returns:
            Status response
        """
        proto_action = action.to_proto()

        async def _call():
            loop = asyncio.get_event_loop()
            return await loop.run_in_executor(
                None,
                lambda: self._get_automation_stub().PerformAction(
                    proto_action, timeout=self.DEADLINE_ACTION
                ),
            )

        proto_response = await self._retry_call_async(
            _call, self.DEADLINE_ACTION
        )
        return Status.from_proto(proto_response)

    async def get_semantic_tree_async(self) -> SemanticTree:
        """
        Get semantic tree (async).

        Returns:
            SemanticTree response
        """
        from google.protobuf.empty_pb2 import Empty

        async def _call():
            loop = asyncio.get_event_loop()
            return await loop.run_in_executor(
                None,
                lambda: self._get_automation_stub().GetSemanticTree(
                    Empty(), timeout=self.DEADLINE_SEMANTIC_TREE
                ),
            )

        proto_response = await self._retry_call_async(
            _call, self.DEADLINE_SEMANTIC_TREE
        )
        return SemanticTree.from_proto(proto_response)

    # Vision Service - Sync
    def get_marked_screenshot(self):
        """
        Get marked screenshot (sync).

        Returns:
            Screenshot response (proto message, will be converted in vision.py)
        """
        from google.protobuf.empty_pb2 import Empty

        def _call():
            return self._get_vision_stub().GetMarkedScreenshot(
                Empty(), timeout=self.DEADLINE_SCREENSHOT
            )

        proto_response = self._retry_call(_call, self.DEADLINE_SCREENSHOT)
        return proto_response

    # Vision Service - Async
    async def get_marked_screenshot_async(self):
        """
        Get marked screenshot (async).

        Returns:
            Screenshot response (proto message, will be converted in vision.py)
        """
        from google.protobuf.empty_pb2 import Empty

        async def _call():
            loop = asyncio.get_event_loop()
            return await loop.run_in_executor(
                None,
                lambda: self._get_vision_stub().GetMarkedScreenshot(
                    Empty(), timeout=self.DEADLINE_SCREENSHOT
                ),
            )

        proto_response = await self._retry_call_async(
            _call, self.DEADLINE_SCREENSHOT
        )
        return proto_response

    # Health Check
    def health_check(self) -> bool:
        """
        Perform a health check by attempting to get semantic tree.

        Returns:
            True if server is healthy, False otherwise
        """
        try:
            # Use a short timeout for health check
            self.get_semantic_tree()
            return True
        except Exception:
            return False

    async def health_check_async(self) -> bool:
        """
        Perform an async health check by attempting to get semantic tree.

        Returns:
            True if server is healthy, False otherwise
        """
        try:
            # Use a short timeout for health check
            await self.get_semantic_tree_async()
            return True
        except Exception:
            return False

    def close(self):
        """Close the gRPC channel."""
        if self._channel:
            self._channel.close()
            self._channel = None
            self._session_stub = None
            self._automation_stub = None
            self._vision_stub = None

    def __enter__(self):
        """Context manager entry."""
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        """Context manager exit."""
        self.close()


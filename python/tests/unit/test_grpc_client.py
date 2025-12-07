"""
Unit tests for grpc_client.py (gRPC client).
"""

import os
from unittest.mock import MagicMock, patch

import grpc
import pytest

from cascade_client.grpc_client import (
    CascadeGrpcClient,
    NonRetryableError,
    RetryableError,
)


class MockRpcError(grpc.RpcError):
    """Mock RpcError for testing."""

    def __init__(self, code, details=""):
        self._code = code
        self._details = details

    def code(self):
        return self._code

    def details(self):
        return self._details


class TestCascadeGrpcClient:
    """Tests for CascadeGrpcClient class."""

    def test_init_with_endpoint(self):
        """Test initialization with explicit endpoint."""
        client = CascadeGrpcClient(endpoint="localhost:50051")
        assert client._endpoint == "localhost:50051"

    def test_init_from_env(self, env_vars):
        """Test initialization from environment variable."""
        client = CascadeGrpcClient()
        assert client._endpoint == "localhost:50051"

    def test_init_missing_endpoint(self, monkeypatch):
        """Test initialization fails without endpoint."""
        monkeypatch.delenv("CASCADE_GRPC_ENDPOINT", raising=False)
        with pytest.raises(ValueError, match="endpoint"):
            CascadeGrpcClient()

    def test_lazy_channel_creation(self):
        """Test that channel is created lazily."""
        client = CascadeGrpcClient(endpoint="localhost:50051")
        assert client._channel is None

        # Channel should be created on first use
        with patch("grpc.insecure_channel") as mock_channel:
            mock_channel.return_value = MagicMock()
            client._get_channel()
            mock_channel.assert_called_once_with("localhost:50051")

    def test_retry_configuration(self):
        """Test retry configuration."""
        client = CascadeGrpcClient(
            endpoint="localhost:50051",
            max_retries=5,
            initial_backoff=2.0,
            max_backoff=20.0,
            backoff_multiplier=3.0,
        )
        assert client._max_retries == 5
        assert client._initial_backoff == 2.0
        assert client._max_backoff == 20.0
        assert client._backoff_multiplier == 3.0

    def test_calculate_backoff(self):
        """Test backoff calculation."""
        client = CascadeGrpcClient(endpoint="localhost:50051")
        assert client._calculate_backoff(1) == 1.0
        assert client._calculate_backoff(2) == 2.0
        assert client._calculate_backoff(3) == 4.0

    def test_is_retryable_error(self):
        """Test retryable error detection."""
        client = CascadeGrpcClient(endpoint="localhost:50051")

        # Create mock RpcError
        unavailable_error = MagicMock()
        unavailable_error.code.return_value = grpc.StatusCode.UNAVAILABLE

        deadline_error = MagicMock()
        deadline_error.code.return_value = grpc.StatusCode.DEADLINE_EXCEEDED

        resource_error = MagicMock()
        resource_error.code.return_value = grpc.StatusCode.RESOURCE_EXHAUSTED

        invalid_error = MagicMock()
        invalid_error.code.return_value = grpc.StatusCode.INVALID_ARGUMENT

        assert client._is_retryable_error(unavailable_error) is True
        assert client._is_retryable_error(deadline_error) is True
        assert client._is_retryable_error(resource_error) is True
        assert client._is_retryable_error(invalid_error) is False

    def test_close(self):
        """Test closing the client."""
        client = CascadeGrpcClient(endpoint="localhost:50051")
        mock_channel = MagicMock()
        client._channel = mock_channel

        client.close()
        mock_channel.close.assert_called_once()
        assert client._channel is None

    def test_context_manager(self):
        """Test using client as context manager."""
        with CascadeGrpcClient(endpoint="localhost:50051") as client:
            assert client is not None
            # Channel should be closed on exit
            mock_channel = MagicMock()
            client._channel = mock_channel

        mock_channel.close.assert_called_once()


class TestRetryLogic:
    """Tests for retry logic."""

    def test_retry_on_retryable_error(self):
        """Test that retryable errors are retried."""
        client = CascadeGrpcClient(endpoint="localhost:50051", max_retries=3)

        call_count = 0

        def failing_call():
            nonlocal call_count
            call_count += 1
            raise MockRpcError(
                grpc.StatusCode.UNAVAILABLE, "Service unavailable"
            )

        with pytest.raises(RetryableError):
            client._retry_call(failing_call, 10.0)

        assert call_count == 3  # Should retry 3 times

    def test_no_retry_on_non_retryable_error(self):
        """Test that non-retryable errors are not retried."""
        client = CascadeGrpcClient(endpoint="localhost:50051", max_retries=3)

        call_count = 0

        def failing_call():
            nonlocal call_count
            call_count += 1
            raise MockRpcError(
                grpc.StatusCode.INVALID_ARGUMENT, "Invalid argument"
            )

        with pytest.raises(NonRetryableError):
            client._retry_call(failing_call, 10.0)

        assert call_count == 1  # Should not retry

    def test_success_after_retry(self):
        """Test successful call after retry."""
        client = CascadeGrpcClient(endpoint="localhost:50051", max_retries=3)

        call_count = 0

        def eventually_successful_call():
            nonlocal call_count
            call_count += 1
            if call_count < 2:
                raise MockRpcError(
                    grpc.StatusCode.UNAVAILABLE, "Service unavailable"
                )
            return "success"

        result = client._retry_call(eventually_successful_call, 10.0)
        assert result == "success"
        assert call_count == 2


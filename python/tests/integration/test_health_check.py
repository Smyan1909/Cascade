"""
Integration tests for health check functionality.
"""

import pytest

from cascade_client.grpc_client import CascadeGrpcClient


class TestHealthCheck:
    """Tests for health check utility."""

    def test_health_check_with_mock_server(self, mock_server):
        """Test health check against mock server."""
        client = CascadeGrpcClient(endpoint=mock_server)
        is_healthy = client.health_check()
        assert is_healthy is True
        client.close()

    def test_health_check_unavailable_server(self):
        """Test health check with unavailable server."""
        # Use a non-existent endpoint
        client = CascadeGrpcClient(endpoint="localhost:99999")
        is_healthy = client.health_check()
        assert is_healthy is False
        client.close()

    @pytest.mark.asyncio
    async def test_health_check_async(self, mock_server):
        """Test async health check."""
        client = CascadeGrpcClient(endpoint=mock_server)
        is_healthy = await client.health_check_async()
        assert is_healthy is True
        client.close()


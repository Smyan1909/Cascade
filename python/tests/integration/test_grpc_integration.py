"""
Integration tests for gRPC client using mock gRPC server.
"""

import pytest

from cascade_client.grpc_client import CascadeGrpcClient
from cascade_client.models import (
    Action,
    ActionType,
    PlatformSource,
    Selector,
)

try:
    from cascade_client.proto import cascade_pb2
except ImportError:
    pytest.skip("Proto stubs not generated", allow_module_level=True)


class TestGrpcIntegration:
    """Integration tests for gRPC client."""

    def test_start_app(self, mock_server):
        """Test StartApp RPC."""
        client = CascadeGrpcClient(endpoint=mock_server)
        status = client.start_app("calculator")
        assert status.success is True
        assert "calculator" in status.message.lower()
        client.close()

    def test_reset_state(self, mock_server):
        """Test ResetState RPC."""
        client = CascadeGrpcClient(endpoint=mock_server)
        status = client.reset_state()
        assert status.success is True
        client.close()

    def test_perform_action(self, mock_server):
        """Test PerformAction RPC."""
        client = CascadeGrpcClient(endpoint=mock_server)
        selector = Selector(platform_source=PlatformSource.WINDOWS, id="btn1")
        action = Action(action_type=ActionType.CLICK, selector=selector)
        status = client.perform_action(action)
        assert status.success is True
        client.close()

    def test_get_semantic_tree(self, mock_server):
        """Test GetSemanticTree RPC."""
        client = CascadeGrpcClient(endpoint=mock_server)
        tree = client.get_semantic_tree()
        assert len(tree.elements) > 0
        assert tree.elements[0].id == "elem1"
        assert tree.elements[0].name == "Test Button"
        client.close()

    def test_get_marked_screenshot(self, mock_server):
        """Test GetMarkedScreenshot RPC."""
        client = CascadeGrpcClient(endpoint=mock_server)
        proto_response = client.get_marked_screenshot()
        assert proto_response.image == b"fake_image_data"
        assert proto_response.format == cascade_pb2.ImageFormat.PNG
        assert len(proto_response.marks) == 1
        client.close()

    @pytest.mark.asyncio
    async def test_start_app_async(self, mock_server):
        """Test StartApp RPC (async)."""
        client = CascadeGrpcClient(endpoint=mock_server)
        status = await client.start_app_async("calculator")
        assert status.success is True
        client.close()

    @pytest.mark.asyncio
    async def test_get_semantic_tree_async(self, mock_server):
        """Test GetSemanticTree RPC (async)."""
        client = CascadeGrpcClient(endpoint=mock_server)
        tree = await client.get_semantic_tree_async()
        assert len(tree.elements) > 0
        client.close()

    def test_health_check(self, mock_server):
        """Test health check utility."""
        client = CascadeGrpcClient(endpoint=mock_server)
        is_healthy = client.health_check()
        assert is_healthy is True
        client.close()

    @pytest.mark.asyncio
    async def test_health_check_async(self, mock_server):
        """Test health check utility (async)."""
        client = CascadeGrpcClient(endpoint=mock_server)
        is_healthy = await client.health_check_async()
        assert is_healthy is True
        client.close()

    def test_context_manager(self, mock_server):
        """Test using client as context manager."""
        with CascadeGrpcClient(endpoint=mock_server) as client:
            status = client.start_app("test")
            assert status.success is True
        # Channel should be closed

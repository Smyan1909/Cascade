"""
Pytest fixtures for Cascade Python Client SDK tests.
"""

import os
from concurrent import futures
from unittest.mock import MagicMock

import grpc
import pytest

from cascade_client.auth.context import CascadeContext
from cascade_client.grpc_client import CascadeGrpcClient
from cascade_client.models import (
    ActionType,
    ControlType,
    ImageFormat,
    Mark,
    NormalizedRectangle,
    PlatformSource,
    Selector,
    UIElement,
)

try:
    from cascade_client.proto import cascade_pb2, cascade_pb2_grpc
    from google.protobuf import empty_pb2
except ImportError:
    cascade_pb2 = None
    cascade_pb2_grpc = None


@pytest.fixture
def test_context():
    """Create a test CascadeContext."""
    return CascadeContext(
        user_id="test_user",
        app_id="test_app",
        auth_token="test_token",
    )


@pytest.fixture
def mock_grpc_channel():
    """Create a mock gRPC channel."""
    return MagicMock()


@pytest.fixture
def mock_grpc_client(mock_grpc_channel):
    """Create a CascadeGrpcClient with mocked channel."""
    client = CascadeGrpcClient(endpoint="localhost:50051")
    client._channel = mock_grpc_channel
    return client


@pytest.fixture
def sample_ui_element():
    """Create a sample UIElement for testing."""
    return UIElement(
        id="elem1",
        name="Test Button",
        control_type=ControlType.BUTTON,
        bounding_box=NormalizedRectangle(x=0.1, y=0.2, width=0.3, height=0.4),
        parent_id="parent1",
        platform_source=PlatformSource.WINDOWS,
        aria_role="button",
        automation_id="btn_test",
        value_text="Click me",
    )


@pytest.fixture
def sample_selector():
    """Create a sample Selector for testing."""
    return Selector(
        platform_source=PlatformSource.WEB,
        path=["html", "body", "div"],
        id="btn_submit",
        name="Submit Button",
        control_type=ControlType.BUTTON,
        index=0,
        text_hint="Submit",
    )


@pytest.fixture
def sample_mark():
    """Create a sample Mark for testing."""
    return Mark(element_id="elem1", label="1")


@pytest.fixture
def env_vars(monkeypatch):
    """Set up environment variables for testing."""
    monkeypatch.setenv("CASCADE_GRPC_ENDPOINT", "localhost:50051")
    monkeypatch.setenv("CASCADE_USER_ID", "test_user")
    monkeypatch.setenv("CASCADE_APP_ID", "test_app")
    monkeypatch.setenv("CASCADE_AUTH_TOKEN", "test_token")


@pytest.fixture
def mock_server():
    """Create and start a mock gRPC server for integration tests."""
    if cascade_pb2 is None or cascade_pb2_grpc is None:
        pytest.skip("Proto stubs not generated")

    class MockSessionService(cascade_pb2_grpc.SessionServiceServicer):
        def StartApp(self, request, context):
            return cascade_pb2.Status(
                success=True, message=f"Started {request.app_name}"
            )

        def ResetState(self, request, context):
            return cascade_pb2.Status(success=True, message="State reset")

    class MockAutomationService(cascade_pb2_grpc.AutomationServiceServicer):
        def PerformAction(self, request, context):
            return cascade_pb2.Status(success=True, message="Action performed")

        def GetSemanticTree(self, request, context):
            element = cascade_pb2.UIElement(
                id="elem1",
                name="Test Button",
                control_type=cascade_pb2.ControlType.BUTTON,
                bounding_box=cascade_pb2.NormalizedRectangle(
                    x=0.1, y=0.2, width=0.3, height=0.4
                ),
                parent_id="",
                platform_source=cascade_pb2.PlatformSource.WINDOWS,
            )
            return cascade_pb2.SemanticTree(elements=[element])

    class MockVisionService(cascade_pb2_grpc.VisionServiceServicer):
        def GetMarkedScreenshot(self, request, context):
            mark = cascade_pb2.Mark(element_id="elem1", label="1")
            return cascade_pb2.Screenshot(
                image=b"fake_image_data",
                format=cascade_pb2.ImageFormat.PNG,
                marks=[mark],
            )

    server = grpc.server(futures.ThreadPoolExecutor(max_workers=10))
    cascade_pb2_grpc.add_SessionServiceServicer_to_server(
        MockSessionService(), server
    )
    cascade_pb2_grpc.add_AutomationServiceServicer_to_server(
        MockAutomationService(), server
    )
    cascade_pb2_grpc.add_VisionServiceServicer_to_server(
        MockVisionService(), server
    )
    port = server.add_insecure_port("[::]:0")
    server.start()
    yield f"localhost:{port}"
    server.stop(grace=1)


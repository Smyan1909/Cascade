"""
Main gRPC client for Cascade backend services.
"""

from typing import Any, Optional

import grpc

from cascade_agent.config import CascadeConfig, load_config


class CascadeClient:
    """
    Client for communicating with the Cascade C# backend via gRPC.
    
    This client provides access to:
    - UI Automation services
    - Vision/OCR services
    - Code generation services
    - Agent management services
    """
    
    def __init__(self, config: Optional[CascadeConfig] = None):
        """
        Initialize the Cascade client.
        
        Args:
            config: Configuration object. If not provided, loads from default location.
        """
        self.config = config or load_config()
        self._channel: Optional[grpc.aio.Channel] = None
        self._ui_stub = None
        self._vision_stub = None
        self._codegen_stub = None
        self._agent_stub = None
    
    async def connect(self) -> None:
        """Establish connection to the gRPC server."""
        server_address = f"{self.config.server.host}:{self.config.server.port}"
        
        if self.config.server.use_ssl and self.config.server.ssl_cert_path:
            with open(self.config.server.ssl_cert_path, "rb") as f:
                credentials = grpc.ssl_channel_credentials(f.read())
            self._channel = grpc.aio.secure_channel(server_address, credentials)
        else:
            self._channel = grpc.aio.insecure_channel(server_address)
        
        # Initialize stubs (will be implemented with generated code)
        # self._ui_stub = UIAutomationServiceStub(self._channel)
        # self._vision_stub = VisionServiceStub(self._channel)
        # self._codegen_stub = CodeGenServiceStub(self._channel)
        # self._agent_stub = AgentServiceStub(self._channel)
    
    async def disconnect(self) -> None:
        """Close the gRPC connection."""
        if self._channel:
            await self._channel.close()
            self._channel = None
    
    async def __aenter__(self) -> "CascadeClient":
        """Async context manager entry."""
        await self.connect()
        return self
    
    async def __aexit__(self, exc_type, exc_val, exc_tb) -> None:
        """Async context manager exit."""
        await self.disconnect()
    
    # UI Automation methods
    async def find_element(self, criteria: dict) -> dict:
        """Find a UI element by criteria."""
        # Implementation will use generated protobuf stubs
        raise NotImplementedError("Requires generated gRPC stubs")
    
    async def click(self, runtime_id: str) -> dict:
        """Click on a UI element."""
        raise NotImplementedError("Requires generated gRPC stubs")
    
    async def type_text(self, runtime_id: str, text: str) -> dict:
        """Type text into a UI element."""
        raise NotImplementedError("Requires generated gRPC stubs")
    
    async def capture_tree(self, max_depth: int = 10) -> dict:
        """Capture the UI element tree."""
        raise NotImplementedError("Requires generated gRPC stubs")
    
    # Vision methods
    async def capture_screenshot(self) -> dict:
        """Capture a screenshot of the foreground window."""
        raise NotImplementedError("Requires generated gRPC stubs")
    
    async def recognize_text(self, image_data: bytes) -> dict:
        """Perform OCR on an image."""
        raise NotImplementedError("Requires generated gRPC stubs")
    
    # Code generation methods
    async def compile(self, source_code: str) -> dict:
        """Compile C# source code."""
        raise NotImplementedError("Requires generated gRPC stubs")
    
    async def execute(self, script_id: str, method_name: str, variables: dict) -> dict:
        """Execute a compiled script."""
        raise NotImplementedError("Requires generated gRPC stubs")
    
    # Agent methods
    async def create_agent(self, **kwargs) -> dict:
        """Create a new agent."""
        raise NotImplementedError("Requires generated gRPC stubs")
    
    async def get_agent(self, agent_id: str = None, name: str = None) -> dict:
        """Get an agent by ID or name."""
        raise NotImplementedError("Requires generated gRPC stubs")
    
    async def get_agent_definition(self, agent_id: str) -> dict:
        """Get full agent definition including scripts."""
        raise NotImplementedError("Requires generated gRPC stubs")



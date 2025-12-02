"""
Base interfaces and types for LLM providers.
"""

from abc import ABC, abstractmethod
from dataclasses import dataclass, field
from enum import Enum
from typing import Any, AsyncIterator, Optional


class ProviderType(Enum):
    """Supported LLM providers."""
    OPENAI = "openai"
    ANTHROPIC = "anthropic"
    AZURE_OPENAI = "azure_openai"


@dataclass
class LLMConfig:
    """Configuration for an LLM provider."""
    provider: ProviderType
    api_key: str
    model: str
    
    # Optional settings
    base_url: Optional[str] = None
    organization: Optional[str] = None
    api_version: Optional[str] = None  # For Azure
    deployment_name: Optional[str] = None  # For Azure
    
    # Model settings
    temperature: float = 0.7
    max_tokens: int = 4096
    top_p: float = 1.0
    
    # Rate limiting
    requests_per_minute: int = 60
    tokens_per_minute: int = 100000
    
    # Retry settings
    max_retries: int = 3
    retry_delay: float = 1.0
    
    # Cost tracking
    input_cost_per_1k: float = 0.0
    output_cost_per_1k: float = 0.0


@dataclass
class Message:
    """A chat message."""
    role: str  # "system", "user", "assistant"
    content: str
    name: Optional[str] = None
    function_call: Optional[dict] = None
    tool_calls: Optional[list[dict]] = None


@dataclass
class LLMResponse:
    """Response from an LLM."""
    content: str
    model: str
    provider: ProviderType
    
    # Token usage
    input_tokens: int = 0
    output_tokens: int = 0
    total_tokens: int = 0
    
    # Timing
    latency_ms: float = 0.0
    
    # Cost
    estimated_cost: float = 0.0
    
    # Additional info
    finish_reason: Optional[str] = None
    function_call: Optional[dict] = None
    tool_calls: Optional[list[dict]] = None
    raw_response: Optional[Any] = None


@dataclass
class StreamChunk:
    """A chunk from streaming response."""
    content: str
    is_final: bool = False
    finish_reason: Optional[str] = None


class BaseLLMProvider(ABC):
    """Base class for LLM providers."""
    
    def __init__(self, config: LLMConfig):
        self.config = config
    
    @property
    @abstractmethod
    def provider_type(self) -> ProviderType:
        """Get the provider type."""
        pass
    
    @abstractmethod
    async def complete(
        self,
        messages: list[Message],
        **kwargs
    ) -> LLMResponse:
        """Generate a completion."""
        pass
    
    @abstractmethod
    async def stream(
        self,
        messages: list[Message],
        **kwargs
    ) -> AsyncIterator[StreamChunk]:
        """Generate a streaming completion."""
        pass
    
    @abstractmethod
    async def count_tokens(self, text: str) -> int:
        """Count tokens in text."""
        pass
    
    @abstractmethod
    def is_available(self) -> bool:
        """Check if the provider is available."""
        pass


class LLMMiddleware(ABC):
    """Base class for middleware."""
    
    @abstractmethod
    async def before_request(
        self,
        messages: list[Message],
        **kwargs
    ) -> tuple[list[Message], dict]:
        """Process before sending request."""
        pass
    
    @abstractmethod
    async def after_response(
        self,
        response: LLMResponse
    ) -> LLMResponse:
        """Process after receiving response."""
        pass



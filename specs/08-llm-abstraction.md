# LLM Abstraction Layer Specification

## Overview

The LLM Abstraction Layer provides a unified interface for multiple LLM providers (OpenAI, Anthropic, Azure OpenAI), enabling provider switching, fallback chains, token management, and cost tracking. Prompts now include hints about the session state (e.g., “You are running in a hidden desktop session”) so reasoning steps account for virtualized UI.

## Architecture

```
python/cascade_agent/llm/
├── __init__.py
├── base.py                     # Base interfaces and types
├── factory.py                  # Provider factory
├── config.py                   # Configuration management
├── providers/
│   ├── __init__.py
│   ├── openai_provider.py      # OpenAI implementation
│   ├── anthropic_provider.py   # Anthropic implementation
│   └── azure_provider.py       # Azure OpenAI implementation
├── middleware/
│   ├── __init__.py
│   ├── rate_limiter.py         # Rate limiting
│   ├── token_counter.py        # Token management
│   ├── cost_tracker.py         # Cost tracking
│   └── retry_handler.py        # Retry logic
├── fallback/
│   ├── __init__.py
│   └── chain.py                # Fallback chain management
└── utils/
    ├── __init__.py
    ├── token_utils.py          # Token counting utilities
    └── response_parser.py      # Response parsing
```

## Session Context Awareness

- Tool invocations include `session_id` metadata so the LLM can reason about long-running tasks across sessions.
- The abstraction exposes a helper `build_session_system_prompt(SessionHandle)` that agents call before each run. This reminds the LLM that automation happens off-screen and the user stays in control.
- When sessions are recycled, the LLM is told to reorient itself by summarizing prior findings before continuing execution.

## Base Interfaces

```python
from abc import ABC, abstractmethod
from dataclasses import dataclass, field
from typing import Optional, AsyncIterator, Any
from enum import Enum

class ProviderType(Enum):
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
```

## Provider Implementations

### OpenAI Provider

```python
from openai import AsyncOpenAI
import tiktoken

class OpenAIProvider(BaseLLMProvider):
    """OpenAI API provider."""
    
    def __init__(self, config: LLMConfig):
        super().__init__(config)
        self.client = AsyncOpenAI(
            api_key=config.api_key,
            organization=config.organization,
            base_url=config.base_url
        )
        self._encoder = None
    
    @property
    def provider_type(self) -> ProviderType:
        return ProviderType.OPENAI
    
    async def complete(
        self,
        messages: list[Message],
        **kwargs
    ) -> LLMResponse:
        import time
        start_time = time.time()
        
        # Convert messages to OpenAI format
        openai_messages = [
            {"role": m.role, "content": m.content}
            for m in messages
        ]
        
        # Merge config with kwargs
        params = {
            "model": kwargs.get("model", self.config.model),
            "messages": openai_messages,
            "temperature": kwargs.get("temperature", self.config.temperature),
            "max_tokens": kwargs.get("max_tokens", self.config.max_tokens),
            "top_p": kwargs.get("top_p", self.config.top_p),
        }
        
        # Add optional parameters
        if kwargs.get("tools"):
            params["tools"] = kwargs["tools"]
        if kwargs.get("tool_choice"):
            params["tool_choice"] = kwargs["tool_choice"]
        if kwargs.get("response_format"):
            params["response_format"] = kwargs["response_format"]
        
        response = await self.client.chat.completions.create(**params)
        
        latency = (time.time() - start_time) * 1000
        
        # Calculate cost
        input_cost = (response.usage.prompt_tokens / 1000) * self.config.input_cost_per_1k
        output_cost = (response.usage.completion_tokens / 1000) * self.config.output_cost_per_1k
        
        return LLMResponse(
            content=response.choices[0].message.content or "",
            model=response.model,
            provider=ProviderType.OPENAI,
            input_tokens=response.usage.prompt_tokens,
            output_tokens=response.usage.completion_tokens,
            total_tokens=response.usage.total_tokens,
            latency_ms=latency,
            estimated_cost=input_cost + output_cost,
            finish_reason=response.choices[0].finish_reason,
            tool_calls=[tc.model_dump() for tc in response.choices[0].message.tool_calls] if response.choices[0].message.tool_calls else None,
            raw_response=response
        )
    
    async def stream(
        self,
        messages: list[Message],
        **kwargs
    ) -> AsyncIterator[StreamChunk]:
        openai_messages = [
            {"role": m.role, "content": m.content}
            for m in messages
        ]
        
        params = {
            "model": kwargs.get("model", self.config.model),
            "messages": openai_messages,
            "temperature": kwargs.get("temperature", self.config.temperature),
            "max_tokens": kwargs.get("max_tokens", self.config.max_tokens),
            "stream": True
        }
        
        stream = await self.client.chat.completions.create(**params)
        
        async for chunk in stream:
            if chunk.choices[0].delta.content:
                yield StreamChunk(
                    content=chunk.choices[0].delta.content,
                    is_final=chunk.choices[0].finish_reason is not None,
                    finish_reason=chunk.choices[0].finish_reason
                )
    
    async def count_tokens(self, text: str) -> int:
        if self._encoder is None:
            try:
                self._encoder = tiktoken.encoding_for_model(self.config.model)
            except KeyError:
                self._encoder = tiktoken.get_encoding("cl100k_base")
        return len(self._encoder.encode(text))
    
    def is_available(self) -> bool:
        return bool(self.config.api_key)
```

### Anthropic Provider

```python
from anthropic import AsyncAnthropic

class AnthropicProvider(BaseLLMProvider):
    """Anthropic Claude API provider."""
    
    def __init__(self, config: LLMConfig):
        super().__init__(config)
        self.client = AsyncAnthropic(api_key=config.api_key)
    
    @property
    def provider_type(self) -> ProviderType:
        return ProviderType.ANTHROPIC
    
    async def complete(
        self,
        messages: list[Message],
        **kwargs
    ) -> LLMResponse:
        import time
        start_time = time.time()
        
        # Extract system message
        system_message = None
        chat_messages = []
        
        for m in messages:
            if m.role == "system":
                system_message = m.content
            else:
                chat_messages.append({
                    "role": m.role,
                    "content": m.content
                })
        
        params = {
            "model": kwargs.get("model", self.config.model),
            "messages": chat_messages,
            "max_tokens": kwargs.get("max_tokens", self.config.max_tokens),
            "temperature": kwargs.get("temperature", self.config.temperature),
        }
        
        if system_message:
            params["system"] = system_message
        
        if kwargs.get("tools"):
            params["tools"] = self._convert_tools(kwargs["tools"])
        
        response = await self.client.messages.create(**params)
        
        latency = (time.time() - start_time) * 1000
        
        # Extract content
        content = ""
        tool_calls = None
        
        for block in response.content:
            if block.type == "text":
                content += block.text
            elif block.type == "tool_use":
                if tool_calls is None:
                    tool_calls = []
                tool_calls.append({
                    "id": block.id,
                    "name": block.name,
                    "arguments": block.input
                })
        
        # Calculate cost
        input_cost = (response.usage.input_tokens / 1000) * self.config.input_cost_per_1k
        output_cost = (response.usage.output_tokens / 1000) * self.config.output_cost_per_1k
        
        return LLMResponse(
            content=content,
            model=response.model,
            provider=ProviderType.ANTHROPIC,
            input_tokens=response.usage.input_tokens,
            output_tokens=response.usage.output_tokens,
            total_tokens=response.usage.input_tokens + response.usage.output_tokens,
            latency_ms=latency,
            estimated_cost=input_cost + output_cost,
            finish_reason=response.stop_reason,
            tool_calls=tool_calls,
            raw_response=response
        )
    
    async def stream(
        self,
        messages: list[Message],
        **kwargs
    ) -> AsyncIterator[StreamChunk]:
        system_message = None
        chat_messages = []
        
        for m in messages:
            if m.role == "system":
                system_message = m.content
            else:
                chat_messages.append({"role": m.role, "content": m.content})
        
        params = {
            "model": kwargs.get("model", self.config.model),
            "messages": chat_messages,
            "max_tokens": kwargs.get("max_tokens", self.config.max_tokens),
            "stream": True
        }
        
        if system_message:
            params["system"] = system_message
        
        async with self.client.messages.stream(**params) as stream:
            async for text in stream.text_stream:
                yield StreamChunk(content=text)
    
    async def count_tokens(self, text: str) -> int:
        # Anthropic doesn't provide a public tokenizer
        # Estimate using character count (roughly 4 chars per token)
        return len(text) // 4
    
    def is_available(self) -> bool:
        return bool(self.config.api_key)
    
    def _convert_tools(self, openai_tools: list[dict]) -> list[dict]:
        """Convert OpenAI tool format to Anthropic format."""
        anthropic_tools = []
        for tool in openai_tools:
            if tool["type"] == "function":
                anthropic_tools.append({
                    "name": tool["function"]["name"],
                    "description": tool["function"].get("description", ""),
                    "input_schema": tool["function"].get("parameters", {})
                })
        return anthropic_tools
```

### Azure OpenAI Provider

```python
from openai import AsyncAzureOpenAI

class AzureOpenAIProvider(BaseLLMProvider):
    """Azure OpenAI API provider."""
    
    def __init__(self, config: LLMConfig):
        super().__init__(config)
        self.client = AsyncAzureOpenAI(
            api_key=config.api_key,
            api_version=config.api_version or "2024-02-15-preview",
            azure_endpoint=config.base_url
        )
    
    @property
    def provider_type(self) -> ProviderType:
        return ProviderType.AZURE_OPENAI
    
    async def complete(
        self,
        messages: list[Message],
        **kwargs
    ) -> LLMResponse:
        import time
        start_time = time.time()
        
        openai_messages = [
            {"role": m.role, "content": m.content}
            for m in messages
        ]
        
        # Use deployment name for Azure
        model = kwargs.get("model", self.config.deployment_name or self.config.model)
        
        params = {
            "model": model,
            "messages": openai_messages,
            "temperature": kwargs.get("temperature", self.config.temperature),
            "max_tokens": kwargs.get("max_tokens", self.config.max_tokens),
        }
        
        response = await self.client.chat.completions.create(**params)
        
        latency = (time.time() - start_time) * 1000
        
        return LLMResponse(
            content=response.choices[0].message.content or "",
            model=response.model,
            provider=ProviderType.AZURE_OPENAI,
            input_tokens=response.usage.prompt_tokens,
            output_tokens=response.usage.completion_tokens,
            total_tokens=response.usage.total_tokens,
            latency_ms=latency,
            finish_reason=response.choices[0].finish_reason,
            raw_response=response
        )
    
    # stream() and count_tokens() similar to OpenAI provider
    
    def is_available(self) -> bool:
        return bool(self.config.api_key and self.config.base_url)
```

## Provider Factory

```python
class LLMFactory:
    """Factory for creating LLM providers."""
    
    _providers: dict[ProviderType, type[BaseLLMProvider]] = {
        ProviderType.OPENAI: OpenAIProvider,
        ProviderType.ANTHROPIC: AnthropicProvider,
        ProviderType.AZURE_OPENAI: AzureOpenAIProvider,
    }
    
    @classmethod
    def create(cls, config: LLMConfig) -> BaseLLMProvider:
        """Create a provider instance."""
        provider_class = cls._providers.get(config.provider)
        if not provider_class:
            raise ValueError(f"Unknown provider: {config.provider}")
        return provider_class(config)
    
    @classmethod
    def register(cls, provider_type: ProviderType, provider_class: type[BaseLLMProvider]):
        """Register a custom provider."""
        cls._providers[provider_type] = provider_class
```

## Middleware

### Rate Limiter

```python
import asyncio
from collections import deque
from datetime import datetime, timedelta

class RateLimiter(LLMMiddleware):
    """Rate limiting middleware."""
    
    def __init__(self, requests_per_minute: int, tokens_per_minute: int):
        self.rpm = requests_per_minute
        self.tpm = tokens_per_minute
        self._request_times: deque = deque()
        self._token_usage: deque = deque()
        self._lock = asyncio.Lock()
    
    async def before_request(
        self,
        messages: list[Message],
        **kwargs
    ) -> tuple[list[Message], dict]:
        async with self._lock:
            now = datetime.now()
            minute_ago = now - timedelta(minutes=1)
            
            # Clean old entries
            while self._request_times and self._request_times[0] < minute_ago:
                self._request_times.popleft()
            while self._token_usage and self._token_usage[0][0] < minute_ago:
                self._token_usage.popleft()
            
            # Check request limit
            if len(self._request_times) >= self.rpm:
                wait_time = (self._request_times[0] - minute_ago).total_seconds()
                await asyncio.sleep(wait_time)
            
            # Check token limit
            current_tokens = sum(t[1] for t in self._token_usage)
            if current_tokens >= self.tpm:
                wait_time = (self._token_usage[0][0] - minute_ago).total_seconds()
                await asyncio.sleep(wait_time)
            
            self._request_times.append(now)
        
        return messages, kwargs
    
    async def after_response(self, response: LLMResponse) -> LLMResponse:
        async with self._lock:
            self._token_usage.append((datetime.now(), response.total_tokens))
        return response
```

### Cost Tracker

```python
from dataclasses import dataclass, field
from typing import Optional

@dataclass
class UsageStats:
    """Usage statistics."""
    total_requests: int = 0
    total_input_tokens: int = 0
    total_output_tokens: int = 0
    total_cost: float = 0.0
    by_provider: dict = field(default_factory=dict)
    by_model: dict = field(default_factory=dict)


class CostTracker(LLMMiddleware):
    """Cost tracking middleware."""
    
    def __init__(self):
        self.stats = UsageStats()
        self._session_stats: dict[str, UsageStats] = {}
    
    async def before_request(
        self,
        messages: list[Message],
        **kwargs
    ) -> tuple[list[Message], dict]:
        return messages, kwargs
    
    async def after_response(self, response: LLMResponse) -> LLMResponse:
        # Update global stats
        self.stats.total_requests += 1
        self.stats.total_input_tokens += response.input_tokens
        self.stats.total_output_tokens += response.output_tokens
        self.stats.total_cost += response.estimated_cost
        
        # Update by provider
        provider = response.provider.value
        if provider not in self.stats.by_provider:
            self.stats.by_provider[provider] = UsageStats()
        self.stats.by_provider[provider].total_requests += 1
        self.stats.by_provider[provider].total_cost += response.estimated_cost
        
        # Update by model
        model = response.model
        if model not in self.stats.by_model:
            self.stats.by_model[model] = UsageStats()
        self.stats.by_model[model].total_requests += 1
        self.stats.by_model[model].total_cost += response.estimated_cost
        
        return response
    
    def get_stats(self) -> UsageStats:
        return self.stats
    
    def reset_stats(self):
        self.stats = UsageStats()
```

## Fallback Chain

```python
class FallbackChain:
    """Manages fallback between providers."""
    
    def __init__(self, providers: list[BaseLLMProvider]):
        self.providers = providers
    
    async def complete(
        self,
        messages: list[Message],
        **kwargs
    ) -> LLMResponse:
        """Try providers in order until one succeeds."""
        
        last_error = None
        
        for provider in self.providers:
            if not provider.is_available():
                continue
            
            try:
                return await provider.complete(messages, **kwargs)
            except Exception as e:
                last_error = e
                continue
        
        raise last_error or Exception("No available providers")
    
    async def complete_with_best_model(
        self,
        messages: list[Message],
        task_type: str = "general",
        **kwargs
    ) -> LLMResponse:
        """Select best model for task type."""
        
        model_preferences = {
            "reasoning": [ProviderType.ANTHROPIC, ProviderType.OPENAI],
            "coding": [ProviderType.ANTHROPIC, ProviderType.OPENAI],
            "general": [ProviderType.OPENAI, ProviderType.ANTHROPIC],
        }
        
        preferred_order = model_preferences.get(task_type, model_preferences["general"])
        
        ordered_providers = sorted(
            self.providers,
            key=lambda p: preferred_order.index(p.provider_type) if p.provider_type in preferred_order else 999
        )
        
        for provider in ordered_providers:
            if provider.is_available():
                try:
                    return await provider.complete(messages, **kwargs)
                except Exception:
                    continue
        
        raise Exception("No available providers")
```

## Unified LLM Client

```python
class LLMClient:
    """Unified LLM client with middleware support."""
    
    def __init__(
        self,
        providers: list[LLMConfig],
        middleware: list[LLMMiddleware] = None
    ):
        self._providers = [LLMFactory.create(c) for c in providers]
        self._fallback_chain = FallbackChain(self._providers)
        self._middleware = middleware or []
    
    async def complete(
        self,
        messages: list[Message],
        provider: ProviderType = None,
        **kwargs
    ) -> LLMResponse:
        """Generate completion with middleware processing."""
        
        # Apply before middleware
        for mw in self._middleware:
            messages, kwargs = await mw.before_request(messages, **kwargs)
        
        # Execute request
        if provider:
            p = next((p for p in self._providers if p.provider_type == provider), None)
            if not p:
                raise ValueError(f"Provider not configured: {provider}")
            response = await p.complete(messages, **kwargs)
        else:
            response = await self._fallback_chain.complete(messages, **kwargs)
        
        # Apply after middleware
        for mw in self._middleware:
            response = await mw.after_response(response)
        
        return response
    
    async def ainvoke(self, prompt: str, **kwargs) -> str:
        """Simple interface matching LangChain."""
        messages = [Message(role="user", content=prompt)]
        response = await self.complete(messages, **kwargs)
        return response.content
    
    def get_provider(self, provider_type: ProviderType) -> Optional[BaseLLMProvider]:
        return next((p for p in self._providers if p.provider_type == provider_type), None)


# Convenience function
def get_llm(config_path: str = None) -> LLMClient:
    """Get configured LLM client."""
    config = load_config(config_path)
    
    providers = []
    if config.get("openai"):
        providers.append(LLMConfig(
            provider=ProviderType.OPENAI,
            **config["openai"]
        ))
    if config.get("anthropic"):
        providers.append(LLMConfig(
            provider=ProviderType.ANTHROPIC,
            **config["anthropic"]
        ))
    if config.get("azure"):
        providers.append(LLMConfig(
            provider=ProviderType.AZURE_OPENAI,
            **config["azure"]
        ))
    
    middleware = [
        RateLimiter(
            requests_per_minute=config.get("rate_limit_rpm", 60),
            tokens_per_minute=config.get("rate_limit_tpm", 100000)
        ),
        CostTracker()
    ]
    
    return LLMClient(providers, middleware)
```

## Configuration Example

```yaml
# cascade_config.yaml
llm:
  openai:
    api_key: ${OPENAI_API_KEY}
    model: gpt-4o
    temperature: 0.7
    max_tokens: 4096
    input_cost_per_1k: 0.005
    output_cost_per_1k: 0.015
  
  anthropic:
    api_key: ${ANTHROPIC_API_KEY}
    model: claude-3-5-sonnet-20241022
    temperature: 0.7
    max_tokens: 4096
    input_cost_per_1k: 0.003
    output_cost_per_1k: 0.015
  
  azure:
    api_key: ${AZURE_OPENAI_KEY}
    base_url: https://your-resource.openai.azure.com/
    api_version: "2024-02-15-preview"
    deployment_name: gpt-4
    model: gpt-4
  
  rate_limit_rpm: 60
  rate_limit_tpm: 100000
  
  fallback_order:
    - openai
    - anthropic
    - azure
```



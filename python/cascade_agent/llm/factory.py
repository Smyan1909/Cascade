"""
Factory for creating LLM providers.
"""

from typing import Optional

from cascade_agent.config import CascadeConfig, load_config
from cascade_agent.llm.base import BaseLLMProvider, LLMConfig, LLMResponse, Message, ProviderType


class LLMFactory:
    """Factory for creating LLM provider instances."""
    
    _providers: dict[ProviderType, type[BaseLLMProvider]] = {}
    
    @classmethod
    def register(cls, provider_type: ProviderType, provider_class: type[BaseLLMProvider]) -> None:
        """Register a provider class."""
        cls._providers[provider_type] = provider_class
    
    @classmethod
    def create(cls, config: LLMConfig) -> BaseLLMProvider:
        """Create a provider instance."""
        provider_class = cls._providers.get(config.provider)
        if not provider_class:
            raise ValueError(f"Unknown provider: {config.provider}")
        return provider_class(config)
    
    @classmethod
    def get_available_providers(cls) -> list[ProviderType]:
        """Get list of registered providers."""
        return list(cls._providers.keys())


class LLMClient:
    """Unified LLM client with fallback support."""
    
    def __init__(
        self,
        providers: list[BaseLLMProvider],
        middleware: list = None
    ):
        self._providers = providers
        self._middleware = middleware or []
    
    async def complete(
        self,
        messages: list[Message],
        provider: Optional[ProviderType] = None,
        **kwargs
    ) -> LLMResponse:
        """Generate completion with fallback support."""
        # Apply before middleware
        for mw in self._middleware:
            messages, kwargs = await mw.before_request(messages, **kwargs)
        
        # Select provider
        if provider:
            selected = next((p for p in self._providers if p.provider_type == provider), None)
            if not selected:
                raise ValueError(f"Provider not configured: {provider}")
            providers_to_try = [selected]
        else:
            providers_to_try = [p for p in self._providers if p.is_available()]
        
        # Try providers in order
        last_error = None
        for p in providers_to_try:
            try:
                response = await p.complete(messages, **kwargs)
                
                # Apply after middleware
                for mw in self._middleware:
                    response = await mw.after_response(response)
                
                return response
            except Exception as e:
                last_error = e
                continue
        
        raise last_error or Exception("No available providers")
    
    async def ainvoke(self, prompt: str, **kwargs) -> str:
        """Simple interface for single prompt."""
        messages = [Message(role="user", content=prompt)]
        response = await self.complete(messages, **kwargs)
        return response.content


def get_llm(config: Optional[CascadeConfig] = None) -> LLMClient:
    """
    Get a configured LLM client.
    
    Args:
        config: Configuration object. If not provided, loads from default location.
    
    Returns:
        Configured LLMClient instance
    """
    if config is None:
        config = load_config()
    
    providers = []
    
    # Try to import and register providers
    try:
        from cascade_agent.llm.openai_provider import OpenAIProvider
        LLMFactory.register(ProviderType.OPENAI, OpenAIProvider)
        
        if config.llm.openai.api_key:
            providers.append(LLMFactory.create(LLMConfig(
                provider=ProviderType.OPENAI,
                api_key=config.llm.openai.api_key,
                model=config.llm.openai.model,
                temperature=config.llm.openai.temperature,
                max_tokens=config.llm.openai.max_tokens
            )))
    except ImportError:
        pass
    
    try:
        from cascade_agent.llm.anthropic_provider import AnthropicProvider
        LLMFactory.register(ProviderType.ANTHROPIC, AnthropicProvider)
        
        if config.llm.anthropic.api_key:
            providers.append(LLMFactory.create(LLMConfig(
                provider=ProviderType.ANTHROPIC,
                api_key=config.llm.anthropic.api_key,
                model=config.llm.anthropic.model,
                temperature=config.llm.anthropic.temperature,
                max_tokens=config.llm.anthropic.max_tokens
            )))
    except ImportError:
        pass
    
    if not providers:
        raise RuntimeError("No LLM providers configured. Set API keys in environment or config.")
    
    return LLMClient(providers)



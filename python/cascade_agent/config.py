"""
Configuration management for Cascade Agent.
"""

import os
from dataclasses import dataclass, field
from pathlib import Path
from typing import Optional

import yaml
from dotenv import load_dotenv


@dataclass
class ServerConfig:
    """gRPC server configuration."""
    host: str = "localhost"
    port: int = 50051
    use_ssl: bool = False
    ssl_cert_path: Optional[str] = None


@dataclass
class OpenAIConfig:
    """OpenAI provider configuration."""
    api_key: str = ""
    model: str = "gpt-4o"
    temperature: float = 0.7
    max_tokens: int = 4096


@dataclass
class AnthropicConfig:
    """Anthropic provider configuration."""
    api_key: str = ""
    model: str = "claude-3-5-sonnet-20241022"
    temperature: float = 0.7
    max_tokens: int = 4096


@dataclass
class AzureOpenAIConfig:
    """Azure OpenAI provider configuration."""
    api_key: str = ""
    endpoint: str = ""
    api_version: str = "2024-02-15-preview"
    deployment_name: str = ""


@dataclass
class LLMConfig:
    """LLM configuration."""
    openai: OpenAIConfig = field(default_factory=OpenAIConfig)
    anthropic: AnthropicConfig = field(default_factory=AnthropicConfig)
    azure: AzureOpenAIConfig = field(default_factory=AzureOpenAIConfig)
    default_provider: str = "openai"
    fallback_enabled: bool = True


@dataclass
class AgentConfig:
    """Agent execution configuration."""
    max_retries: int = 3
    timeout_seconds: int = 300
    enable_logging: bool = True
    log_level: str = "INFO"


@dataclass
class ExplorationConfig:
    """Exploration agent configuration."""
    max_depth: int = 10
    screenshot_interval: int = 1000
    ocr_enabled: bool = True


@dataclass
class LoggingConfig:
    """Logging configuration."""
    level: str = "INFO"
    format: str = "%(asctime)s - %(name)s - %(levelname)s - %(message)s"
    file: Optional[str] = None


@dataclass
class CascadeConfig:
    """Main Cascade configuration."""
    server: ServerConfig = field(default_factory=ServerConfig)
    llm: LLMConfig = field(default_factory=LLMConfig)
    agent: AgentConfig = field(default_factory=AgentConfig)
    exploration: ExplorationConfig = field(default_factory=ExplorationConfig)
    logging: LoggingConfig = field(default_factory=LoggingConfig)


def load_config(config_path: Optional[str] = None) -> CascadeConfig:
    """
    Load configuration from file and environment variables.
    
    Args:
        config_path: Path to YAML configuration file. If not provided,
                    looks for cascade_config.yaml in current directory.
    
    Returns:
        CascadeConfig instance
    """
    # Load environment variables
    load_dotenv()
    
    config = CascadeConfig()
    
    # Try to load from file
    if config_path is None:
        config_path = os.environ.get("CASCADE_CONFIG_PATH", "cascade_config.yaml")
    
    config_file = Path(config_path)
    if config_file.exists():
        with open(config_file) as f:
            yaml_config = yaml.safe_load(f)
            if yaml_config:
                _apply_yaml_config(config, yaml_config)
    
    # Override with environment variables
    _apply_env_overrides(config)
    
    return config


def _apply_yaml_config(config: CascadeConfig, yaml_config: dict) -> None:
    """Apply YAML configuration to config object."""
    if "server" in yaml_config:
        for key, value in yaml_config["server"].items():
            if hasattr(config.server, key):
                setattr(config.server, key, value)
    
    if "llm" in yaml_config:
        llm_config = yaml_config["llm"]
        if "openai" in llm_config:
            for key, value in llm_config["openai"].items():
                if hasattr(config.llm.openai, key):
                    setattr(config.llm.openai, key, value)
        if "anthropic" in llm_config:
            for key, value in llm_config["anthropic"].items():
                if hasattr(config.llm.anthropic, key):
                    setattr(config.llm.anthropic, key, value)
        if "azure" in llm_config:
            for key, value in llm_config["azure"].items():
                if hasattr(config.llm.azure, key):
                    setattr(config.llm.azure, key, value)
        if "default_provider" in llm_config:
            config.llm.default_provider = llm_config["default_provider"]
        if "fallback_enabled" in llm_config:
            config.llm.fallback_enabled = llm_config["fallback_enabled"]
    
    if "agent" in yaml_config:
        for key, value in yaml_config["agent"].items():
            if hasattr(config.agent, key):
                setattr(config.agent, key, value)
    
    if "exploration" in yaml_config:
        for key, value in yaml_config["exploration"].items():
            if hasattr(config.exploration, key):
                setattr(config.exploration, key, value)
    
    if "logging" in yaml_config:
        for key, value in yaml_config["logging"].items():
            if hasattr(config.logging, key):
                setattr(config.logging, key, value)


def _apply_env_overrides(config: CascadeConfig) -> None:
    """Apply environment variable overrides."""
    # Server
    if host := os.environ.get("CASCADE_SERVER_HOST"):
        config.server.host = host
    if port := os.environ.get("CASCADE_SERVER_PORT"):
        config.server.port = int(port)
    
    # OpenAI
    if api_key := os.environ.get("OPENAI_API_KEY"):
        config.llm.openai.api_key = api_key
    if model := os.environ.get("OPENAI_MODEL"):
        config.llm.openai.model = model
    
    # Anthropic
    if api_key := os.environ.get("ANTHROPIC_API_KEY"):
        config.llm.anthropic.api_key = api_key
    if model := os.environ.get("ANTHROPIC_MODEL"):
        config.llm.anthropic.model = model
    
    # Azure OpenAI
    if api_key := os.environ.get("AZURE_OPENAI_KEY"):
        config.llm.azure.api_key = api_key
    if endpoint := os.environ.get("AZURE_OPENAI_ENDPOINT"):
        config.llm.azure.endpoint = endpoint
    
    # Default provider
    if provider := os.environ.get("CASCADE_LLM_PROVIDER"):
        config.llm.default_provider = provider



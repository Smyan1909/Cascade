"""
Cascade Agent - AI Agent Builder using LangGraph

This package provides LangGraph-based agents for building and running
specialized Windows automation agents.
"""

__version__ = "0.1.0"
__author__ = "Cascade Team"

from cascade_agent.config import CascadeConfig, load_config

__all__ = [
    "CascadeConfig",
    "load_config",
    "__version__",
]



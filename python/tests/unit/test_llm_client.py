import os

import pytest

from clients.llm_client import load_llm_client_from_env


def test_load_llm_client_unsupported_provider(monkeypatch):
    monkeypatch.setenv("CASCADE_MODEL_PROVIDER", "unsupported")
    with pytest.raises(ValueError):
        load_llm_client_from_env()


def test_load_llm_client_missing_provider(monkeypatch):
    monkeypatch.delenv("CASCADE_MODEL_PROVIDER", raising=False)
    with pytest.raises(ValueError):
        load_llm_client_from_env()


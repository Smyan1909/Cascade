"""
Unit tests for auth/context.py (CascadeContext).
"""

import os

import pytest

from cascade_client.auth.context import CascadeContext


class TestCascadeContext:
    """Tests for CascadeContext class."""

    def test_create_from_params(self):
        """Test creating context from parameters."""
        context = CascadeContext(
            user_id="user1", app_id="app1", auth_token="token123"
        )
        assert context.user_id == "user1"
        assert context.app_id == "app1"
        assert context.auth_token == "token123"

    def test_create_from_env(self, env_vars):
        """Test creating context from environment variables."""
        context = CascadeContext.from_env()
        assert context.user_id == "test_user"
        assert context.app_id == "test_app"
        assert context.auth_token == "test_token"

    def test_create_from_env_missing(self, monkeypatch):
        """Test creating context from env with missing variables."""
        # Remove all env vars
        monkeypatch.delenv("CASCADE_USER_ID", raising=False)
        monkeypatch.delenv("CASCADE_APP_ID", raising=False)
        monkeypatch.delenv("CASCADE_AUTH_TOKEN", raising=False)

        with pytest.raises(ValueError, match="CASCADE_USER_ID"):
            CascadeContext.from_env()

    def test_validation_empty_string(self):
        """Test that empty strings are rejected."""
        with pytest.raises(Exception):  # Pydantic validation error
            CascadeContext(user_id="", app_id="app1", auth_token="token1")

        with pytest.raises(Exception):
            CascadeContext(user_id="user1", app_id="", auth_token="token1")

        with pytest.raises(Exception):
            CascadeContext(user_id="user1", app_id="app1", auth_token="")

    def test_get_firestore_config(self):
        """Test getting Firestore configuration."""
        context = CascadeContext(
            user_id="user1", app_id="app1", auth_token="token123"
        )
        config = context.get_firestore_config()
        assert config["auth_token"] == "token123"
        assert config["project_id"] == "app1"

    def test_get_firestore_path_prefix(self):
        """Test getting Firestore path prefix."""
        context = CascadeContext(
            user_id="user1", app_id="app1", auth_token="token123"
        )
        prefix = context.get_firestore_path_prefix()
        assert prefix == "/artifacts/app1/users/user1"

    def test_get_skill_map_path(self):
        """Test getting skill map path."""
        context = CascadeContext(
            user_id="user1", app_id="app1", auth_token="token123"
        )
        path = context.get_skill_map_path("skill1")
        assert path == "/artifacts/app1/users/user1/skill_maps/skill1"

    def test_get_worker_checkpoint_path(self):
        """Test getting worker checkpoint path."""
        context = CascadeContext(
            user_id="user1", app_id="app1", auth_token="token123"
        )
        path = context.get_worker_checkpoint_path("run1")
        assert path == "/artifacts/app1/users/user1/worker_checkpoints/run1"

    def test_get_explorer_checkpoint_path(self):
        """Test getting explorer checkpoint path."""
        context = CascadeContext(
            user_id="user1", app_id="app1", auth_token="token123"
        )
        path = context.get_explorer_checkpoint_path("run1")
        assert path == "/artifacts/app1/users/user1/explorer_checkpoints/run1"

    def test_get_orchestrator_checkpoint_path(self):
        """Test getting orchestrator checkpoint path."""
        context = CascadeContext(
            user_id="user1", app_id="app1", auth_token="token123"
        )
        path = context.get_orchestrator_checkpoint_path("run1")
        assert path == "/artifacts/app1/users/user1/orchestrator_checkpoints/run1"


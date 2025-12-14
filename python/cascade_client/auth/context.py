"""
Auth and context handling for Cascade.

Provides CascadeContext class for managing user/app/auth token context
used across Explorer, Worker, and Orchestrator agents.
"""

import os
from typing import Dict, Optional

from pydantic import BaseModel, Field, field_validator


class CascadeContext(BaseModel):
    """
    Context object holding user/app/auth token information.

    This context is used to:
    - Initialize Firestore clients with proper user scoping
    - Carry authentication information across agent layers
    - Ensure all persistence operations include user/app context
    """

    user_id: str = Field(..., description="User ID")
    app_id: str = Field(..., description="Application ID")
    auth_token: str = Field(..., description="Initial authentication token")

    @classmethod
    def from_env(cls) -> "CascadeContext":
        """
        Create context from environment variables.

        Reads:
        - CASCADE_USER_ID
        - CASCADE_APP_ID
        - CASCADE_AUTH_TOKEN

        Raises:
            ValueError: If any required environment variable is missing
        """
        user_id = os.getenv("CASCADE_USER_ID")
        app_id = os.getenv("CASCADE_APP_ID")
        auth_token = os.getenv("CASCADE_AUTH_TOKEN")

        if not user_id:
            raise ValueError(
                "CASCADE_USER_ID environment variable is required but not set"
            )
        if not app_id:
            raise ValueError(
                "CASCADE_APP_ID environment variable is required but not set"
            )
        if not auth_token:
            raise ValueError(
                "CASCADE_AUTH_TOKEN environment variable is required but not set"
            )

        return cls(user_id=user_id, app_id=app_id, auth_token=auth_token)

    @field_validator("user_id", "app_id", "auth_token")
    @classmethod
    def validate_not_empty(cls, v: str) -> str:
        """Validate that string fields are not empty."""
        if not v or not v.strip():
            raise ValueError("Field cannot be empty")
        return v.strip()

    def get_firestore_config(self) -> Dict[str, str]:
        """
        Get Firestore configuration dictionary.

        Returns a dictionary suitable for initializing Firestore clients.
        The configuration includes the auth token and can be extended with
        additional Firestore-specific settings.

        Returns:
            Dictionary with Firestore configuration:
            - auth_token: Authentication token
            - project_id: Can be derived from app_id if needed
        """
        return {
            "auth_token": self.auth_token,
            "project_id": self.app_id,  # Can be overridden if needed
        }

    def get_firestore_path_prefix(self) -> str:
        """
        Get the Firestore path prefix for user-scoped collections.

        Returns the path prefix that should be used for all Firestore operations
        to ensure proper user/app scoping as per the architecture.

        Returns:
            Path prefix in format: /artifacts/{app_id}/users/{user_id}
        """
        return f"artifacts/{self.app_id}/users/{self.user_id}"

    def get_skill_map_path(self, skill_id: str) -> str:
        """Get Firestore path for a skill map."""
        return f"{self.get_firestore_path_prefix()}/skill_maps/{skill_id}"

    def get_worker_checkpoint_path(self, run_id: str) -> str:
        """Get Firestore path for a worker checkpoint."""
        return f"{self.get_firestore_path_prefix()}/worker_checkpoints/{run_id}"

    def get_explorer_checkpoint_path(self, run_id: str) -> str:
        """Get Firestore path for an explorer checkpoint."""
        return f"{self.get_firestore_path_prefix()}/explorer_checkpoints/{run_id}"

    def get_orchestrator_checkpoint_path(self, run_id: str) -> str:
        """Get Firestore path for an orchestrator checkpoint."""
        return (
            f"{self.get_firestore_path_prefix()}/orchestrator_checkpoints/{run_id}"
        )


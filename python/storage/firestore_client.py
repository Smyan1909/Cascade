"""Firestore wrapper with user/app scoping."""

from __future__ import annotations

from typing import Any, Dict, List, Optional

from cascade_client.auth.context import CascadeContext


class FirestoreClient:
    """Thin wrapper over Firestore to enforce scoped paths."""

    def __init__(self, context: CascadeContext, client: Optional[Any] = None):
        self._context = context
        self._client = client

    def _ensure_client(self):
        if self._client is not None:
            return self._client
        
        # Check for emulator first
        import os
        emulator_host = os.getenv("FIRESTORE_EMULATOR_HOST")
        if emulator_host:
            print(f"[Firestore] Connecting to Emulator at: {emulator_host}")
            try:
                from google.cloud import firestore
                from google.auth import credentials as ga_credentials
                
                # Direct instantiation bypasses firebase_admin strict checks
                self._client = firestore.Client(
                    project="cascade-prototype",
                    credentials=ga_credentials.AnonymousCredentials()
                )
                return self._client
            except ImportError as exc:
                print(f"[Firestore] Emulator init failed: {exc}")
                # Fall through to standard init

        try:
            import firebase_admin
            from firebase_admin import credentials, firestore
        except ImportError as exc:
            raise ImportError(
                "firebase-admin is required for Firestore operations"
            ) from exc

        if not firebase_admin._apps:
            cred = credentials.ApplicationDefault()
            firebase_admin.initialize_app(cred)
        self._client = firestore.client()
        return self._client

    # Path helpers
    def skill_map_path(self, skill_id: str) -> str:
        return self._context.get_skill_map_path(skill_id)

    def explorer_checkpoint_path(self, run_id: str) -> str:
        return self._context.get_explorer_checkpoint_path(run_id)

    def code_artifact_path(self, artifact_id: str) -> str:
        return (
            f"{self._context.get_firestore_path_prefix()}/code_artifacts/{artifact_id}"
        )

    def skill_maps_collection(self) -> str:
        """Return the collection path for skill maps."""
        return f"{self._context.get_firestore_path_prefix()}/skill_maps"

    def documentation_collection(self) -> str:
        """Return the collection path for documentation."""
        return f"{self._context.get_firestore_path_prefix()}/documentation"

    def documentation_path(self, doc_id: str) -> str:
        """Return the document path for a specific documentation entry."""
        return f"{self.documentation_collection()}/{doc_id}"

    def approval_policies_collection(self) -> str:
        """Return the collection path for approval policies."""
        return f"{self._context.get_firestore_path_prefix()}/approval_policies"

    def approval_policy_path(self, policy_id: str) -> str:
        return f"{self.approval_policies_collection()}/{policy_id}"

    # CRUD helpers
    def upsert_document(self, path: str, data: Dict[str, Any]) -> None:
        path = path.lstrip("/")  # Firestore SDK expects no leading slash
        client = self._ensure_client()
        doc_ref = client.document(path)
        doc_ref.set(data)

    def get_document(self, path: str) -> Optional[Dict[str, Any]]:
        path = path.lstrip("/")  # Firestore SDK expects no leading slash
        client = self._ensure_client()
        doc = client.document(path).get()
        if not doc.exists:
            return None
        return doc.to_dict()

    def upsert_skill_map(self, skill_map: Any) -> None:
        self.upsert_document(self.skill_map_path(skill_map.metadata.skill_id), skill_map.to_firestore())

    def get_skill_map(self, skill_id: str) -> Optional[Dict[str, Any]]:
        """Get a skill map by ID."""
        return self.get_document(self.skill_map_path(skill_id))

    def save_checkpoint(self, path: str, state: Dict[str, Any]) -> None:
        self.upsert_document(path, state)

    def load_checkpoint(self, path: str) -> Optional[Dict[str, Any]]:
        return self.get_document(path)

    def save_code_artifact(self, artifact_id: str, payload: Dict[str, Any]) -> None:
        self.upsert_document(self.code_artifact_path(artifact_id), payload)

    def get_code_artifact(self, artifact_id: str) -> Optional[Dict[str, Any]]:
        return self.get_document(self.code_artifact_path(artifact_id))

    # Approval policy CRUD
    def get_approval_policy(self, policy_id: str) -> Optional[Dict[str, Any]]:
        return self.get_document(self.approval_policy_path(policy_id))

    def upsert_approval_policy(self, policy_id: str, data: Dict[str, Any]) -> None:
        self.upsert_document(self.approval_policy_path(policy_id), data)

    def list_approval_policies(self) -> Dict[str, Dict[str, Any]]:
        client = self._ensure_client()
        collection = client.collection(self.approval_policies_collection().lstrip("/"))
        results: Dict[str, Dict[str, Any]] = {}
        for doc in collection.stream():
            results[doc.id] = doc.to_dict()
        return results

    def list_skill_maps(self) -> Dict[str, Dict[str, Any]]:
        """List all skill maps for the scoped user/app."""
        client = self._ensure_client()
        collection = client.collection(self.skill_maps_collection().lstrip("/"))
        results = {}
        for doc in collection.stream():
            results[doc.id] = doc.to_dict()
        return results

    # Documentation CRUD
    def upsert_documentation(self, documentation: Any) -> None:
        """Save or update a documentation entry."""
        self.upsert_document(
            self.documentation_path(documentation.metadata.doc_id),
            documentation.to_firestore()
        )

    def get_documentation(self, doc_id: str) -> Optional[Dict[str, Any]]:
        """Get a documentation entry by ID."""
        return self.get_document(self.documentation_path(doc_id))

    def list_documentation(self) -> Dict[str, Dict[str, Any]]:
        """List all documentation for the scoped user/app."""
        client = self._ensure_client()
        collection = client.collection(self.documentation_collection().lstrip("/"))
        results = {}
        for doc in collection.stream():
            results[doc.id] = doc.to_dict()
        return results

    def search_documentation_by_tags(self, tags: List[str]) -> Dict[str, Dict[str, Any]]:
        """Search documentation by tags (returns docs matching ANY of the tags)."""
        client = self._ensure_client()
        collection = client.collection(self.documentation_collection().lstrip("/"))
        results = {}
        
        # Firestore doesn't support OR queries well, so we fetch all and filter
        for doc in collection.stream():
            data = doc.to_dict()
            doc_tags = data.get("metadata", {}).get("tags", [])
            if any(tag in doc_tags for tag in tags):
                results[doc.id] = data
        
        return results



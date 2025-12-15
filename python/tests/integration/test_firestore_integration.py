"""
Integration tests for Firestore emulator connection.

These tests require the Firestore emulator to be running on the port
specified by FIRESTORE_EMULATOR_HOST (default: localhost:8080).

Run with: pytest tests/integration/test_firestore_integration.py -v
"""

import os
import uuid
import pytest
from datetime import datetime

# Skip all tests if emulator is not configured
pytestmark = pytest.mark.skipif(
    not os.getenv("FIRESTORE_EMULATOR_HOST"),
    reason="FIRESTORE_EMULATOR_HOST not set - emulator required"
)


@pytest.fixture
def firestore_context():
    """Create a test context for Firestore operations."""
    from cascade_client.auth.context import CascadeContext
    return CascadeContext(
        user_id="test-user",
        app_id="cascade-prototype",
        auth_token="test-token"
    )


@pytest.fixture
def firestore_client(firestore_context):
    """Create a FirestoreClient connected to the emulator."""
    from storage.firestore_client import FirestoreClient
    return FirestoreClient(firestore_context)


@pytest.fixture
def sample_skill_map():
    """Create a sample SkillMap for testing."""
    from agents.explorer.skill_map import SkillMap, SkillMetadata, SkillStep
    from cascade_client.models import Selector, PlatformSource, ControlType
    
    return SkillMap(
        metadata=SkillMetadata(
            skill_id=f"test-skill-{uuid.uuid4().hex[:8]}",
            app_id="cascade-prototype",
            user_id="test-user",
            capability="Test Calculator Addition",
            description="Test skill map for integration testing",
            version=1,
        ),
        steps=[
            SkillStep(
                action="Click",
                step_description="Click the number 5 button",
                selector=Selector(
                    platform_source=PlatformSource.WINDOWS,  # Use enum
                    name="Five",
                    control_type=ControlType.BUTTON,  # Use enum
                    path=["Calculator", "Number pad", "Five"],
                ),
            ),
        ],
    )


class TestFirestoreEmulatorConnection:
    """Test that we can connect to the Firestore emulator."""
    
    def test_emulator_host_is_set(self):
        """Verify FIRESTORE_EMULATOR_HOST is set correctly."""
        host = os.getenv("FIRESTORE_EMULATOR_HOST")
        assert host is not None, "FIRESTORE_EMULATOR_HOST must be set"
        print(f"\n[TEST] Emulator host: {host}")
    
    def test_client_connects_to_emulator(self, firestore_client):
        """Verify the client can connect to the emulator."""
        # This will trigger client initialization
        client = firestore_client._ensure_client()
        assert client is not None, "Firestore client should be initialized"
        print(f"\n[TEST] Firestore client initialized successfully")


class TestSkillMapPersistence:
    """Test that skill maps can be saved and retrieved from the emulator."""
    
    def test_save_skill_map(self, firestore_client, sample_skill_map):
        """Test saving a skill map to Firestore emulator."""
        skill_id = sample_skill_map.metadata.skill_id
        print(f"\n[TEST] Saving skill map: {skill_id}")
        
        # Save the skill map
        firestore_client.upsert_skill_map(sample_skill_map)
        print(f"[TEST] Skill map saved successfully")
        
        # Verify it was saved
        path = firestore_client.skill_map_path(skill_id)
        print(f"[TEST] Saved at path: {path}")
    
    def test_get_skill_map(self, firestore_client, sample_skill_map):
        """Test retrieving a skill map from Firestore emulator."""
        skill_id = sample_skill_map.metadata.skill_id
        
        # Save first
        firestore_client.upsert_skill_map(sample_skill_map)
        
        # Retrieve
        data = firestore_client.get_skill_map(skill_id)
        assert data is not None, f"Skill map {skill_id} should exist"
        assert data["metadata"]["skill_id"] == skill_id
        assert data["metadata"]["capability"] == "Test Calculator Addition"
        print(f"\n[TEST] Retrieved skill map: {skill_id}")
        print(f"[TEST] Capability: {data['metadata']['capability']}")
    
    def test_list_skill_maps(self, firestore_client, sample_skill_map):
        """Test listing all skill maps."""
        # Save a skill map first
        firestore_client.upsert_skill_map(sample_skill_map)
        
        # List all skill maps
        skill_maps = firestore_client.list_skill_maps()
        
        assert sample_skill_map.metadata.skill_id in skill_maps, \
            "Saved skill map should appear in list"
        
        print(f"\n[TEST] Found {len(skill_maps)} skill maps:")
        for skill_id, data in skill_maps.items():
            capability = data.get("metadata", {}).get("capability", "Unknown")
            print(f"  - {skill_id}: {capability}")


class TestDocumentCRUD:
    """Test basic document CRUD operations."""
    
    def test_upsert_and_get_document(self, firestore_client):
        """Test basic document upsert and retrieval."""
        # Firestore paths should NOT start with /
        test_path = "artifacts/cascade-prototype/users/test-user/test_docs/test1"
        test_data = {"name": "test", "value": 42, "timestamp": datetime.utcnow().isoformat()}
        
        # Upsert
        firestore_client.upsert_document(test_path, test_data)
        print(f"\n[TEST] Saved document at: {test_path}")
        
        # Get
        retrieved = firestore_client.get_document(test_path)
        assert retrieved is not None
        assert retrieved["name"] == "test"
        assert retrieved["value"] == 42
        print(f"[TEST] Retrieved document: {retrieved}")
    
    def test_get_nonexistent_document(self, firestore_client):
        """Test getting a document that doesn't exist."""
        result = firestore_client.get_document("nonexistent/path/to/doc")
        assert result is None, "Non-existent document should return None"
        print("\n[TEST] Correctly returned None for non-existent document")


@pytest.fixture
def sample_documentation():
    """Create a sample DocumentationMap for testing."""
    from agents.explorer.documentation_map import (
        DocumentationMap, DocumentationMetadata, DocumentationSection
    )
    
    return DocumentationMap(
        metadata=DocumentationMetadata(
            doc_id=f"test-doc-{uuid.uuid4().hex[:8]}",
            app_id="cascade-prototype",
            user_id="test-user",
            title="Calculator User Guide",
            doc_type="overview",
            description="Guide for using the calculator application",
            tags=["calculator", "guide", "basics"],
            related_skills=["calc-add", "calc-subtract"],
        ),
        sections=[
            DocumentationSection(
                heading="Getting Started",
                content="Open the calculator from the Start menu.",
                element_references=["Start Menu", "Calculator Icon"],
            ),
            DocumentationSection(
                heading="Basic Operations",
                content="Use the number pad to enter numbers and operators.",
                element_references=["Number Pad", "Equals Button"],
                code_examples=["5 + 3 = 8"],
            ),
        ],
    )


class TestDocumentationPersistence:
    """Test documentation CRUD operations."""
    
    def test_save_documentation(self, firestore_client, sample_documentation):
        """Test saving documentation to Firestore emulator."""
        doc_id = sample_documentation.metadata.doc_id
        print(f"\n[TEST] Saving documentation: {doc_id}")
        
        # Save
        firestore_client.upsert_documentation(sample_documentation)
        print(f"[TEST] Documentation saved successfully")
        
        # Verify path
        path = firestore_client.documentation_path(doc_id)
        print(f"[TEST] Saved at path: {path}")
    
    def test_get_documentation(self, firestore_client, sample_documentation):
        """Test retrieving documentation by ID."""
        doc_id = sample_documentation.metadata.doc_id
        
        # Save first
        firestore_client.upsert_documentation(sample_documentation)
        
        # Retrieve
        data = firestore_client.get_documentation(doc_id)
        assert data is not None, f"Documentation {doc_id} should exist"
        assert data["metadata"]["doc_id"] == doc_id
        assert data["metadata"]["title"] == "Calculator User Guide"
        assert "calculator" in data["metadata"]["tags"]
        print(f"\n[TEST] Retrieved documentation: {doc_id}")
        print(f"[TEST] Title: {data['metadata']['title']}")
    
    def test_list_documentation(self, firestore_client, sample_documentation):
        """Test listing all documentation."""
        # Save first
        firestore_client.upsert_documentation(sample_documentation)
        
        # List
        docs = firestore_client.list_documentation()
        
        assert sample_documentation.metadata.doc_id in docs, \
            "Saved documentation should appear in list"
        
        print(f"\n[TEST] Found {len(docs)} documentation entries:")
        for doc_id, data in docs.items():
            title = data.get("metadata", {}).get("title", "Unknown")
            print(f"  - {doc_id}: {title}")
    
    def test_search_documentation_by_tags(self, firestore_client, sample_documentation):
        """Test searching documentation by tags."""
        # Save first
        firestore_client.upsert_documentation(sample_documentation)
        
        # Search with matching tag
        results = firestore_client.search_documentation_by_tags(["calculator"])
        assert len(results) > 0, "Should find documentation with 'calculator' tag"
        print(f"\n[TEST] Found {len(results)} docs with 'calculator' tag")
        
        # Search with non-matching tag
        no_results = firestore_client.search_documentation_by_tags(["nonexistent-tag-xyz"])
        assert sample_documentation.metadata.doc_id not in no_results
        print(f"[TEST] Correctly found 0 docs with non-existent tag")


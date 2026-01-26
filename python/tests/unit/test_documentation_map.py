"""Unit tests for DocumentationMap model."""

import datetime
from agents.explorer.documentation_map import (
    DocumentationMap, 
    DocumentationMetadata, 
    DocumentationSection
)


def test_documentation_metadata_defaults():
    """Test that DocumentationMetadata sets proper defaults."""
    meta = DocumentationMetadata(
        doc_id="doc-1",
        app_id="test-app",
        user_id="test-user",
        title="Test Doc"
    )
    assert meta.doc_type == "overview"
    assert meta.description == ""
    assert meta.tags == []
    assert meta.related_skills == []
    assert meta.version == 1


def test_documentation_section_creation():
    """Test DocumentationSection creation."""
    section = DocumentationSection(
        heading="Getting Started",
        content="This is the content of the section."
    )
    assert section.heading == "Getting Started"
    assert section.content == "This is the content of the section."
    assert section.element_references == []
    assert section.code_examples == []


def test_documentation_map_to_firestore():
    """Test serialization to Firestore format."""
    meta = DocumentationMetadata(
        doc_id="doc-1",
        app_id="test-app",
        user_id="test-user",
        title="Calculator Guide",
        doc_type="workflow",
        description="How to use the calculator",
        tags=["calculator", "math"]
    )
    sections = [
        DocumentationSection(
            heading="Basic Operations",
            content="Click numbers and operators to perform calculations.",
            element_references=["Number Pad", "Equals Button"]
        )
    ]
    doc = DocumentationMap(metadata=meta, sections=sections)
    
    data = doc.to_firestore()
    
    assert data["metadata"]["doc_id"] == "doc-1"
    assert data["metadata"]["title"] == "Calculator Guide"
    assert data["metadata"]["doc_type"] == "workflow"
    assert "calculator" in data["metadata"]["tags"]
    assert len(data["sections"]) == 1
    assert data["sections"][0]["heading"] == "Basic Operations"


def test_documentation_map_from_firestore():
    """Test deserialization from Firestore format."""
    data = {
        "metadata": {
            "doc_id": "doc-2",
            "app_id": "test-app",
            "user_id": "test-user",
            "title": "Troubleshooting Guide",
            "doc_type": "troubleshooting",
            "description": "Common issues and solutions",
            "tags": ["help", "errors"],
            "related_skills": [],
            "version": 1,
            "created_at": datetime.datetime.now(datetime.timezone.utc).isoformat(),
            "updated_at": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        },
        "sections": [
            {
                "heading": "App Not Responding",
                "content": "Try restarting the application.",
                "element_references": [],
                "code_examples": []
            }
        ]
    }
    
    doc = DocumentationMap.from_firestore(data)
    
    assert doc.metadata.doc_id == "doc-2"
    assert doc.metadata.title == "Troubleshooting Guide"
    assert doc.metadata.doc_type == "troubleshooting"
    assert len(doc.sections) == 1


def test_get_summary():
    """Test getting a compact summary."""
    meta = DocumentationMetadata(
        doc_id="doc-1",
        app_id="test-app",
        user_id="test-user",
        title="App Overview",
        doc_type="overview",
        description="An overview of the application",
        tags=["intro", "basics"]
    )
    sections = [
        DocumentationSection(heading="Introduction", content="Welcome!"),
        DocumentationSection(heading="Features", content="List of features"),
    ]
    doc = DocumentationMap(metadata=meta, sections=sections)
    
    summary = doc.get_summary()
    
    assert "App Overview" in summary
    assert "overview" in summary
    assert "intro" in summary or "basics" in summary
    assert "Introduction" in summary
    assert "Features" in summary


def test_get_full_content():
    """Test getting full formatted content."""
    meta = DocumentationMetadata(
        doc_id="doc-1",
        app_id="test-app",
        user_id="test-user",
        title="Full Test",
        doc_type="element_guide",
        description="Testing full content output"
    )
    sections = [
        DocumentationSection(
            heading="Section One",
            content="Content for section one.",
            element_references=["Button A"],
            code_examples=["example code"]
        )
    ]
    doc = DocumentationMap(metadata=meta, sections=sections)
    
    content = doc.get_full_content()
    
    assert "# Full Test" in content
    assert "element_guide" in content
    assert "## Section One" in content
    assert "Content for section one." in content
    assert "Button A" in content
    assert "example code" in content


def test_add_section():
    """Test adding sections dynamically."""
    meta = DocumentationMetadata(
        doc_id="doc-1",
        app_id="test-app",
        user_id="test-user",
        title="Dynamic Test"
    )
    doc = DocumentationMap(metadata=meta, sections=[])
    
    assert len(doc.sections) == 0
    
    doc.add_section("New Section", "New content here")
    
    assert len(doc.sections) == 1
    assert doc.sections[0].heading == "New Section"
    assert doc.sections[0].content == "New content here"

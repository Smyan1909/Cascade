"""Helpers to load and describe documentation for Worker planning."""

from __future__ import annotations

from typing import Dict, List, Optional

from cascade_client.auth.context import CascadeContext
from storage.firestore_client import FirestoreClient

from agents.explorer.documentation_map import DocumentationMap


def load_all_documentation(
    context: CascadeContext, 
    fs: FirestoreClient | None = None
) -> List[DocumentationMap]:
    """Load all documentation for the given app/user context."""
    client = fs or FirestoreClient(context)
    raw = client.list_documentation()
    docs: List[DocumentationMap] = []
    for _, data in raw.items():
        try:
            docs.append(DocumentationMap.model_validate(data))
        except Exception:
            # Skip invalid documentation entries
            continue
    return docs


def get_documentation_summaries(docs: List[DocumentationMap]) -> List[Dict[str, str]]:
    """Return compact summaries for LLM planning."""
    summaries: List[Dict[str, str]] = []
    for doc in docs:
        summaries.append({
            "doc_id": doc.metadata.doc_id,
            "title": doc.metadata.title,
            "doc_type": doc.metadata.doc_type,
            "description": doc.metadata.description or "",
            "tags": ", ".join(doc.metadata.tags),
            "sections": ", ".join(s.heading for s in doc.sections),
        })
    return summaries


def format_documentation_for_prompt(docs: List[DocumentationMap]) -> str:
    """Format documentation for inclusion in agent system prompt."""
    if not docs:
        return "No documentation available for this application."
    
    lines = ["## Available Documentation\n"]
    
    for doc in docs:
        lines.append(f"### {doc.metadata.title}")
        lines.append(f"*Type: {doc.metadata.doc_type}*")
        if doc.metadata.description:
            lines.append(f"{doc.metadata.description}")
        if doc.metadata.tags:
            lines.append(f"Tags: {', '.join(doc.metadata.tags)}")
        lines.append("")
    
    return "\n".join(lines)


def get_documentation_by_id(
    doc_id: str,
    context: CascadeContext,
    fs: FirestoreClient | None = None
) -> Optional[DocumentationMap]:
    """Get a specific documentation entry by ID."""
    client = fs or FirestoreClient(context)
    data = client.get_documentation(doc_id)
    if data:
        try:
            return DocumentationMap.model_validate(data)
        except Exception:
            return None
    return None


def search_documentation(
    tags: List[str],
    context: CascadeContext,
    fs: FirestoreClient | None = None
) -> List[DocumentationMap]:
    """Search documentation by tags."""
    client = fs or FirestoreClient(context)
    raw = client.search_documentation_by_tags(tags)
    docs: List[DocumentationMap] = []
    for _, data in raw.items():
        try:
            docs.append(DocumentationMap.model_validate(data))
        except Exception:
            continue
    return docs

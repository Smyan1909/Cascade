"""Documentation Map schema for structured application documentation."""

from datetime import datetime, timezone
from typing import Any, Dict, List, Literal, Optional

from pydantic import BaseModel, Field

DocumentationType = Literal["overview", "workflow", "element_guide", "troubleshooting"]


class DocumentationMetadata(BaseModel):
    """Metadata for a Documentation entry."""

    doc_id: str = Field(..., description="Unique documentation identifier")
    app_id: str = Field(..., description="Application ID")
    user_id: str = Field(..., description="User ID")
    title: str = Field(..., description="Document title")
    doc_type: DocumentationType = Field(
        default="overview",
        description="Type of documentation: overview, workflow, element_guide, or troubleshooting"
    )
    description: str = Field(
        default="",
        description="Brief summary of what this documentation covers"
    )
    tags: List[str] = Field(
        default_factory=list,
        description="Searchable tags for this documentation"
    )
    related_skills: List[str] = Field(
        default_factory=list,
        description="List of related skill IDs"
    )
    version: int = Field(default=1, ge=1, description="Documentation version")
    created_at: datetime = Field(
        default_factory=lambda: datetime.now(timezone.utc),
        description="Creation timestamp"
    )
    updated_at: datetime = Field(
        default_factory=lambda: datetime.now(timezone.utc),
        description="Last update timestamp"
    )


class DocumentationSection(BaseModel):
    """A section within a documentation entry."""

    heading: str = Field(..., description="Section heading/title")
    content: str = Field(..., description="Section content in Markdown format")
    element_references: List[str] = Field(
        default_factory=list,
        description="UI element names or identifiers referenced in this section"
    )
    code_examples: List[str] = Field(
        default_factory=list,
        description="Code snippets or automation examples"
    )


class DocumentationMap(BaseModel):
    """Documentation entry stored in Firestore."""

    metadata: DocumentationMetadata
    sections: List[DocumentationSection] = Field(default_factory=list)

    def to_firestore(self) -> Dict[str, Any]:
        """Serialize to a Firestore-friendly dict."""
        data = self.model_dump(mode="json")
        return data

    @classmethod
    def from_firestore(cls, data: Dict[str, Any]) -> "DocumentationMap":
        """Deserialize from Firestore dict."""
        return cls.model_validate(data)

    def update_timestamp(self) -> None:
        """Update the updated_at timestamp."""
        self.metadata.updated_at = datetime.now(timezone.utc)

    def add_section(self, heading: str, content: str, **kwargs) -> None:
        """Add a new section to the documentation."""
        section = DocumentationSection(heading=heading, content=content, **kwargs)
        self.sections.append(section)

    def get_summary(self) -> str:
        """Get a compact summary for LLM context."""
        lines = [
            f"**{self.metadata.title}** ({self.metadata.doc_type})",
            f"  {self.metadata.description}",
        ]
        if self.metadata.tags:
            lines.append(f"  Tags: {', '.join(self.metadata.tags)}")
        if self.sections:
            lines.append(f"  Sections: {', '.join(s.heading for s in self.sections)}")
        return "\n".join(lines)

    def get_full_content(self) -> str:
        """Get full documentation content formatted for reading."""
        lines = [
            f"# {self.metadata.title}",
            "",
            f"*Type: {self.metadata.doc_type}*",
            "",
            self.metadata.description,
            "",
        ]
        
        for section in self.sections:
            lines.append(f"## {section.heading}")
            lines.append("")
            lines.append(section.content)
            lines.append("")
            
            if section.element_references:
                lines.append(f"*Referenced elements: {', '.join(section.element_references)}*")
                lines.append("")
            
            if section.code_examples:
                for example in section.code_examples:
                    lines.append("```")
                    lines.append(example)
                    lines.append("```")
                    lines.append("")
        
        return "\n".join(lines)

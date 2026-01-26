"""Skill Map schema for the Explorer agent."""

from datetime import datetime, timezone
from typing import Any, Dict, List, Literal, Optional

from pydantic import BaseModel, Field, validator

from cascade_client.models import ActionType, Selector

PreferredMethod = Literal["api", "sandbox", "ui"]
SkillType = Literal["primitive", "composite"]


class SandboxFunctionSpec(BaseModel):
    """A Python function used within a sandbox skill."""

    module: str = Field(..., description="Python module path, e.g. openpyxl")
    function: str = Field(..., description="Function name, e.g. load_workbook")
    description: str = Field(default="", description="What this function is used for")


class SandboxFileSpec(BaseModel):
    """File input/output contract entry for sandbox execution."""

    name: str = Field(..., description="Logical name used in inputs (e.g., 'workbook')")
    file_glob: str = Field(..., description="Expected file type(s), e.g. '*.xlsx'")
    required: bool = Field(default=True)


class SandboxFileIoSpec(BaseModel):
    """Describes sandbox file IO contract (copy-in/run/copy-out)."""

    inputs: List[SandboxFileSpec] = Field(default_factory=list)
    outputs: List[SandboxFileSpec] = Field(default_factory=list)
    notes: str = Field(default="", description="Any constraints about IO behavior")


class SandboxSpec(BaseModel):
    """Sandbox execution configuration stored on a Skill Map."""

    provider: Literal["e2b"] = Field(default="e2b")
    python_packages: List[str] = Field(
        default_factory=list,
        description="Pip requirement strings needed to run this skill (e.g. openpyxl, python-pptx).",
    )
    functions: Dict[str, SandboxFunctionSpec] = Field(
        default_factory=dict,
        description="Mapping of task verb -> function spec (e.g. 'open_workbook' -> openpyxl.load_workbook).",
    )
    file_io: SandboxFileIoSpec = Field(default_factory=SandboxFileIoSpec)
    entrypoint: str = Field(
        default="",
        description="Canonical operation name Worker should invoke (key in functions), if applicable.",
    )


class SkillMetadata(BaseModel):
    """Metadata for a Skill Map."""

    skill_id: str = Field(..., description="Unique skill identifier")
    app_id: str = Field(..., description="Application ID")
    user_id: str = Field(..., description="User ID")
    
    # Skill type: primitive (single action) or composite (multi-step sequence)
    skill_type: SkillType = Field(
        default="primitive",
        description="Type of skill: 'primitive' for single actions, 'composite' for multi-step sequences"
    )
    
    # For composite skills: list of skill IDs that must be executed first
    composed_of: List[str] = Field(
        default_factory=list,
        description="For composite skills: list of prerequisite skill IDs to execute in order"
    )
    
    description: str = Field(
        default="",
        description="Human-readable description of what the skill does",
    )
    capability: str = Field(
        default="",
        description="Capability category (e.g., login, save_file, search)",
    )
    inputs: Dict[str, Any] = Field(
        default_factory=dict,
        description="Input parameters required/optional for this skill",
    )
    outputs: Dict[str, Any] = Field(
        default_factory=dict,
        description="Outputs/results this skill produces",
    )
    preconditions: List[str] = Field(
        default_factory=list,
        description="Conditions that must hold before executing this skill",
    )
    postconditions: List[str] = Field(
        default_factory=list,
        description="Conditions expected after successful execution",
    )
    version: int = Field(default=1, ge=1, description="Skill version")
    provenance: str = Field(
        default="explorer", description="Origin of the skill map (e.g., explorer run)"
    )
    confidence: float = Field(
        default=0.0, ge=0.0, le=1.0, description="Overall confidence score"
    )
    preferred_method: PreferredMethod = Field(
        default="ui", description="Preferred automation method"
    )
    created_at: datetime = Field(
        default_factory=lambda: datetime.now(timezone.utc),
        description="Creation timestamp",
    )
    updated_at: datetime = Field(
        default_factory=lambda: datetime.now(timezone.utc),
        description="Last update timestamp",
    )
    
    # Initial state context: describes the application state when this skill was discovered
    initial_state_description: str = Field(
        default="",
        description="Human-readable description of the initial application state when this skill was discovered (e.g., 'Calculator in Standard mode')",
    )
    initial_state_tree: Optional[Dict[str, Any]] = Field(
        default=None,
        description="Optional semantic tree snapshot of the initial state. Include only when the human-readable description is ambiguous or insufficient.",
    )
    requires_initial_state: bool = Field(
        default=False,
        description="If True, the application must be in the initial state before executing this skill",
    )

    # Sandbox-based programmatic automation (optional).
    # When present, Worker should run the operation in an E2B sandbox and copy files in/out.
    sandbox: Optional[SandboxSpec] = Field(default=None)

class ApiEndpoint(BaseModel):
    """API endpoint description for automation."""

    method: str = Field(..., description="HTTP method")
    url: str = Field(..., description="Full URL or path")
    headers: Dict[str, str] = Field(default_factory=dict)
    query: Dict[str, Any] = Field(default_factory=dict)
    body_schema: Optional[Dict[str, Any]] = Field(
        default=None, description="JSON schema or example payload"
    )
    auth_type: Optional[str] = Field(default=None, description="Auth strategy hint")
    evidence: Optional[str] = Field(
        default=None, description="Text citation for why this endpoint was chosen"
    )
    confidence: float = Field(
        default=0.0, ge=0.0, le=1.0, description="Confidence in endpoint mapping"
    )


class Guard(BaseModel):
    """Guard condition to check before/after a step."""

    description: str
    selector: Optional[Selector] = None
    expectation: Optional[str] = Field(default=None, description="Expected condition")


class Fallback(BaseModel):
    """Fallback selector or action."""

    selector: Optional[Selector] = None
    api_endpoint: Optional[ApiEndpoint] = None
    note: Optional[str] = None


class WaitCondition(BaseModel):
    """Wait condition with timeout."""

    description: str
    timeout_seconds: float = Field(default=5.0, ge=0.0)


class SkillStep(BaseModel):
    """Single automation step."""

    action: str = Field(..., description="Action verb, e.g., Click, Type, CallAPI")
    step_description: str = Field(
        default="",
        description="What this step accomplishes (LLM-readable)",
    )
    selector: Optional[Selector] = Field(
        default=None, description="UI selector when using UI automation"
    )
    api_endpoint: Optional[ApiEndpoint] = Field(
        default=None, description="API endpoint when using API automation"
    )
    inputs: Optional[Dict[str, Any]] = Field(
        default=None, description="Payload for the action"
    )
    guards: List[Guard] = Field(default_factory=list)
    fallbacks: List[Fallback] = Field(default_factory=list)
    waits: List[WaitCondition] = Field(default_factory=list)
    confidence: float = Field(
        default=0.0, ge=0.0, le=1.0, description="Confidence for this step"
    )

    @validator("action")
    def validate_action(cls, value: str) -> str:
        if not value:
            raise ValueError("action cannot be empty")
        return value

    def prefer_api(self) -> bool:
        """Return True if this step has an API endpoint with confidence."""
        return self.api_endpoint is not None and self.api_endpoint.confidence > 0

    def prefer_ui(self) -> bool:
        """Return True if this step has a selector (UI path)."""
        return self.selector is not None


class SkillEvidence(BaseModel):
    """Evidence captured during mapping (manual snippets, OCR, API responses)."""

    manual_snippets: List[str] = Field(default_factory=list)
    ocr_snippets: List[str] = Field(default_factory=list)
    api_samples: List[str] = Field(default_factory=list)


class SkillMap(BaseModel):
    """Skill Map representation stored in Firestore."""

    metadata: SkillMetadata
    steps: List[SkillStep]
    selectors: List[Selector] = Field(default_factory=list)
    assets: SkillEvidence = Field(default_factory=SkillEvidence)

    def to_firestore(self) -> Dict[str, Any]:
        """Serialize to a Firestore-friendly dict."""
        data = self.model_dump(mode="json")
        return data

    def update_confidence(self, score: float) -> None:
        """Update metadata confidence."""
        self.metadata.confidence = max(0.0, min(1.0, score))
        self.metadata.updated_at = datetime.now(timezone.utc)

    def choose_method_for_step(self, step: SkillStep) -> PreferredMethod:
        """Decide method per step based on availability and metadata preference."""
        # If this is a sandbox skill, the whole skill is expected to run in sandbox,
        # regardless of individual step fields.
        if self.metadata.preferred_method == "sandbox" and self.metadata.sandbox is not None:
            return "sandbox"
        if self.metadata.preferred_method == "api" and step.prefer_api():
            return "api"
        if self.metadata.preferred_method == "ui" and step.prefer_ui():
            return "ui"
        # Fallback heuristics
        if step.prefer_api():
            return "api"
        if step.prefer_ui():
            return "ui"
        return self.metadata.preferred_method


def default_action_for_method(method: PreferredMethod) -> str:
    """Return a canonical action verb for the given method."""
    if method == "api":
        return "CallAPI"
    if method == "sandbox":
        return "RunSandbox"
    return ActionType.CLICK.name


"""
Pydantic models mirroring proto messages for type safety and validation.

These models provide a Python-friendly interface to the gRPC protocol,
with automatic conversion to/from proto messages.
"""

from enum import IntEnum
from typing import Any, Dict, List, Optional

from pydantic import BaseModel, Field, field_validator


# Enums
class ActionType(IntEnum):
    """Action types for UI automation."""

    ACTION_TYPE_UNSPECIFIED = 0
    CLICK = 1
    TYPE_TEXT = 2
    HOVER = 3
    FOCUS = 4
    SCROLL = 5
    WAIT_VISIBLE = 6


class PlatformSource(IntEnum):
    """Platform sources for UI automation."""

    PLATFORM_SOURCE_UNSPECIFIED = 0
    WINDOWS = 1
    JAVA = 2
    WEB = 3


class ControlType(IntEnum):
    """Control types for UI elements."""

    CONTROL_TYPE_UNSPECIFIED = 0
    BUTTON = 1
    INPUT = 2
    COMBO = 3
    MENU = 4
    TREE = 5
    TABLE = 6
    CUSTOM = 7
    LISTITEM = 8  # ListItem, checkbox, radio button
    TAB = 9       # TabItem


class ImageFormat(IntEnum):
    """Image formats for screenshots."""

    IMAGE_FORMAT_UNSPECIFIED = 0
    PNG = 1
    JPEG = 2


# Message Models
class NormalizedRectangle(BaseModel):
    """Normalized coordinates (0-1) relative to the capture surface."""

    x: float = Field(..., ge=0.0, le=1.0, description="X coordinate (0-1)")
    y: float = Field(..., ge=0.0, le=1.0, description="Y coordinate (0-1)")
    width: float = Field(..., ge=0.0, le=1.0, description="Width (0-1)")
    height: float = Field(..., ge=0.0, le=1.0, description="Height (0-1)")

    @classmethod
    def from_proto(cls, proto_msg) -> "NormalizedRectangle":
        """Create from proto message."""
        return cls(
            x=proto_msg.x,
            y=proto_msg.y,
            width=proto_msg.width,
            height=proto_msg.height,
        )

    def to_proto(self):
        """Convert to proto message."""
        try:
            from cascade_client.proto import cascade_pb2

            return cascade_pb2.NormalizedRectangle(
                x=self.x,
                y=self.y,
                width=self.width,
                height=self.height,
            )
        except ImportError:
            raise ImportError(
                "Proto stubs not generated. Run generate_proto.ps1 or generate_proto.sh"
            )


class UIElement(BaseModel):
    """UI element in the semantic tree."""

    id: str = Field(..., description="Stable ID within a session")
    name: str = Field(default="", description="Element name")
    control_type: ControlType = Field(
        default=ControlType.CONTROL_TYPE_UNSPECIFIED, description="Control type"
    )
    bounding_box: Optional[NormalizedRectangle] = Field(
        default=None, description="Bounding box coordinates"
    )
    parent_id: str = Field(default="", description="Parent element ID")
    platform_source: PlatformSource = Field(
        default=PlatformSource.PLATFORM_SOURCE_UNSPECIFIED,
        description="Platform source",
    )
    aria_role: Optional[str] = Field(default=None, description="ARIA role (optional)")
    automation_id: Optional[str] = Field(
        default=None, description="Automation ID (optional)"
    )
    value_text: Optional[str] = Field(default=None, description="Value text (optional)")

    @classmethod
    def from_proto(cls, proto_msg) -> "UIElement":
        """Create from proto message."""
        return cls(
            id=proto_msg.id,
            name=proto_msg.name,
            control_type=ControlType(proto_msg.control_type),
            bounding_box=NormalizedRectangle.from_proto(proto_msg.bounding_box)
            if proto_msg.HasField("bounding_box")
            else None,
            parent_id=proto_msg.parent_id,
            platform_source=PlatformSource(proto_msg.platform_source),
            aria_role=proto_msg.aria_role if proto_msg.HasField("aria_role") else None,
            automation_id=proto_msg.automation_id
            if proto_msg.HasField("automation_id")
            else None,
            value_text=proto_msg.value_text if proto_msg.HasField("value_text") else None,
        )

    def to_proto(self):
        """Convert to proto message."""
        try:
            from cascade_client.proto import cascade_pb2

            msg = cascade_pb2.UIElement(
                id=self.id,
                name=self.name,
                control_type=self.control_type.value,
                parent_id=self.parent_id,
                platform_source=self.platform_source.value,
            )

            if self.bounding_box:
                msg.bounding_box.CopyFrom(self.bounding_box.to_proto())

            if self.aria_role is not None:
                msg.aria_role = self.aria_role

            if self.automation_id is not None:
                msg.automation_id = self.automation_id

            if self.value_text is not None:
                msg.value_text = self.value_text

            return msg
        except ImportError:
            raise ImportError(
                "Proto stubs not generated. Run generate_proto.ps1 or generate_proto.sh"
            )


class SemanticTree(BaseModel):
    """Semantic tree containing UI elements."""

    elements: List[UIElement] = Field(default_factory=list, description="UI elements")

    @classmethod
    def from_proto(cls, proto_msg) -> "SemanticTree":
        """Create from proto message."""
        return cls(
            elements=[UIElement.from_proto(elem) for elem in proto_msg.elements]
        )

    def to_proto(self):
        """Convert to proto message."""
        try:
            from cascade_client.proto import cascade_pb2

            return cascade_pb2.SemanticTree(
                elements=[elem.to_proto() for elem in self.elements]
            )
        except ImportError:
            raise ImportError(
                "Proto stubs not generated. Run generate_proto.ps1 or generate_proto.sh"
            )

    def to_graph(self) -> Dict[str, Dict]:
        """
        Convert semantic tree to a graph structure (dict-based, networkx-like).

        Returns a dictionary where keys are element IDs and values are dictionaries
        containing the element data and connections.

        Example:
            {
                "elem1": {
                    "element": UIElement(...),
                    "children": ["elem2", "elem3"],
                    "parent": None
                },
                ...
            }
        """
        graph: Dict[str, Dict] = {}

        # First pass: create nodes
        for elem in self.elements:
            graph[elem.id] = {
                "element": elem,
                "children": [],
                "parent": elem.parent_id if elem.parent_id else None,
            }

        # Second pass: build parent-child relationships
        for elem in self.elements:
            if elem.parent_id and elem.parent_id in graph:
                graph[elem.parent_id]["children"].append(elem.id)

        return graph

    def get_element_by_id(self, element_id: str) -> Optional[UIElement]:
        """Get element by ID."""
        for elem in self.elements:
            if elem.id == element_id:
                return elem
        return None

    def get_elements_by_control_type(
        self, control_type: ControlType
    ) -> List[UIElement]:
        """Get all elements with the specified control type."""
        return [elem for elem in self.elements if elem.control_type == control_type]


class Selector(BaseModel):
    """Selector for targeting UI elements."""

    platform_source: PlatformSource = Field(
        ..., description="Platform source"
    )
    path: List[str] = Field(default_factory=list, description="Path components")
    id: Optional[str] = Field(default=None, description="Element ID filter")
    name: Optional[str] = Field(default=None, description="Element name filter")
    control_type: Optional[ControlType] = Field(
        default=None, description="Control type filter"
    )
    index: Optional[int] = Field(default=None, description="Index filter")
    text_hint: Optional[str] = Field(default=None, description="Text hint filter")

    @staticmethod
    def _coerce_int_enum(value: Any, enum_cls: type[IntEnum], *, aliases: Dict[str, str] | None = None) -> IntEnum:
        """
        Accept IntEnum instances, ints, numeric strings, or enum-name strings (case-insensitive).

        This is intentionally lenient because LLMs/tool callers commonly emit enum names
        like "WINDOWS" / "TAB" rather than the underlying numeric values.
        """
        if isinstance(value, enum_cls):
            return value

        if isinstance(value, int):
            return enum_cls(value)

        if isinstance(value, str):
            s = value.strip()
            if s == "":
                raise ValueError(f"Empty value is not valid for {enum_cls.__name__}")

            # Numeric string (e.g. "1")
            if s.isdigit() or (s.startswith("-") and s[1:].isdigit()):
                return enum_cls(int(s))

            key = s.upper()
            if aliases and key in aliases:
                key = aliases[key]

            try:
                return enum_cls[key]  # type: ignore[index]
            except KeyError as e:
                allowed = ", ".join([m.name for m in enum_cls])  # type: ignore[arg-type]
                raise ValueError(
                    f"Invalid {enum_cls.__name__} '{value}'. Allowed: {allowed}"
                ) from e

        raise TypeError(f"Invalid type for {enum_cls.__name__}: {type(value).__name__}")

    @field_validator("platform_source", mode="before")
    @classmethod
    def _validate_platform_source(cls, v: Any) -> Any:
        return cls._coerce_int_enum(
            v,
            PlatformSource,
            aliases={
                "WIN": "WINDOWS",
                "WINDOWS_UIA": "WINDOWS",
            },
        )

    @field_validator("control_type", mode="before")
    @classmethod
    def _validate_control_type(cls, v: Any) -> Any:
        if v is None:
            return None
        return cls._coerce_int_enum(
            v,
            ControlType,
            aliases={
                "LIST_ITEM": "LISTITEM",
                "TABITEM": "TAB",
                "TAB_ITEM": "TAB",
            },
        )

    @classmethod
    def from_proto(cls, proto_msg) -> "Selector":
        """Create from proto message."""
        return cls(
            platform_source=PlatformSource(proto_msg.platform_source),
            path=list(proto_msg.path),
            id=proto_msg.id if proto_msg.HasField("id") else None,
            name=proto_msg.name if proto_msg.HasField("name") else None,
            control_type=ControlType(proto_msg.control_type)
            if proto_msg.HasField("control_type")
            else None,
            index=proto_msg.index if proto_msg.HasField("index") else None,
            text_hint=proto_msg.text_hint if proto_msg.HasField("text_hint") else None,
        )

    def to_proto(self):
        """Convert to proto message."""
        try:
            from cascade_client.proto import cascade_pb2

            msg = cascade_pb2.Selector(
                platform_source=self.platform_source.value,
                path=self.path,
            )

            if self.id is not None:
                msg.id = self.id

            if self.name is not None:
                msg.name = self.name

            if self.control_type is not None:
                msg.control_type = self.control_type.value

            if self.index is not None:
                msg.index = self.index

            if self.text_hint is not None:
                msg.text_hint = self.text_hint

            return msg
        except ImportError:
            raise ImportError(
                "Proto stubs not generated. Run generate_proto.ps1 or generate_proto.sh"
            )


class Action(BaseModel):
    """Action to perform on a UI element."""

    action_type: ActionType = Field(..., description="Action type")
    selector: Selector = Field(..., description="Selector for target element")
    text: Optional[str] = Field(default=None, description="Text payload")
    number: Optional[float] = Field(default=None, description="Numeric payload")
    json_payload: Optional[str] = Field(default=None, description="JSON payload")

    @classmethod
    def from_proto(cls, proto_msg) -> "Action":
        """Create from proto message."""
        action = cls(
            action_type=ActionType(proto_msg.action_type),
            selector=Selector.from_proto(proto_msg.selector),
        )

        # Handle oneof payload
        if proto_msg.HasField("text"):
            action.text = proto_msg.text
        elif proto_msg.HasField("number"):
            action.number = proto_msg.number
        elif proto_msg.HasField("json_payload"):
            action.json_payload = proto_msg.json_payload

        return action

    def to_proto(self):
        """Convert to proto message."""
        try:
            from cascade_client.proto import cascade_pb2

            msg = cascade_pb2.Action(
                action_type=self.action_type.value,
                selector=self.selector.to_proto(),
            )

            # Handle oneof payload
            if self.text is not None:
                msg.text = self.text
            elif self.number is not None:
                msg.number = self.number
            elif self.json_payload is not None:
                msg.json_payload = self.json_payload

            return msg
        except ImportError:
            raise ImportError(
                "Proto stubs not generated. Run generate_proto.ps1 or generate_proto.sh"
            )


class Mark(BaseModel):
    """Mark on a screenshot."""

    element_id: str = Field(..., description="Element ID")
    label: str = Field(..., description="Mark label")

    @classmethod
    def from_proto(cls, proto_msg) -> "Mark":
        """Create from proto message."""
        return cls(element_id=proto_msg.element_id, label=proto_msg.label)

    def to_proto(self):
        """Convert to proto message."""
        try:
            from cascade_client.proto import cascade_pb2

            return cascade_pb2.Mark(
                element_id=self.element_id,
                label=self.label,
            )
        except ImportError:
            raise ImportError(
                "Proto stubs not generated. Run generate_proto.ps1 or generate_proto.sh"
            )


class Screenshot(BaseModel):
    """Screenshot with marks."""

    image: bytes = Field(..., description="Image bytes")
    format: ImageFormat = Field(..., description="Image format")
    marks: List[Mark] = Field(default_factory=list, description="Marks on screenshot")

    @classmethod
    def from_proto(cls, proto_msg) -> "Screenshot":
        """Create from proto message."""
        return cls(
            image=proto_msg.image,
            format=ImageFormat(proto_msg.format),
            marks=[Mark.from_proto(mark) for mark in proto_msg.marks],
        )

    def to_proto(self):
        """Convert to proto message."""
        try:
            from cascade_client.proto import cascade_pb2

            return cascade_pb2.Screenshot(
                image=self.image,
                format=self.format.value,
                marks=[mark.to_proto() for mark in self.marks],
            )
        except ImportError:
            raise ImportError(
                "Proto stubs not generated. Run generate_proto.ps1 or generate_proto.sh"
            )


class Status(BaseModel):
    """Status response."""

    success: bool = Field(..., description="Success flag")
    message: str = Field(default="", description="Status message")

    @classmethod
    def from_proto(cls, proto_msg) -> "Status":
        """Create from proto message."""
        return cls(success=proto_msg.success, message=proto_msg.message)

    def to_proto(self):
        """Convert to proto message."""
        try:
            from cascade_client.proto import cascade_pb2

            return cascade_pb2.Status(success=self.success, message=self.message)
        except ImportError:
            raise ImportError(
                "Proto stubs not generated. Run generate_proto.ps1 or generate_proto.sh"
            )


class StartAppRequest(BaseModel):
    """Request to start an application."""

    app_name: str = Field(..., description="Application name/identifier")

    @classmethod
    def from_proto(cls, proto_msg) -> "StartAppRequest":
        """Create from proto message."""
        return cls(app_name=proto_msg.app_name)

    def to_proto(self):
        """Convert to proto message."""
        try:
            from cascade_client.proto import cascade_pb2

            return cascade_pb2.StartAppRequest(app_name=self.app_name)
        except ImportError:
            raise ImportError(
                "Proto stubs not generated. Run generate_proto.ps1 or generate_proto.sh"
            )


class WorkerEventType(IntEnum):
    """Worker event types for streaming progress."""

    WORKER_EVENT_UNSPECIFIED = 0
    RUN_STARTED = 1
    STEP_STARTED = 2
    STEP_COMPLETED = 3
    STEP_FAILED = 4
    CHECKPOINT_SAVED = 5
    RUN_COMPLETED = 6
    RUN_FAILED = 7
    LOG = 8


class WorkerRunRequest(BaseModel):
    """Request to start a Worker run."""

    task: Optional[str] = None
    skill_id: Optional[str] = None
    run_id: Optional[str] = None
    user_id: Optional[str] = None
    app_id: Optional[str] = None
    auth_token: Optional[str] = None
    inputs: Dict[str, str] = Field(default_factory=dict)
    dry_run: bool = False
    max_replans: int = 0

    def to_proto(self):
        """Convert to proto message."""
        try:
            from cascade_client.proto import cascade_pb2

            return cascade_pb2.WorkerRunRequest(
                task=self.task or "",
                skill_id=self.skill_id or "",
                run_id=self.run_id or "",
                user_id=self.user_id or "",
                app_id=self.app_id or "",
                auth_token=self.auth_token or "",
                inputs=self.inputs,
                dry_run=self.dry_run,
                max_replans=self.max_replans,
            )
        except ImportError:
            raise ImportError(
                "Proto stubs not generated. Run generate_proto.ps1 or generate_proto.sh"
            )

    @classmethod
    def from_proto(cls, proto_msg) -> "WorkerRunRequest":
        """Create from proto message."""
        return cls(
            task=proto_msg.task or None,
            skill_id=proto_msg.skill_id or None,
            run_id=proto_msg.run_id or None,
            user_id=proto_msg.user_id or None,
            app_id=proto_msg.app_id or None,
            auth_token=proto_msg.auth_token or None,
            inputs=dict(proto_msg.inputs),
            dry_run=proto_msg.dry_run,
            max_replans=proto_msg.max_replans,
        )


class WorkerResumeRequest(BaseModel):
    """Request to resume a Worker run from checkpoint."""

    run_id: str
    user_id: Optional[str] = None
    app_id: Optional[str] = None
    auth_token: Optional[str] = None

    def to_proto(self):
        """Convert to proto message."""
        try:
            from cascade_client.proto import cascade_pb2

            return cascade_pb2.WorkerResumeRequest(
                run_id=self.run_id,
                user_id=self.user_id or "",
                app_id=self.app_id or "",
                auth_token=self.auth_token or "",
            )
        except ImportError:
            raise ImportError(
                "Proto stubs not generated. Run generate_proto.ps1 or generate_proto.sh"
            )

    @classmethod
    def from_proto(cls, proto_msg) -> "WorkerResumeRequest":
        """Create from proto message."""
        return cls(
            run_id=proto_msg.run_id,
            user_id=proto_msg.user_id or None,
            app_id=proto_msg.app_id or None,
            auth_token=proto_msg.auth_token or None,
        )


class WorkerEvent(BaseModel):
    """Streaming event emitted by the Worker runtime."""

    run_id: str
    skill_id: str
    event_type: WorkerEventType
    step_index: Optional[int] = None
    message: str = ""
    error: Optional[str] = None
    metadata: Dict[str, str] = Field(default_factory=dict)
    checkpoint: Optional[Dict[str, Any]] = None
    planning_phase: bool = False
    selected_skills: List[str] = Field(default_factory=list)
    verification_result: Optional[str] = None

    def to_proto(self):
        """Convert to proto message."""
        try:
            from cascade_client.proto import cascade_pb2
            import json

            checkpoint_json = (
                json.dumps(self.checkpoint) if self.checkpoint is not None else ""
            )
            return cascade_pb2.WorkerEvent(
                run_id=self.run_id,
                skill_id=self.skill_id,
                event_type=self.event_type.value,
                step_index=self.step_index if self.step_index is not None else -1,
                message=self.message,
                error=self.error or "",
                metadata=self.metadata,
                checkpoint=checkpoint_json,
                planning_phase=self.planning_phase,
                selected_skills=self.selected_skills,
                verification_result=self.verification_result or "",
            )
        except ImportError:
            raise ImportError(
                "Proto stubs not generated. Run generate_proto.ps1 or generate_proto.sh"
            )

    @classmethod
    def from_proto(cls, proto_msg) -> "WorkerEvent":
        """Create from proto message."""
        import json

        checkpoint = None
        if proto_msg.checkpoint:
            try:
                checkpoint = json.loads(proto_msg.checkpoint)
            except json.JSONDecodeError:
                checkpoint = {"raw": proto_msg.checkpoint}
        step_index = proto_msg.step_index if proto_msg.step_index >= 0 else None
        return cls(
            run_id=proto_msg.run_id,
            skill_id=proto_msg.skill_id,
            event_type=WorkerEventType(proto_msg.event_type),
            step_index=step_index,
            message=proto_msg.message,
            error=proto_msg.error or None,
            metadata=dict(proto_msg.metadata),
            checkpoint=checkpoint,
            planning_phase=proto_msg.planning_phase,
            selected_skills=list(proto_msg.selected_skills),
            verification_result=proto_msg.verification_result or None,
        )


# Agent-to-Agent (A2A) models -------------------------------------------------


class AgentDescriptor(BaseModel):
    """Metadata describing an agent instance."""

    agent_id: str
    role: Optional[str] = None
    run_id: Optional[str] = None

    @classmethod
    def from_proto(cls, proto_msg) -> "AgentDescriptor":
        return cls(
            agent_id=proto_msg.agent_id,
            role=proto_msg.role or None,
            run_id=proto_msg.run_id or None,
        )

    def to_proto(self):
        try:
            from cascade_client.proto import cascade_pb2

            return cascade_pb2.AgentDescriptor(
                agent_id=self.agent_id,
                role=self.role or "",
                run_id=self.run_id or "",
            )
        except ImportError:
            raise ImportError(
                "Proto stubs not generated. Run generate_proto.ps1 or generate_proto.sh"
            )


class AgentRegisterRequest(BaseModel):
    """Registration request for an agent instance."""

    user_id: str
    app_id: str
    auth_token: str
    role: Optional[str] = None
    run_id: Optional[str] = None

    def to_proto(self):
        try:
            from cascade_client.proto import cascade_pb2

            return cascade_pb2.AgentRegisterRequest(
                user_id=self.user_id,
                app_id=self.app_id,
                auth_token=self.auth_token,
                role=self.role or "",
                run_id=self.run_id or "",
            )
        except ImportError:
            raise ImportError(
                "Proto stubs not generated. Run generate_proto.ps1 or generate_proto.sh"
            )

    @classmethod
    def from_proto(cls, proto_msg) -> "AgentRegisterRequest":
        return cls(
            user_id=proto_msg.user_id,
            app_id=proto_msg.app_id,
            auth_token=proto_msg.auth_token,
            role=proto_msg.role or None,
            run_id=proto_msg.run_id or None,
        )


class AgentRegisterResponse(BaseModel):
    """Response containing assigned agent_id."""

    agent_id: str

    @classmethod
    def from_proto(cls, proto_msg) -> "AgentRegisterResponse":
        return cls(agent_id=proto_msg.agent_id)


class AgentMessage(BaseModel):
    """Message envelope for A2A communication."""

    message_id: str
    user_id: str
    app_id: str
    sender_agent_id: str
    sender_role: Optional[str] = None
    target_agent_id: Optional[str] = None
    target_role: Optional[str] = None
    run_id: Optional[str] = None
    headers: Dict[str, str] = Field(default_factory=dict)
    json_payload: str = ""
    created_at_ms: Optional[int] = None

    def to_proto(self):
        try:
            from cascade_client.proto import cascade_pb2

            return cascade_pb2.AgentMessage(
                message_id=self.message_id,
                user_id=self.user_id,
                app_id=self.app_id,
                sender_agent_id=self.sender_agent_id,
                sender_role=self.sender_role or "",
                target_agent_id=self.target_agent_id or "",
                target_role=self.target_role or "",
                run_id=self.run_id or "",
                headers=self.headers,
                json_payload=self.json_payload,
                created_at_ms=self.created_at_ms or 0,
            )
        except ImportError:
            raise ImportError(
                "Proto stubs not generated. Run generate_proto.ps1 or generate_proto.sh"
            )

    @classmethod
    def from_proto(cls, proto_msg) -> "AgentMessage":
        return cls(
            message_id=proto_msg.message_id,
            user_id=proto_msg.user_id,
            app_id=proto_msg.app_id,
            sender_agent_id=proto_msg.sender_agent_id,
            sender_role=proto_msg.sender_role or None,
            target_agent_id=proto_msg.target_agent_id or None,
            target_role=proto_msg.target_role or None,
            run_id=proto_msg.run_id or None,
            headers=dict(proto_msg.headers),
            json_payload=proto_msg.json_payload,
            created_at_ms=proto_msg.created_at_ms or None,
        )


class AgentEnvelope(BaseModel):
    """Streamed inbox delivery with ack token."""

    message: AgentMessage
    ack_token: str

    def to_proto(self):
        try:
            from cascade_client.proto import cascade_pb2

            msg_proto = self.message.to_proto()
            return cascade_pb2.AgentEnvelope(message=msg_proto, ack_token=self.ack_token)
        except ImportError:
            raise ImportError(
                "Proto stubs not generated. Run generate_proto.ps1 or generate_proto.sh"
            )

    @classmethod
    def from_proto(cls, proto_msg) -> "AgentEnvelope":
        return cls(
            message=AgentMessage.from_proto(proto_msg.message),
            ack_token=proto_msg.ack_token,
        )


class AgentInboxRequest(BaseModel):
    """Request to stream inbox messages for an agent."""

    agent_id: str
    user_id: str
    app_id: str
    run_id: Optional[str] = None
    role: Optional[str] = None

    def to_proto(self):
        try:
            from cascade_client.proto import cascade_pb2

            return cascade_pb2.AgentInboxRequest(
                agent_id=self.agent_id,
                user_id=self.user_id,
                app_id=self.app_id,
                run_id=self.run_id or "",
                role=self.role or "",
            )
        except ImportError:
            raise ImportError(
                "Proto stubs not generated. Run generate_proto.ps1 or generate_proto.sh"
            )

    @classmethod
    def from_proto(cls, proto_msg) -> "AgentInboxRequest":
        return cls(
            agent_id=proto_msg.agent_id,
            user_id=proto_msg.user_id,
            app_id=proto_msg.app_id,
            run_id=proto_msg.run_id or None,
            role=proto_msg.role or None,
        )


class AgentAck(BaseModel):
    """Acknowledgement for processed messages."""

    message_id: str
    ack_token: str
    agent_id: str
    user_id: str
    app_id: str

    def to_proto(self):
        try:
            from cascade_client.proto import cascade_pb2

            return cascade_pb2.AgentAck(
                message_id=self.message_id,
                ack_token=self.ack_token,
                agent_id=self.agent_id,
                user_id=self.user_id,
                app_id=self.app_id,
            )
        except ImportError:
            raise ImportError(
                "Proto stubs not generated. Run generate_proto.ps1 or generate_proto.sh"
            )

    @classmethod
    def from_proto(cls, proto_msg) -> "AgentAck":
        return cls(
            message_id=proto_msg.message_id,
            ack_token=proto_msg.ack_token,
            agent_id=proto_msg.agent_id,
            user_id=proto_msg.user_id,
            app_id=proto_msg.app_id,
        )


## NOTE: Code execution models removed.
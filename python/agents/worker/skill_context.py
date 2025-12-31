"""Skill context formatting for Worker agent.

This module handles loading, categorizing, and formatting skills as context
for the Worker agent. UI and Web API skills become instructional context;
native code skills remain executable.
"""

from __future__ import annotations

import json
from typing import Any, Dict, List, Literal, Optional

from cascade_client.auth.context import CascadeContext
from storage.firestore_client import FirestoreClient

from agents.explorer.skill_map import SkillMap, SkillStep


SkillType = Literal["ui", "web_api", "native_code"]


def load_all_skills(context: CascadeContext, fs: Optional[FirestoreClient] = None) -> List[SkillMap]:
    """Load all skill maps for the given app/user context."""
    client = fs or FirestoreClient(context)
    raw = client.list_skill_maps()
    skills: List[SkillMap] = []
    for _, data in raw.items():
        try:
            skills.append(SkillMap.model_validate(data))
        except Exception:
            continue
    return skills


def categorize_skill(skill: SkillMap) -> SkillType:
    """Categorize a skill based on its content.
    
    Returns:
        'ui' - UI automation skill (use base tools)
        'web_api' - Web API skill (use call_http_api)
        'native_code' - Native code skill (keep executable)
    """
    # Explicit code skill linkage (preferred signal)
    if getattr(skill.metadata, "code_artifact_id", None):
        return "native_code"
    
    # Check steps for API endpoints
    for step in skill.steps:
        if step.api_endpoint:
            url = step.api_endpoint.url.lower() if step.api_endpoint.url else ""
            if url.startswith("http://") or url.startswith("https://"):
                return "web_api"
            # Non-HTTP API (could be native code path)
            return "native_code"
    
    # Default to UI skill
    return "ui"


def format_skill_as_context(skill: SkillMap) -> str:
    """Format any skill as readable context for the LLM.
    
    The format differs based on skill type to guide the Worker
    on which tools to use.
    """
    skill_type = categorize_skill(skill)
    
    if skill_type == "ui":
        return _format_ui_skill(skill)
    elif skill_type == "web_api":
        return _format_web_api_skill(skill)
    else:
        return _format_code_skill(skill)


def _format_ui_skill(skill: SkillMap) -> str:
    """Format UI skill with base tool instructions."""
    lines = [
        f"## Skill: {skill.metadata.skill_id}",
        f"**Type**: UI Automation",
        f"**Capability**: {skill.metadata.capability or 'N/A'}",
        f"**Description**: {skill.metadata.description or 'N/A'}",
        "",
    ]
    
    # Preconditions
    if skill.metadata.preconditions:
        lines.append("### Preconditions")
        for pre in skill.metadata.preconditions:
            lines.append(f"- {pre}")
        lines.append("")
    
    # Steps
    if skill.steps:
        lines.append("### Steps (use base tools; routes by selector.platform_source)")
        for i, step in enumerate(skill.steps, 1):
            lines.append(f"{i}. **{step.action}**: {step.step_description or 'No description'}")
            
            if step.selector:
                selector_info = _format_selector(step.selector)
                lines.append(f"   - Element: {selector_info}")
                if getattr(step.selector, "platform_source", None) and step.selector.platform_source.name == "WEB":
                    lines.append("   - Use: `click_element` / `type_text` (routes to Playwright automatically for WEB)")
                    lines.append("   - For complex DOM flows, you may also use `pw_*` tools (e.g., `pw_click`, `pw_fill`, `pw_eval`).")
                else:
                    lines.append("   - Use: `click_element` or `type_text` with selector:")
                lines.append(f"     ```json")
                lines.append(f"     {json.dumps(_selector_to_dict(step.selector), indent=2)}")
                lines.append(f"     ```")
            
            if step.inputs:
                lines.append(f"   - Inputs: {json.dumps(step.inputs)}")
            lines.append("")
    
    # Postconditions
    if skill.metadata.postconditions:
        lines.append("### Expected Result")
        for post in skill.metadata.postconditions:
            lines.append(f"- {post}")
    
    return "\n".join(lines)


def _format_web_api_skill(skill: SkillMap) -> str:
    """Format web API skill with call_http_api instructions."""
    lines = [
        f"## Skill: {skill.metadata.skill_id}",
        f"**Type**: Web API",
        f"**Capability**: {skill.metadata.capability or 'N/A'}",
        f"**Description**: {skill.metadata.description or 'N/A'}",
        "",
    ]
    
    # Find API steps
    for i, step in enumerate(skill.steps, 1):
        if step.api_endpoint:
            ep = step.api_endpoint
            lines.append(f"### Step {i}: {step.step_description or step.action}")
            lines.append(f"- **Method**: {ep.method}")
            lines.append(f"- **URL**: {ep.url}")
            if ep.headers:
                lines.append(f"- **Headers**: {json.dumps(ep.headers)}")
            if ep.body_schema:
                lines.append(f"- **Body Schema**: {json.dumps(ep.body_schema)}")
            lines.append("")
            lines.append("Use `call_http_api` tool:")
            lines.append("```")
            lines.append(f'call_http_api(method="{ep.method}", url="{ep.url}", ...)')
            lines.append("```")
            lines.append("")
    
    return "\n".join(lines)


def _format_code_skill(skill: SkillMap) -> str:
    """Format native code skill (these remain executable)."""
    lines = [
        f"## Skill: {skill.metadata.skill_id}",
        f"**Type**: Native Code (Executable)",
        f"**Capability**: {skill.metadata.capability or 'N/A'}",
        f"**Description**: {skill.metadata.description or 'N/A'}",
        "",
        "> This skill runs native code (e.g., C# via Roslyn).",
        "> Use `execute_code_skill` tool to run it directly.",
        "",
    ]
    
    if skill.metadata.inputs:
        lines.append("### Required Inputs")
        for key, val in skill.metadata.inputs.items():
            lines.append(f"- `{key}`: {val}")
    
    return "\n".join(lines)


def _format_selector(selector) -> str:
    """Format selector for display."""
    parts = []
    if selector.name:
        parts.append(f'"{selector.name}"')
    if selector.control_type:
        parts.append(f"({selector.control_type.name})")
    if selector.id:
        parts.append(f"[id={selector.id}]")
    return " ".join(parts) if parts else "Unknown element"


def _selector_to_dict(selector) -> Dict[str, Any]:
    """Convert selector to dict for JSON serialization."""
    platform = getattr(selector, "platform_source", None)
    d: Dict[str, Any] = {"platform_source": platform.name if platform else "WINDOWS"}
    if selector.name:
        d["name"] = selector.name
    if selector.control_type:
        d["control_type"] = selector.control_type.name
    if selector.id:
        d["id"] = selector.id
    if selector.path:
        d["path"] = list(selector.path)
    if selector.index is not None:
        d["index"] = selector.index
    if getattr(selector, "text_hint", None):
        d["text_hint"] = selector.text_hint
    return d


def get_skill_summaries(skills: List[SkillMap]) -> List[Dict[str, str]]:
    """Get compact summaries for listing skills."""
    summaries = []
    for skill in skills:
        skill_type = categorize_skill(skill)
        summaries.append({
            "skill_id": skill.metadata.skill_id,
            "type": skill_type,
            "capability": skill.metadata.capability or "",
            "description": skill.metadata.description or "",
        })
    return summaries


def get_executable_skills(skills: List[SkillMap]) -> List[SkillMap]:
    """Return only native code skills that need executable registration."""
    return [s for s in skills if categorize_skill(s) == "native_code"]

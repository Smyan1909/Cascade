"""Skills as dynamically registered MCP tools."""

from __future__ import annotations

import json
from typing import Any, Dict, List

from cascade_client.auth.context import CascadeContext
from cascade_client.grpc_client import CascadeGrpcClient

from agents.explorer.skill_map import SkillMap
from agents.worker.skill_loader import load_all_skills


def register_skill_tools(
    registry: Any,
    context: CascadeContext,
    grpc_client: CascadeGrpcClient,
) -> None:
    """Dynamically register all discovered skills as MCP tools."""
    skills = load_all_skills(context)

    for skill in skills:
        skill_id = skill.metadata.skill_id
        tool_name = f"execute_skill_{skill_id}"

        # Build input schema from skill metadata
        input_schema = {
            "type": "object",
            "properties": {},
            "required": [],
        }

        # Add inputs from skill metadata
        if skill.metadata.inputs:
            for key, value in skill.metadata.inputs.items():
                prop_type = "string"
                if isinstance(value, int):
                    prop_type = "integer"
                elif isinstance(value, float):
                    prop_type = "number"
                elif isinstance(value, bool):
                    prop_type = "boolean"
                elif isinstance(value, dict):
                    prop_type = "object"
                elif isinstance(value, list):
                    prop_type = "array"

                input_schema["properties"][key] = {
                    "type": prop_type,
                    "description": f"Input parameter: {key}",
                }
                # Assume all inputs are required for now
                input_schema["required"].append(key)

        description = skill.metadata.description or skill.metadata.capability or f"Execute skill {skill_id}"
        if skill.metadata.preconditions:
            description += f"\nPreconditions: {', '.join(skill.metadata.preconditions)}"
        if skill.metadata.postconditions:
            description += f"\nPostconditions: {', '.join(skill.metadata.postconditions)}"

        # Create handler that executes the skill
        def make_handler(skill_map: SkillMap, grpc_client: CascadeGrpcClient):
            def handler(**kwargs):
                try:
                    skill_id = skill_map.metadata.skill_id
                    print(f"[SkillTool] Executing skill: {skill_id}")
                    print(f"[SkillTool] Steps: {len(skill_map.steps)}")
                    
                    from agents.worker.graph import StepExecutor
                    executor = StepExecutor(grpc_client, dry_run=False)
                    
                    # Execute each step
                    statuses = executor.execute_skill(skill_map)
                    success = all(st.success for st in statuses) if statuses else False
                    
                    print(f"[SkillTool] Execution complete: success={success}")
                    for st in statuses:
                        print(f"[SkillTool]   Step {st.step_index}: {st.action} - {st.message}")
                    
                    return {
                        "content": [
                            {
                                "type": "text",
                                "text": json.dumps(
                                    {
                                        "success": success,
                                        "skill_id": skill_id,
                                        "statuses": [st.model_dump() for st in statuses],
                                    }
                                ),
                            }
                        ]
                    }
                except Exception as e:
                    import traceback
                    print(f"[SkillTool] ERROR: {e}")
                    traceback.print_exc()
                    return {
                        "content": [{"type": "text", "text": f"Error executing skill: {str(e)}"}],
                        "isError": True,
                    }

            return handler

        registry.register_tool(
            name=tool_name,
            description=description,
            input_schema=input_schema,
            handler=make_handler(skill, grpc_client),
        )


def refresh_skill_tools(
    registry: Any,
    context: CascadeContext,
    grpc_client: CascadeGrpcClient,
) -> None:
    """Refresh skill tools by reloading from Firestore."""
    # Remove existing skill tools
    existing_tools = [name for name in registry._tools.keys() if name.startswith("execute_skill_")]
    for name in existing_tools:
        if name in registry._tools:
            del registry._tools[name]
        if name in registry._schemas:
            del registry._schemas[name]

    # Re-register all skills
    register_skill_tools(registry, context, grpc_client)


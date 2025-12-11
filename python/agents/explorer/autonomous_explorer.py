"""Autonomous Explorer: LLM-driven exploration using tools and vision."""

from __future__ import annotations

import json
import uuid
from typing import Any, Dict, List, Optional

from cascade_client.auth.context import CascadeContext
from cascade_client.grpc_client import CascadeGrpcClient

from mcp_server.tool_registry import ToolRegistry
from mcp_server.body_tools import register_body_tools
from mcp_server.explorer_tools import register_explorer_tools

from agents.core.autonomous_agent import (
    AgentConfig, AgentResult, AgentStatus, AutonomousAgent, ReActVerifier
)
from agents.core.verify_prompts import EXPLORER_VERIFY_PROMPT, get_explorer_verify_task
from .prompts_autonomous import EXPLORER_SYSTEM_PROMPT, get_explorer_task
from .skill_map import SkillMap, SkillMetadata, SkillStep


class HybridExplorer:
    """
    Autonomous Explorer that uses LLM-driven exploration with tools and vision.
    
    Architecture:
    - Exploration: Pure agentic loop where LLM uses tools to explore
      - get_semantic_tree() - See the UI structure
      - get_screenshot() - See the app visually
      - click_element(), type_text() - Interact with UI
      - web_search() - Look up documentation
      - save_skill_map() - Save discovered skills
    - Verification: ReAct loop to test the generated skills
    
    The LLM does ALL the reasoning about what elements do and how to map them
    to capabilities. No hardcoded rules or synonym matching.
    """

    def __init__(
        self,
        context: CascadeContext,
        grpc_client: CascadeGrpcClient,
        max_explore_iterations: int = 50,
        max_verify_iterations: int = 10,
        verbose: bool = True,
    ):
        self._context = context
        self._grpc = grpc_client
        self._max_explore_iterations = max_explore_iterations
        self._max_verify_iterations = max_verify_iterations
        self._verbose = verbose
        
        # Setup tool registry with all available tools
        self._registry = ToolRegistry()
        register_body_tools(self._registry, grpc_client)
        register_explorer_tools(self._registry)
        self._add_skill_tools()

    def _add_skill_tools(self) -> None:
        """Add skill map saving tool for the explorer agent."""
        from storage.firestore_client import FirestoreClient
        
        fs = FirestoreClient(self._context)
        context = self._context  # Capture for closure
        
        def save_skill_map(skill_map_json: str) -> Dict[str, Any]:
            """Save a skill map to Firestore."""
            try:
                skill_data = json.loads(skill_map_json)
                
                # Ensure required metadata fields
                if "metadata" in skill_data:
                    skill_data["metadata"]["app_id"] = skill_data["metadata"].get("app_id", context.app_id)
                    skill_data["metadata"]["user_id"] = skill_data["metadata"].get("user_id", context.user_id)
                    if "skill_id" not in skill_data["metadata"]:
                        skill_data["metadata"]["skill_id"] = str(uuid.uuid4())
                
                skill_map = SkillMap.model_validate(skill_data)
                fs.upsert_skill_map(skill_map)
                return {
                    "content": [{
                        "type": "text", 
                        "text": f"Successfully saved skill map: {skill_map.metadata.skill_id}"
                    }]
                }
            except Exception as e:
                return {
                    "content": [{"type": "text", "text": f"Error saving skill map: {str(e)}"}],
                    "isError": True
                }
        
        self._registry.register_tool(
            name="save_skill_map",
            description="""Save a skill map to storage. The skill_map_json should be a JSON string with this format:
{
  "metadata": {
    "skill_id": "optional-id",
    "app_id": "application-name",
    "user_id": "user-id",
    "capability": "what this skill does",
    "description": "detailed description"
  },
  "steps": [
    {
      "action": "Click",
      "step_description": "what this step does",
      "selector": {
        "platform_source": "WINDOWS",
        "name": "Button Name",
        "control_type": "BUTTON",
        "path": ["element-id"]
      }
    }
  ]
}""",
            input_schema={
                "type": "object",
                "properties": {"skill_map_json": {"type": "string", "description": "JSON string of the skill map"}},
                "required": ["skill_map_json"]
            },
            handler=lambda skill_map_json: save_skill_map(skill_map_json),
        )

    def _log(self, msg: str) -> None:
        if self._verbose:
            print(f"[Explorer] {msg}")

    def explore(
        self,
        app_name: str,
        instructions: Optional[Dict[str, Any]] = None,
        run_id: Optional[str] = None,
        skip_verify: bool = False,
    ) -> AgentResult:
        """
        Run autonomous exploration.
        
        The LLM agent will:
        1. Launch the app (if needed)
        2. Use get_semantic_tree() and get_screenshot() to observe
        3. Reason about what each element does
        4. Click/interact with elements to test them
        5. Build skill maps based on its understanding
        6. Save skill maps when it's discovered capabilities
        7. Continue until it has explored all requested coverage areas
        
        Args:
            app_name: Application to explore
            instructions: Optional instructions dict with coverage areas
            run_id: Optional run identifier
            skip_verify: Skip verification phase
            
        Returns:
            AgentResult with exploration outcome
        """
        run_id = run_id or uuid.uuid4().hex[:8]
        
        # =====================================================
        # PHASE 1: AGENTIC EXPLORATION
        # =====================================================
        self._log("=== PHASE 1: AGENTIC EXPLORATION ===")
        self._log("LLM will use tools to explore the application")
        
        # Create the exploration task
        task = get_explorer_task(app_name, instructions or {})
        
        # Create the exploration agent
        config = AgentConfig(
            max_iterations=self._max_explore_iterations,
            verbose=self._verbose,
            thread_id=f"explore_{run_id}",
        )
        
        explorer_agent = AutonomousAgent(
            tool_registry=self._registry,
            system_prompt=EXPLORER_SYSTEM_PROMPT,
            config=config,
        )
        
        # Run the exploration
        self._log(f"Starting exploration with max {self._max_explore_iterations} iterations")
        explore_result = explorer_agent.run(task)
        
        self._log(f"Exploration complete: {explore_result.status.value}")
        self._log(f"Tool calls: {len(explore_result.tool_calls)}")
        
        if explore_result.status == AgentStatus.FAILED:
            return explore_result
        
        if skip_verify:
            self._log("Skipping verification phase")
            return explore_result
        
        # =====================================================
        # PHASE 2: VERIFICATION (optional)
        # =====================================================
        self._log("=== PHASE 2: VERIFICATION ===")
        
        # Get the skill maps that were saved during exploration
        skill_ids = self._get_recent_skill_ids(explore_result)
        
        if not skill_ids:
            self._log("No skill maps found to verify")
            return explore_result
        
        self._log(f"Verifying {len(skill_ids)} skill maps")
        
        verifier = ReActVerifier(
            tool_registry=self._registry,
            system_prompt=EXPLORER_VERIFY_PROMPT,
            max_iterations=self._max_verify_iterations,
            verbose=self._verbose,
        )
        
        # Verify each skill
        for skill_id in skill_ids:
            skill_summary = self._get_skill_summary(skill_id)
            if skill_summary:
                verify_task = get_explorer_verify_task(skill_summary, app_name)
                verify_result = verifier.verify(
                    verification_task=verify_task,
                    context={"skill_id": skill_id},
                    thread_id=f"verify_{run_id}_{skill_id[:8]}",
                )
                
                if verify_result.success:
                    self._log(f"Skill {skill_id[:8]}... verified")
                else:
                    self._log(f"Skill {skill_id[:8]}... needs work: {verify_result.feedback[:50]}...")
        
        return AgentResult(
            status=AgentStatus.COMPLETED,
            final_response=explore_result.final_response,
            iterations=explore_result.iterations,
            tool_calls=explore_result.tool_calls,
            elapsed_seconds=explore_result.elapsed_seconds,
        )

    def _get_recent_skill_ids(self, result: AgentResult) -> List[str]:
        """Extract skill IDs from tool calls in the result."""
        skill_ids = []
        for tc in result.tool_calls:
            if tc.get("name") == "save_skill_map":
                args = tc.get("arguments", {})
                if "skill_map_json" in args:
                    try:
                        data = json.loads(args["skill_map_json"])
                        if "metadata" in data and "skill_id" in data["metadata"]:
                            skill_ids.append(data["metadata"]["skill_id"])
                    except:
                        pass
        return skill_ids

    def _get_skill_summary(self, skill_id: str) -> Optional[str]:
        """Get a summary of a skill map from Firestore."""
        try:
            from storage.firestore_client import FirestoreClient
            fs = FirestoreClient(self._context)
            skill_data = fs.get_skill_map(skill_id)
            
            if not skill_data:
                return None
            
            meta = skill_data.get("metadata", {})
            steps = skill_data.get("steps", [])
            
            lines = [
                f"Skill ID: {skill_id}",
                f"Capability: {meta.get('capability', 'N/A')}",
                f"Description: {meta.get('description', 'N/A')}",
                f"Steps ({len(steps)}):",
            ]
            for i, step in enumerate(steps[:10], 1):  # Limit to first 10
                action = step.get('action', 'unknown')
                desc = step.get('step_description', '')
                lines.append(f"  {i}. {action}: {desc}")
            
            if len(steps) > 10:
                lines.append(f"  ... and {len(steps) - 10} more steps")
            
            return "\n".join(lines)
        except Exception as e:
            self._log(f"Could not get skill summary: {e}")
            return None


# Backward compatibility alias
AutonomousExplorer = HybridExplorer


def build_autonomous_explorer(
    context: CascadeContext,
    grpc_client: CascadeGrpcClient,
) -> HybridExplorer:
    """Factory function to build explorer."""
    return HybridExplorer(context, grpc_client)

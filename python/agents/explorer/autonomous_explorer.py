"""Autonomous Explorer: LLM-driven exploration using tools and vision."""

from __future__ import annotations

import json
import uuid
from typing import Any, Dict, List, Optional

from cascade_client.auth.context import CascadeContext
from cascade_client.grpc_client import CascadeGrpcClient

from mcp_server.tool_registry import ToolRegistry
from mcp_server.body_tools import register_body_tools
# NOTE: explorer_tools imported lazily in __init__ to avoid circular import

from agents.core.autonomous_agent import (
    AgentConfig, AgentResult, AgentStatus, AutonomousAgent, ReActVerifier
)
from agents.core.planning_agent import PlanningAgent, Plan, get_user_plan_approval
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
        max_explore_iterations: int = 500,  # High limit - agent decides when done
        max_verify_iterations: int = 25,    # More time for thorough verification
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
        
        # Lazy import to avoid circular dependency
        from mcp_server.explorer_tools import register_explorer_tools
        register_explorer_tools(self._registry)
        self._add_skill_tools()

    def _add_skill_tools(self) -> None:
        """Add skill map and documentation saving tools for the explorer agent."""
        from storage.firestore_client import FirestoreClient
        from .documentation_map import DocumentationMap
        
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
        
        def save_documentation(documentation_json: str) -> Dict[str, Any]:
            """Save documentation to Firestore."""
            try:
                doc_data = json.loads(documentation_json)
                
                # Ensure required metadata fields
                if "metadata" in doc_data:
                    doc_data["metadata"]["app_id"] = doc_data["metadata"].get("app_id", context.app_id)
                    doc_data["metadata"]["user_id"] = doc_data["metadata"].get("user_id", context.user_id)
                    if "doc_id" not in doc_data["metadata"]:
                        doc_data["metadata"]["doc_id"] = str(uuid.uuid4())
                
                documentation = DocumentationMap.model_validate(doc_data)
                fs.upsert_documentation(documentation)
                return {
                    "content": [{
                        "type": "text",
                        "text": f"Successfully saved documentation: {documentation.metadata.doc_id} - '{documentation.metadata.title}'"
                    }]
                }
            except Exception as e:
                return {
                    "content": [{"type": "text", "text": f"Error saving documentation: {str(e)}"}],
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
        
        self._registry.register_tool(
            name="save_documentation",
            description="""Save structured documentation about the application to storage. Use this to document:
- Application overviews and navigation guides
- Workflow descriptions and best practices
- UI element guides and their purposes
- Troubleshooting tips and common issues

The documentation_json should be a JSON string with this format:
{
  "metadata": {
    "doc_id": "optional-id",
    "title": "Document Title",
    "doc_type": "overview|workflow|element_guide|troubleshooting",
    "description": "Brief summary of this documentation",
    "tags": ["tag1", "tag2"],
    "related_skills": ["skill-id-1", "skill-id-2"]
  },
  "sections": [
    {
      "heading": "Section Title",
      "content": "Markdown content describing this section...",
      "element_references": ["Button Name", "Menu Item"],
      "code_examples": []
    }
  ]
}""",
            input_schema={
                "type": "object",
                "properties": {"documentation_json": {"type": "string", "description": "JSON string of the documentation"}},
                "required": ["documentation_json"]
            },
            handler=lambda documentation_json: save_documentation(documentation_json),
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
        auto_approve: bool = False,
    ) -> AgentResult:
        """
        Run autonomous exploration with Plan-Approve-Execute flow.
        
        The agent will:
        1. Create a detailed exploration plan
        2. Wait for user approval (unless auto_approve=True)
        3. Execute the approved plan
        4. Verify the created skills
        
        Args:
            app_name: Application to explore
            instructions: Optional instructions dict with coverage areas
            run_id: Optional run identifier
            skip_verify: Skip verification phase
            auto_approve: Skip plan approval step
            
        Returns:
            AgentResult with exploration outcome
        """
        run_id = run_id or uuid.uuid4().hex[:8]
        
        # Build goal description from instructions
        goal = f"Explore {app_name} and create skill maps for all capabilities"
        context = ""
        if instructions:
            if "objective" in instructions:
                goal = instructions["objective"]
            if "coverage" in instructions:
                areas = []
                for category, items in instructions["coverage"].items():
                    if isinstance(items, list):
                        areas.extend(items)
                context = f"Capabilities to discover: {', '.join(areas)}"
        
        # =====================================================
        # PHASE 0: PLANNING (with approval loop)
        # =====================================================
        self._log("=== PHASE 0: PLANNING ===")
        self._log("Creating exploration plan...")
        
        planner = PlanningAgent(verbose=self._verbose)
        plan = planner.create_plan(goal, app_name, context)
        
        # Approval loop
        if not auto_approve:
            self._log("Waiting for user approval...")
            approved = False
            while not approved:
                approved, feedback = get_user_plan_approval(plan)
                if not approved:
                    if feedback == "User rejected the plan" or feedback == "Cancelled by user":
                        self._log("Plan rejected by user")
                        return AgentResult(
                            status=AgentStatus.FAILED,
                            final_response="Plan rejected by user",
                            iterations=0,
                        )
                    # Refine the plan based on feedback
                    self._log(f"Refining plan based on feedback: {feedback[:50]}...")
                    plan = planner.refine_plan(plan, feedback)
            
            self._log("Plan approved by user")
        else:
            self._log("Auto-approving plan")
            print("\n" + plan.to_display_string())
        
        plan.status = plan.status.APPROVED
        
        # =====================================================
        # PHASE 1: EXECUTION (exploration with approved plan)
        # =====================================================
        self._log("=== PHASE 1: EXECUTION ===")
        self._log("Executing approved plan...")
        
        # Create the exploration task with plan context
        task = plan.to_execution_prompt() + "\n\n" + get_explorer_task(app_name, instructions or {})
        
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
        # PHASE 2: VERIFICATION WITH RE-EXPLORATION
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
        
        max_fix_attempts = 2  # Max re-exploration attempts per skill
        verified_skills = []
        failed_skills = []
        
        # Verify each skill with re-exploration loop
        for skill_id in skill_ids:
            attempts = 0
            skill_verified = False
            
            while attempts <= max_fix_attempts and not skill_verified:
                skill_summary = self._get_skill_for_verification(skill_id)
                if not skill_summary:
                    self._log(f"Skill {skill_id[:8]}... not found, skipping")
                    break
                
                # Run verification
                verify_task = get_explorer_verify_task(skill_summary, app_name)
                verify_result = verifier.verify(
                    verification_task=verify_task,
                    context={"skill_id": skill_id},
                    thread_id=f"verify_{run_id}_{skill_id[:8]}_{attempts}",
                )
                
                if verify_result.success:
                    self._log(f"Skill {skill_id[:8]}... VERIFIED ✓")
                    verified_skills.append(skill_id)
                    skill_verified = True
                else:
                    attempts += 1
                    self._log(f"Skill {skill_id[:8]}... needs work (attempt {attempts}/{max_fix_attempts})")
                    self._log(f"  Issues: {verify_result.feedback[:100]}...")
                    
                    if attempts <= max_fix_attempts:
                        # =====================================================
                        # RE-EXPLORATION: Fix the skill based on feedback
                        # =====================================================
                        self._log(f"=== RE-EXPLORING to fix {skill_id[:8]}... ===")
                        
                        fix_task = self._create_fix_task(
                            skill_id, skill_summary, 
                            verify_result.feedback, app_name
                        )
                        
                        fix_config = AgentConfig(
                            max_iterations=30,  # Shorter for focused fixes
                            verbose=self._verbose,
                            thread_id=f"fix_{run_id}_{skill_id[:8]}_{attempts}",
                        )
                        
                        fix_agent = AutonomousAgent(
                            tool_registry=self._registry,
                            system_prompt=EXPLORER_SYSTEM_PROMPT,
                            config=fix_config,
                        )
                        
                        fix_result = fix_agent.run(fix_task)
                        self._log(f"Fix attempt complete: {fix_result.status.value}")
                    else:
                        self._log(f"Skill {skill_id[:8]}... FAILED after {max_fix_attempts} attempts ✗")
                        failed_skills.append(skill_id)
        
        # Summary
        self._log(f"=== VERIFICATION COMPLETE ===")
        self._log(f"Verified: {len(verified_skills)}, Failed: {len(failed_skills)}")
        
        return AgentResult(
            status=AgentStatus.COMPLETED if not failed_skills else AgentStatus.COMPLETED,
            final_response=f"Verified {len(verified_skills)} skills. Failed: {len(failed_skills)}. " + explore_result.final_response,
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

    def _get_skill_for_verification(self, skill_id: str) -> Optional[str]:
        """Get a skill map from Firestore for verification."""
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
            for i, step in enumerate(steps[:10], 1):
                lines.append(f"  {i}. {json.dumps(step)}")
            
            if len(steps) > 10:
                lines.append(f"  ... and {len(steps) - 10} more steps")
            
            return "\n".join(lines)
        except Exception as e:
            self._log(f"Could not get skill summary: {e}")
            return None

    def _create_fix_task(
        self, 
        skill_id: str, 
        skill_summary: str, 
        feedback: str, 
        app_name: str
    ) -> str:
        """Create a targeted task to fix a failed skill."""
        return f"""## Fix Skill: {skill_id}

Application: **{app_name}**

### Current Skill (NEEDS FIXING)
{skill_summary}

### Verification Feedback
{feedback}

### Your Task
1. Analyze what's wrong with this skill based on the feedback
2. Re-discover the correct selector(s) for this capability
3. Test the fix with an end-to-end verification
4. Save the UPDATED skill map (use the SAME skill_id: {skill_id})

IMPORTANT: 
- Use get_semantic_tree() to find the correct elements
- Test the skill works before saving
- Save with the SAME skill_id to overwrite the broken skill
"""

# Backward compatibility alias
AutonomousExplorer = HybridExplorer


def build_autonomous_explorer(
    context: CascadeContext,
    grpc_client: CascadeGrpcClient,
) -> HybridExplorer:
    """Factory function to build explorer."""
    return HybridExplorer(context, grpc_client)

"""Autonomous Explorer: LLM-driven exploration using tools and vision."""

from __future__ import annotations

import json
import uuid
from typing import Any, Dict, List, Optional

from cascade_client.auth.context import CascadeContext
from cascade_client.grpc_client import CascadeGrpcClient

from mcp_server.tool_registry import ToolRegistry
from mcp_server.body_tools import register_body_tools

from agents.core.autonomous_agent import (
    AgentConfig, AgentResult, AgentStatus, AutonomousAgent
)
from .prompts_autonomous import EXPLORER_SYSTEM_PROMPT, get_explorer_task
from .skill_map import SkillMap, SkillMetadata, SkillStep


class HybridExplorer:
    """
    Autonomous Explorer that uses LLM-driven exploration with tools and vision.
    
    Architecture:
    - Planning: Creates an exploration plan and gets user approval
    - Exploration: Hypothesis-driven discovery where LLM:
      - Forms hypotheses about UI elements
      - Tests them through interaction
      - Saves confirmed skills
    
    Verification is handled inline via hypothesis testing rather than 
    as a separate phase. The Explorer tests each capability before saving.
    """

    def __init__(
        self,
        context: CascadeContext,
        grpc_client: CascadeGrpcClient,
        max_explore_iterations: int = 500,
        verbose: bool = True,
    ):
        self._context = context
        self._grpc = grpc_client
        self._max_explore_iterations = max_explore_iterations
        self._verbose = verbose
        
        # Setup tool registry with all available tools
        self._registry = ToolRegistry()
        register_body_tools(self._registry, grpc_client)
        
        # Lazy import to avoid circular dependency
        from mcp_server.explorer_tools import register_explorer_tools
        register_explorer_tools(self._registry)
        self._add_skill_tools()
        
        # Load existing skills to avoid recreating them
        self._existing_skills: List[SkillMap] = []
        self._load_existing_skills()

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
    "description": "REQUIRED: Clear, human-readable description of what this skill does",
    "initial_state_description": "REQUIRED: Describe the application state when this skill is valid (e.g., 'Calculator in Standard mode')",
    "requires_initial_state": false,
    "initial_state_tree": null
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
}

IMPORTANT:
- `description`: Always provide a clear description of what the skill does
- `initial_state_description`: Always describe the app state when this skill was discovered
- `requires_initial_state`: Set to true if the app MUST be in that state to use this skill
- `initial_state_tree`: Include semantic tree snapshot ONLY if the description is ambiguous""",
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

    def _load_existing_skills(self) -> None:
        """Load existing skills from Firestore to avoid recreating them."""
        from storage.firestore_client import FirestoreClient
        from agents.worker.skill_context import load_all_skills
        
        try:
            fs = FirestoreClient(self._context)
            self._existing_skills = load_all_skills(self._context, fs)
            if self._verbose and self._existing_skills:
                print(f"[Explorer] Loaded {len(self._existing_skills)} existing skills")
        except Exception as e:
            if self._verbose:
                print(f"[Explorer] Could not load existing skills: {e}")
            self._existing_skills = []

    def _format_existing_skills_summary(self) -> str:
        """Format existing skills as a summary for the prompt."""
        if not self._existing_skills:
            return ""
        
        lines = [
            "\n## Existing Skills (DO NOT RECREATE)",
            "The following skills already exist for this application. Skip these capabilities:",
            ""
        ]
        
        for skill in self._existing_skills:
            meta = skill.metadata
            skill_info = f"- **{meta.skill_id}**: {meta.description or meta.capability or 'No description'}"
            if meta.requires_initial_state and meta.initial_state_description:
                skill_info += f" (requires state: {meta.initial_state_description})"
            lines.append(skill_info)
        
        lines.append("")
        lines.append("Focus on discovering NEW capabilities not listed above.")
        return "\n".join(lines)

    def _log(self, msg: str) -> None:
        if self._verbose:
            print(f"[Explorer] {msg}")

    def _regenerate_plan_with_feedback(
        self,
        app_name: str,
        instructions: Dict[str, Any],
        existing_skills: List[SkillMap],
        feedback_history: List[str],
    ) -> str:
        """Regenerate the exploration plan incorporating user feedback.
        
        Args:
            app_name: Application to explore
            instructions: Original instructions dict
            existing_skills: List of existing skills to avoid recreating
            feedback_history: List of user feedback strings from previous iterations
            
        Returns:
            Updated plan text incorporating user feedback
        """
        from .prompts_autonomous import refine_explorer_plan
        
        return refine_explorer_plan(
            app_name=app_name,
            instructions=instructions,
            existing_skills=existing_skills,
            feedback_history=feedback_history,
        )

    def explore(
        self,
        app_name: str,
        instructions: Optional[Dict[str, Any]] = None,
        run_id: Optional[str] = None,
        auto_approve: bool = False,
        summarized_conversation_history: Optional[str] = None,
        raw_conversation_history: Optional[List[Dict[str, str]]] = None,
        iteration_count: int = 0,
    ) -> AgentResult:
        """
        Run autonomous exploration with Plan-Approve-Execute flow.
        
        The agent will:
        1. Create a detailed exploration plan
        2. Wait for user approval (unless auto_approve=True)
        3. Execute the approved plan with hypothesis-driven testing
        
        Skills are tested inline during exploration via hypothesis validation.
        Only confirmed, working skills are saved.
        
        Args:
            app_name: Application to explore
            instructions: Optional instructions dict with coverage areas
            run_id: Optional run identifier
            auto_approve: Skip plan approval step
            
        Returns:
            AgentResult with exploration outcome
        """
        run_id = run_id or uuid.uuid4().hex[:8]
        
        # =====================================================
        # PHASE 1: STREAMLINED PLANNING
        # =====================================================
        self._log("=== PHASE 1: PLANNING ===")
        self._log("Creating skill acquisition plan...")
        
        # Use streamlined explorer-specific planning
        from .prompts_autonomous import create_explorer_plan
        
        plan_text = create_explorer_plan(
            app_name=app_name,
            instructions=instructions or {},
            existing_skills=self._existing_skills,
        )
        
        new_instructions = None

        
        # Display and approve the plan
        print("\n" + "━" * 50)
        print("EXPLORATION PLAN")
        print("━" * 50)
        print(plan_text)
        print("━" * 50)
        
        if not auto_approve and iteration_count == 0:
            # Iterative plan approval with feedback loop
            plan_approved = False
            user_feedback_history: List[str] = []
            
            while not plan_approved:
                self._log("Waiting for user approval...")
                try:
                    response = input("\n[?] Approve plan? [y]es / [n]o / [m]odify: ").strip().lower()
                    
                    if response in ("y", "yes"):
                        plan_approved = True
                        self._log("Plan approved by user")
                    elif response in ("n", "no"):
                        self._log("Plan rejected by user")
                        return AgentResult(
                            status=AgentStatus.FAILED,
                            final_response="Plan rejected by user",
                            iterations=0,
                        )
                    elif response in ("m", "modify"):
                        # Prompt for feedback
                        feedback = input("[?] Enter your feedback: ").strip()
                        if feedback:
                            self._log(f"Refining plan with feedback: {feedback}")
                            user_feedback_history.append(feedback)
                            
                            # Regenerate plan with feedback context
                            plan_text = self._regenerate_plan_with_feedback(
                                app_name=app_name,
                                instructions=instructions or {},
                                existing_skills=self._existing_skills,
                                feedback_history=user_feedback_history,
                            )
                            
                            # Display the refined plan
                            print("\n" + "━" * 50)
                            print("REFINED EXPLORATION PLAN")
                            print("━" * 50)
                            print(plan_text)
                            print("━" * 50)
                        else:
                            print("[!] No feedback provided, plan unchanged")
                    else:
                        print("[!] Please enter y, n, or m")
                        
                except (KeyboardInterrupt, EOFError):
                    self._log("Cancelled by user")
                    return AgentResult(
                        status=AgentStatus.FAILED,
                        final_response="Cancelled by user",
                        iterations=0,
                    )
        # If we're continuing in the same chat session, we need to get new instructions from the user
        elif iteration_count > 0:
            new_instructions = input("Enter continuation instructions: ").strip()
            if new_instructions:
                self._log(f"Continuation instructions: {new_instructions}")

                # Use LLM to parse/refine user's freeform instructions into proper dict format
                # (see get_explorer_task signature and prompt format in prompts_autonomous.py)
                from clients.llm_client import LlmMessage, load_llm_client_from_env

                llm_client = load_llm_client_from_env()
                system_prompt = (
                    "You are an assistant that converts freeform user continuation instructions for a UI exploration "
                    "session into a structured JSON dictionary suitable for an autonomous explorer agent.\n"
                    "Target format has these optional keys: objective (string), coverage (dict of lists of strings), "
                    "constraints (list of strings). DO NOT add extra fields.\n"
                    "Output ONLY a JSON object (no explanation).\n\n"
                    "Example input:\n"
                    "User: Instead, focus on accessibility features and find any export and import workflows. Avoid sending emails.\n"
                    "Example output:\n"
                    '{\n'
                    '  "objective": "Find accessibility features and import/export workflows",\n'
                    '  "coverage": {"features": ["accessibility", "export", "import"]},\n'
                    '  "constraints": ["Do not send emails"]\n'
                    '}\n'
                    "Example input:\n"
                    "User: Instead, focus on accessibility features and find any export and import workflows. Avoid sending emails.\n"
                    "Example output:\n"
                    '{\n'
                    '  "objective": "Find accessibility features and import/export workflows",\n'
                    '  "coverage": {"features": ["accessibility", "export", "import"]},\n'
                    '  "constraints": ["Do not send emails"]\n'
                    '}\n'
                )

                messages = [
                    LlmMessage(role="system", content=system_prompt),
                    LlmMessage(role="user", content=new_instructions)
                ]
                try:
                    response = llm_client.generate(messages, temperature=0.0, max_tokens=400)
                    instructions_text = response.content.strip()
                    try:
                        # Attempt parsing LLM output as JSON
                        import json
                        parsed_instructions = json.loads(instructions_text)
                        instructions = parsed_instructions
                    except Exception as parse_err:
                        self._log(f"[!] Failed to parse LLM output as JSON: {instructions_text}\nError: {parse_err}")
                        print("[!] Could not parse your instructions into a valid format. Try again.")
                        instructions = {}
                except Exception as err:
                    self._log(f"[!] Error calling LLM for continuation instruction parsing: {err}")
                    print("[!] Could not process your instructions due to an internal error.")
                    instructions = {}
        else:
            self._log("Auto-approving plan")

        
        # =====================================================
        # PHASE 2: EXPLORATION (hypothesis-driven)
        # =====================================================
        self._log("=== PHASE 2: EXPLORATION ===")
        self._log("Executing exploration with hypothesis testing...")
        
        # Create the exploration task with plan context and existing skills
        existing_skills_summary = self._format_existing_skills_summary()

        if iteration_count == 0:
            task = (
                f"## Your Exploration Plan\n\n{plan_text}\n\n" +
                get_explorer_task(app_name, instructions or {}) +
                existing_skills_summary
            )
        else:
            task = (
                f"## Continue your exploration plan based on the user's instructions: {new_instructions}\n\n" +
                get_explorer_task(app_name, instructions or {}) +
                existing_skills_summary
            )
        # Create the exploration agent
        config = AgentConfig(
            max_iterations=self._max_explore_iterations,
            verbose=self._verbose,
            thread_id=f"explore_{run_id}",
            enable_verification=True,
        )
        
        explorer_agent = AutonomousAgent(
            tool_registry=self._registry,
            system_prompt=EXPLORER_SYSTEM_PROMPT,
            config=config,
            summarized_conversation_history=summarized_conversation_history,
            raw_conversation_history=raw_conversation_history,
        )
        
        # Run the exploration
        self._log(f"Starting exploration with max {self._max_explore_iterations} iterations")
        explore_result = explorer_agent.run(task)
        
        self._log(f"Exploration complete: {explore_result.status.value}")
        self._log(f"Tool calls: {len(explore_result.tool_calls)}")
        
        # Count saved skills
        skill_count = sum(1 for tc in explore_result.tool_calls if tc.get("name") == "save_skill_map")
        self._log(f"Skills saved: {skill_count}")
        
        return explore_result


# Backward compatibility alias
AutonomousExplorer = HybridExplorer


def build_autonomous_explorer(
    context: CascadeContext,
    grpc_client: CascadeGrpcClient,
) -> HybridExplorer:
    """Factory function to build explorer."""
    return HybridExplorer(context, grpc_client)

"""Autonomous Worker agent using pure ReAct pattern."""

from __future__ import annotations

import json
import uuid
from typing import Any, Dict, List, Optional

from cascade_client.auth.context import CascadeContext
from cascade_client.grpc_client import CascadeGrpcClient

from mcp_server.tool_registry import ToolRegistry
from mcp_server.body_tools import register_body_tools
from mcp_server.explorer_tools import register_explorer_tools
from mcp_server.api_tools import register_api_tools, register_code_execution_tool

from agents.core.autonomous_agent import AutonomousAgent, AgentConfig, AgentResult
from agents.worker.prompts_autonomous import WORKER_SYSTEM_PROMPT, get_worker_task
from agents.worker.skill_context import (
    load_all_skills,
    categorize_skill,
    format_skill_as_context,
    get_skill_summaries,
    get_executable_skills,
)


class AutonomousWorker:
    """
    Worker agent using pure ReAct pattern.
    
    Unlike Explorer/Orchestrator, Worker uses pure ReAct because:
    - Tasks are usually short and focused
    - Dynamic UI state requires reactive decision-making
    - No complex planning needed for single tasks
    """

    def __init__(
        self,
        context: CascadeContext,
        grpc_client: CascadeGrpcClient,
        config: Optional[AgentConfig] = None,
    ):
        self._context = context
        self._grpc = grpc_client
        self._config = config or AgentConfig(
            max_iterations=30,
            verbose=True,
            thread_id=f"worker_{uuid.uuid4().hex[:8]}",
        )
        
        # Setup MCP tool registry
        self._registry = ToolRegistry()
        register_body_tools(self._registry, grpc_client)
        register_explorer_tools(self._registry)
        self._register_skill_context_tools()
        self._register_api_tools()
        self._register_documentation_tools()

    def _register_skill_context_tools(self) -> None:
        """Register skill context tools (list and read skills)."""
        context = self._context
        
        def list_skills() -> Dict[str, Any]:
            """List all available skills with summaries."""
            try:
                skills = load_all_skills(context)
                summaries = get_skill_summaries(skills)
                
                if not summaries:
                    return {
                        "content": [{"type": "text", "text": "No skills available for this application."}]
                    }
                
                lines = [f"## Available Skills ({len(summaries)} total)\n"]
                for s in summaries:
                    type_badge = f"[{s['type'].upper()}]"
                    lines.append(f"- **{s['skill_id']}** {type_badge}")
                    if s['capability']:
                        lines.append(f"  Capability: {s['capability']}")
                    if s['description']:
                        lines.append(f"  {s['description'][:100]}")
                    lines.append("")
                
                lines.append("\nUse `read_skill(skill_id)` to get detailed instructions.")
                
                return {
                    "content": [{"type": "text", "text": "\n".join(lines)}]
                }
            except Exception as e:
                return {
                    "content": [{"type": "text", "text": f"Error listing skills: {str(e)}"}],
                    "isError": True
                }
        
        def read_skill(skill_id: str) -> Dict[str, Any]:
            """Read full skill content as context."""
            try:
                skills = load_all_skills(context)
                skill = next((s for s in skills if s.metadata.skill_id == skill_id), None)
                
                if not skill:
                    return {
                        "content": [{"type": "text", "text": f"Skill '{skill_id}' not found."}],
                        "isError": True
                    }
                
                formatted = format_skill_as_context(skill)
                return {
                    "content": [{"type": "text", "text": formatted}]
                }
            except Exception as e:
                return {
                    "content": [{"type": "text", "text": f"Error reading skill: {str(e)}"}],
                    "isError": True
                }
        
        # Register list_skills tool
        self._registry.register_tool(
            name="list_skills",
            description="""List all available skills for this application.

Returns summaries of skills with their types:
- UI: Use base tools (click_element, type_text) guided by skill instructions
- WEB_API: Use call_http_api tool with skill endpoint details
- NATIVE_CODE: Use execute_code_skill for C#/Roslyn automation

Call read_skill(skill_id) to get detailed instructions for a specific skill.""",
            input_schema={"type": "object", "properties": {}},
            handler=list_skills,
        )
        
        # Register read_skill tool
        self._registry.register_tool(
            name="read_skill",
            description="""Read detailed instructions for a specific skill.

Returns step-by-step guidance on how to execute the skill:
- For UI skills: Shows which elements to interact with and selectors to use
- For API skills: Shows endpoints, methods, and parameters
- For code skills: Shows how to call execute_code_skill

IMPORTANT: Read skills BEFORE executing tasks to understand the right approach.""",
            input_schema={
                "type": "object",
                "properties": {
                    "skill_id": {
                        "type": "string",
                        "description": "ID of the skill to read"
                    }
                },
                "required": ["skill_id"]
            },
            handler=lambda skill_id: read_skill(skill_id),
        )
        
        # Log skill availability
        try:
            skills = load_all_skills(context)
            print(f"[Worker] Found {len(skills)} skills (context-based)")
        except Exception as e:
            print(f"[Worker] Could not load skills: {e}")
    
    def _register_api_tools(self) -> None:
        """Register API and code execution tools."""
        register_api_tools(self._registry)
        register_code_execution_tool(self._registry, self._grpc)

    def _register_documentation_tools(self) -> None:
        """Register documentation query tools."""
        from storage.firestore_client import FirestoreClient
        from agents.worker.documentation_loader import (
            load_all_documentation,
            get_documentation_by_id,
            search_documentation,
        )
        
        context = self._context  # Capture for closures
        
        def get_documentation(
            doc_id: str = "",
            tags: List[str] = None,
            list_all: bool = False
        ) -> Dict[str, Any]:
            """Query documentation from storage."""
            try:
                if doc_id:
                    # Get specific document by ID
                    doc = get_documentation_by_id(doc_id, context)
                    if doc:
                        return {
                            "content": [{
                                "type": "text",
                                "text": doc.get_full_content()
                            }]
                        }
                    else:
                        return {
                            "content": [{"type": "text", "text": f"Documentation '{doc_id}' not found"}],
                            "isError": True
                        }
                
                elif tags:
                    # Search by tags
                    docs = search_documentation(tags, context)
                    if docs:
                        results = []
                        for doc in docs:
                            results.append(f"**{doc.metadata.title}** (ID: {doc.metadata.doc_id})")
                            results.append(f"  {doc.metadata.description}")
                            results.append(f"  Tags: {', '.join(doc.metadata.tags)}")
                            results.append("")
                        return {
                            "content": [{
                                "type": "text",
                                "text": f"Found {len(docs)} documents:\n\n" + "\n".join(results)
                            }]
                        }
                    else:
                        return {
                            "content": [{"type": "text", "text": f"No documentation found for tags: {tags}"}]
                        }
                
                else:
                    # List all documentation (summaries only)
                    docs = load_all_documentation(context)
                    if docs:
                        results = []
                        for doc in docs:
                            results.append(f"- **{doc.metadata.title}** (ID: `{doc.metadata.doc_id}`)")
                            results.append(f"  Type: {doc.metadata.doc_type}")
                            if doc.metadata.description:
                                results.append(f"  {doc.metadata.description}")
                            if doc.metadata.tags:
                                results.append(f"  Tags: {', '.join(doc.metadata.tags)}")
                            results.append("")
                        return {
                            "content": [{
                                "type": "text",
                                "text": f"## Available Documentation ({len(docs)} documents)\n\n" + "\n".join(results) + "\n\nUse get_documentation with doc_id to read full content."
                            }]
                        }
                    else:
                        return {
                            "content": [{"type": "text", "text": "No documentation available for this application."}]
                        }
                        
            except Exception as e:
                return {
                    "content": [{"type": "text", "text": f"Error querying documentation: {str(e)}"}],
                    "isError": True
                }
        
        self._registry.register_tool(
            name="get_documentation",
            description="""Query documentation about the application. Use this FIRST to understand the software before taking actions.

Usage:
- List all docs: get_documentation() with no arguments
- Get specific doc: get_documentation(doc_id="doc-id-here")
- Search by tags: get_documentation(tags=["navigation", "login"])

Always start by listing available documentation to understand what guidance exists.""",
            input_schema={
                "type": "object",
                "properties": {
                    "doc_id": {
                        "type": "string",
                        "description": "Specific document ID to retrieve (get full content)"
                    },
                    "tags": {
                        "type": "array",
                        "items": {"type": "string"},
                        "description": "Tags to search for (returns matching documents)"
                    }
                }
            },
            handler=lambda doc_id="", tags=None: get_documentation(doc_id, tags or []),
        )
        
        # Log documentation availability
        try:
            docs = load_all_documentation(context)
            print(f"[Worker] Found {len(docs)} documentation entries")
        except Exception:
            print("[Worker] Could not load documentation")

    def execute(
        self,
        task: str,
        app_name: Optional[str] = None,
        additional_context: str = "",
    ) -> AgentResult:
        """
        Execute a task using pure ReAct.
        
        Args:
            task: The task to execute
            app_name: Optional application name
            additional_context: Optional context
            
        Returns:
            AgentResult with execution outcome
        """
        task_prompt = get_worker_task(
            task=task,
            app_name=app_name or "",
            additional_context=additional_context,
        )
        
        context = {
            "task": task,
            "app_name": app_name,
            "user_id": self._context.user_id,
        }
        
        agent = AutonomousAgent(
            tool_registry=self._registry,
            system_prompt=WORKER_SYSTEM_PROMPT,
            config=self._config,
        )
        
        print(f"[Worker] Executing task: {task[:80]}...")
        print(f"[Worker] Using pure ReAct pattern")
        
        result = agent.run_with_recovery(task_prompt, context, max_retries=1)
        
        print(f"[Worker] Completed: {result.status}")
        return result


def build_autonomous_worker(
    context: CascadeContext,
    grpc_client: CascadeGrpcClient,
) -> AutonomousWorker:
    """Factory function to build worker."""
    return AutonomousWorker(context, grpc_client)

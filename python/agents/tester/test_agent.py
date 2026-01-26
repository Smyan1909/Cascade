"""Test Agent: Executes a skill and reports success/failure."""

from __future__ import annotations

import re
from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional

from cascade_client.auth.context import CascadeContext
from cascade_client.grpc_client import CascadeGrpcClient

from mcp_server.tool_registry import ToolRegistry
from mcp_server.body_tools import register_body_tools
from mcp_server.playwright_tools import register_playwright_tools

from agents.core.autonomous_agent import (
    AgentConfig, AgentResult, AgentStatus, AutonomousAgent
)
from agents.explorer.skill_map import SkillMap
from agents.worker.skill_context import format_skill_as_context

from .prompts import TESTER_SYSTEM_PROMPT, get_test_task


@dataclass
class TestResult:
    """Result of skill test execution."""
    success: bool
    reasoning: str
    skill_id: str
    iterations: int = 0
    elapsed_seconds: float = 0.0
    tool_calls: List[Dict[str, Any]] = field(default_factory=list)
    error: Optional[str] = None


class TestAgent:
    """
    Test Agent that executes a single skill and reports success/failure.
    
    Uses the same AutonomousAgent pattern as Explorer and Worker,
    but with a focused prompt for skill testing.
    """

    def __init__(
        self,
        context: CascadeContext,
        grpc_client: CascadeGrpcClient,
        max_iterations: int = 50,
        verbose: bool = True,
    ):
        self._context = context
        self._grpc = grpc_client
        self._max_iterations = max_iterations
        self._verbose = verbose
        
        # Setup tool registry with UI interaction tools
        self._registry = ToolRegistry()
        router = register_body_tools(self._registry, grpc_client)
        register_playwright_tools(self._registry, router)
        
        # Add explorer tools for observation (get_semantic_tree, get_screenshot)
        from mcp_server.explorer_tools import register_explorer_tools
        register_explorer_tools(self._registry)

    def _log(self, msg: str) -> None:
        if self._verbose:
            print(f"[Tester] {msg}")

    def test_skill(
        self,
        skill: SkillMap,
        app_name: str = "",
    ) -> TestResult:
        """
        Execute a skill and report success/failure.
        
        Args:
            skill: The SkillMap to test
            app_name: Optional application name
            
        Returns:
            TestResult with success status and reasoning
        """
        skill_id = skill.metadata.skill_id
        self._log(f"Testing skill: {skill_id}")
        
        # Format skill as context for the agent
        skill_formatted = format_skill_as_context(skill)
        
        # Check if initial state is required
        if skill.metadata.requires_initial_state and skill.metadata.initial_state_description:
            skill_formatted += f"\n\n**IMPORTANT**: This skill requires the app to be in this state: {skill.metadata.initial_state_description}"
        
        # Build the task prompt
        task = get_test_task(skill_formatted, app_name)
        
        # Create the agent
        config = AgentConfig(
            max_iterations=self._max_iterations,
            verbose=self._verbose,
            thread_id=f"test_{skill_id}",
        )
        
        agent = AutonomousAgent(
            tool_registry=self._registry,
            system_prompt=TESTER_SYSTEM_PROMPT,
            config=config,
        )
        
        # Run the test
        self._log(f"Executing skill steps...")
        result = agent.run(task)
        
        # Parse the result
        success, reasoning = self._parse_result(result.final_response)
        
        self._log(f"Result: {'SUCCESS' if success else 'FAILURE'}")
        self._log(f"Reasoning: {reasoning}")
        
        return TestResult(
            success=success,
            reasoning=reasoning,
            skill_id=skill_id,
            iterations=result.iterations,
            elapsed_seconds=result.elapsed_seconds,
            tool_calls=result.tool_calls,
            error=result.error,
        )

    def _parse_result(self, response: str) -> tuple[bool, str]:
        """Parse the agent's response to extract success/failure and reasoning."""
        if not response:
            return False, "No response from agent"
        
        response_upper = response.upper()
        
        # Look for SUCCESS: or FAILURE: patterns
        success_match = re.search(r'SUCCESS\s*[:\-]?\s*(.+?)(?:\n|$)', response, re.IGNORECASE | re.DOTALL)
        failure_match = re.search(r'FAILURE\s*[:\-]?\s*(.+?)(?:\n|$)', response, re.IGNORECASE | re.DOTALL)
        
        if success_match and not failure_match:
            return True, success_match.group(1).strip()
        elif failure_match:
            return False, failure_match.group(1).strip()
        elif "SUCCESS" in response_upper:
            return True, "Skill executed successfully"
        elif "FAILURE" in response_upper or "FAILED" in response_upper:
            return False, "Skill execution failed"
        else:
            # Cannot determine - treat as failure
            return False, f"Could not determine result from response: {response[:200]}"


def build_test_agent(
    context: CascadeContext,
    grpc_client: CascadeGrpcClient,
) -> TestAgent:
    """Factory function to build test agent."""
    return TestAgent(context, grpc_client)

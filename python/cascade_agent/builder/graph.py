"""
Builder Agent LangGraph definition.
"""

from typing import Annotated, Optional, TypedDict

from langgraph.graph import END, StateGraph
from langgraph.graph.message import add_messages


class BuilderState(TypedDict):
    """State for the Builder Agent."""
    # Input from Explorer
    application_model: dict
    exploration_results: dict
    original_instructions: str
    
    # Messages
    messages: Annotated[list, add_messages]
    
    # Design phase
    agent_spec: Optional[dict]
    capabilities: list[dict]
    
    # Code generation
    generated_actions: list[dict]
    generated_workflows: list[dict]
    agent_code: Optional[str]
    
    # Instruction generation
    agent_instructions: Optional[str]
    example_prompts: list[str]
    
    # Validation
    validation_results: list[dict]
    all_tests_passed: bool
    
    # Output
    final_agent: Optional[dict]
    
    # Progress
    current_phase: str
    errors: list[str]


def create_builder_graph() -> StateGraph:
    """
    Create the Builder Agent graph.
    
    The Builder Agent workflow:
    1. Analyze exploration results
    2. Design agent specification
    3. Define capabilities
    4. Generate action code
    5. Generate workflow code
    6. Generate agent Python code
    7. Generate instructions
    8. Validate agent
    9. Package agent
    
    Returns:
        Compiled StateGraph
    """
    workflow = StateGraph(BuilderState)
    
    # Add nodes
    workflow.add_node("analyze_input", _analyze_input_node)
    workflow.add_node("design_agent", _design_agent_node)
    workflow.add_node("define_capabilities", _define_capabilities_node)
    workflow.add_node("generate_actions", _generate_actions_node)
    workflow.add_node("generate_workflows", _generate_workflows_node)
    workflow.add_node("generate_agent_code", _generate_agent_code_node)
    workflow.add_node("generate_instructions", _generate_instructions_node)
    workflow.add_node("validate_agent", _validate_agent_node)
    workflow.add_node("fix_issues", _fix_issues_node)
    workflow.add_node("package_agent", _package_agent_node)
    
    # Set entry point
    workflow.set_entry_point("analyze_input")
    
    # Linear flow through design
    workflow.add_edge("analyze_input", "design_agent")
    workflow.add_edge("design_agent", "define_capabilities")
    workflow.add_edge("define_capabilities", "generate_actions")
    workflow.add_edge("generate_actions", "generate_workflows")
    workflow.add_edge("generate_workflows", "generate_agent_code")
    workflow.add_edge("generate_agent_code", "generate_instructions")
    workflow.add_edge("generate_instructions", "validate_agent")
    
    # Validation loop
    workflow.add_conditional_edges(
        "validate_agent",
        _check_validation_result,
        {
            "passed": "package_agent",
            "failed": "fix_issues",
            "abort": END
        }
    )
    
    workflow.add_conditional_edges(
        "fix_issues",
        _check_fix_result,
        {
            "retry_validation": "validate_agent",
            "regenerate": "generate_actions",
            "abort": END
        }
    )
    
    workflow.add_edge("package_agent", END)
    
    return workflow.compile()


# Placeholder node implementations
async def _analyze_input_node(state: BuilderState) -> dict:
    """Analyze exploration results."""
    return {"current_phase": "analysis_complete"}


async def _design_agent_node(state: BuilderState) -> dict:
    """Design agent specification."""
    return {"current_phase": "design_complete"}


async def _define_capabilities_node(state: BuilderState) -> dict:
    """Define agent capabilities."""
    return {"capabilities": [], "current_phase": "capabilities_defined"}


async def _generate_actions_node(state: BuilderState) -> dict:
    """Generate C# action code."""
    return {"generated_actions": [], "current_phase": "actions_generated"}


async def _generate_workflows_node(state: BuilderState) -> dict:
    """Generate workflow code."""
    return {"generated_workflows": [], "current_phase": "workflows_generated"}


async def _generate_agent_code_node(state: BuilderState) -> dict:
    """Generate Python agent code."""
    return {"agent_code": "", "current_phase": "agent_code_generated"}


async def _generate_instructions_node(state: BuilderState) -> dict:
    """Generate agent instructions."""
    return {"agent_instructions": "", "example_prompts": []}


async def _validate_agent_node(state: BuilderState) -> dict:
    """Validate generated agent."""
    return {"validation_results": [], "all_tests_passed": True}


async def _fix_issues_node(state: BuilderState) -> dict:
    """Fix validation issues."""
    return {"current_phase": "fixes_applied"}


async def _package_agent_node(state: BuilderState) -> dict:
    """Package agent for deployment."""
    return {"final_agent": {}, "current_phase": "complete"}


def _check_validation_result(state: BuilderState) -> str:
    """Check validation result."""
    if state.get("all_tests_passed"):
        return "passed"
    if len(state.get("errors", [])) > 5:
        return "abort"
    return "failed"


def _check_fix_result(state: BuilderState) -> str:
    """Check fix result."""
    errors = state.get("errors", [])
    if len(errors) > 3:
        return "abort"
    if any("critical" in e.lower() for e in errors):
        return "regenerate"
    return "retry_validation"



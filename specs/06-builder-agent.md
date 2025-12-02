# Builder Agent Specification

## Overview

The Builder Agent takes the output from the Explorer Agent (application model and knowledge) and generates a complete, specialized agent for the target application. This includes generating automation code, creating an instruction list, and packaging everything into a deployable agent.

## Architecture

```
python/cascade_agent/builder/
├── __init__.py
├── graph.py                    # Main LangGraph definition
├── state.py                    # Agent state definition
├── nodes/
│   ├── __init__.py
│   ├── analysis.py             # Input analysis nodes
│   ├── design.py               # Agent design nodes
│   ├── code_generation.py      # Code synthesis nodes
│   ├── instruction_gen.py      # Instruction generation
│   ├── validation.py           # Testing and validation
│   └── packaging.py            # Final packaging
├── templates/
│   ├── __init__.py
│   ├── agent_template.py       # Python agent templates
│   ├── action_template.py      # C# action templates
│   └── workflow_template.py    # Workflow templates
├── prompts/
│   ├── __init__.py
│   ├── design_prompts.py
│   ├── codegen_prompts.py
│   └── instruction_prompts.py
└── models/
    ├── __init__.py
    ├── agent_spec.py
    └── generated_code.py
```

## Agent State

```python
from typing import TypedDict, Annotated, Optional
from langgraph.graph.message import add_messages

class BuilderState(TypedDict):
    # Input from Explorer
    application_model: ApplicationModel
    exploration_results: ExplorationResults
    original_instructions: str
    
    # Messages
    messages: Annotated[list, add_messages]
    
    # Design phase
    agent_spec: Optional[AgentSpecification]
    capabilities: list[Capability]
    
    # Code generation
    generated_actions: list[GeneratedAction]
    generated_workflows: list[GeneratedWorkflow]
    agent_code: Optional[str]
    
    # Instruction generation
    agent_instructions: Optional[str]
    example_prompts: list[str]
    
    # Validation
    validation_results: list[ValidationResult]
    all_tests_passed: bool
    
    # Output
    final_agent: Optional[BuiltAgent]
    
    # Progress
    current_phase: str
    errors: list[str]


class AgentSpecification(TypedDict):
    name: str
    description: str
    target_application: str
    version: str
    capabilities: list[str]
    required_inputs: list[dict]
    expected_outputs: list[dict]
    limitations: list[str]


class Capability(TypedDict):
    id: str
    name: str
    description: str
    action_sequence: list[str]
    required_elements: list[str]
    parameters: list[dict]
    return_type: str


class GeneratedAction(TypedDict):
    id: str
    name: str
    description: str
    csharp_code: str
    python_wrapper: str
    element_locators: list[str]
    parameters: list[dict]
    return_type: str


class GeneratedWorkflow(TypedDict):
    id: str
    name: str
    description: str
    steps: list[dict]
    csharp_code: str
    python_wrapper: str
    error_handling: str


class ValidationResult(TypedDict):
    test_name: str
    passed: bool
    error_message: Optional[str]
    execution_time_ms: int
    screenshots: list[str]


class BuiltAgent(TypedDict):
    spec: AgentSpecification
    actions: list[GeneratedAction]
    workflows: list[GeneratedWorkflow]
    instructions: str
    example_prompts: list[str]
    python_agent_code: str
    csharp_scripts: list[dict]
    metadata: dict
```

## LangGraph Definition

```python
from langgraph.graph import StateGraph, END

def create_builder_graph() -> StateGraph:
    """Create the Builder Agent graph."""
    
    workflow = StateGraph(BuilderState)
    
    # Add nodes
    workflow.add_node("analyze_input", analyze_input_node)
    workflow.add_node("design_agent", design_agent_node)
    workflow.add_node("define_capabilities", define_capabilities_node)
    workflow.add_node("generate_actions", generate_actions_node)
    workflow.add_node("generate_workflows", generate_workflows_node)
    workflow.add_node("generate_agent_code", generate_agent_code_node)
    workflow.add_node("generate_instructions", generate_instructions_node)
    workflow.add_node("validate_agent", validate_agent_node)
    workflow.add_node("fix_issues", fix_issues_node)
    workflow.add_node("package_agent", package_agent_node)
    
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
        check_validation_result,
        {
            "passed": "package_agent",
            "failed": "fix_issues",
            "abort": END
        }
    )
    
    workflow.add_conditional_edges(
        "fix_issues",
        check_fix_result,
        {
            "retry_validation": "validate_agent",
            "regenerate": "generate_actions",
            "abort": END
        }
    )
    
    workflow.add_edge("package_agent", END)
    
    return workflow.compile()
```

## Node Implementations

### Analysis Node

```python
async def analyze_input_node(state: BuilderState) -> dict:
    """Analyze the input from Explorer Agent."""
    
    app_model = state["application_model"]
    exploration = state["exploration_results"]
    instructions = state["original_instructions"]
    
    # Analyze coverage
    coverage_analysis = analyze_coverage(app_model, exploration, instructions)
    
    # Identify gaps
    gaps = identify_exploration_gaps(coverage_analysis)
    
    # Determine what can be automated
    automatable = determine_automatable_features(app_model, instructions)
    
    prompt = ANALYZE_INPUT_PROMPT.format(
        app_model=app_model,
        coverage=coverage_analysis,
        gaps=gaps,
        automatable=automatable
    )
    
    response = await llm.ainvoke(prompt)
    analysis = parse_analysis(response)
    
    return {
        "current_phase": "analysis_complete",
        "messages": [AIMessage(content=f"Analysis complete. Found {len(automatable)} automatable features.")]
    }
```

### Design Node

```python
async def design_agent_node(state: BuilderState) -> dict:
    """Design the agent specification."""
    
    prompt = DESIGN_AGENT_PROMPT.format(
        application_name=state["application_model"]["name"],
        features=state["application_model"]["windows"],
        instructions=state["original_instructions"]
    )
    
    response = await llm.ainvoke(prompt)
    spec = parse_agent_spec(response)
    
    # Validate spec
    if not validate_spec(spec):
        raise ValueError("Invalid agent specification")
    
    return {
        "agent_spec": spec,
        "current_phase": "design_complete",
        "messages": [AIMessage(content=f"Designed agent: {spec['name']}")]
    }


async def define_capabilities_node(state: BuilderState) -> dict:
    """Define detailed capabilities based on the design."""
    
    spec = state["agent_spec"]
    app_model = state["application_model"]
    
    capabilities = []
    
    for cap_name in spec["capabilities"]:
        prompt = DEFINE_CAPABILITY_PROMPT.format(
            capability_name=cap_name,
            app_model=app_model,
            spec=spec
        )
        
        response = await llm.ainvoke(prompt)
        capability = parse_capability(response)
        
        # Map to actual elements
        capability["required_elements"] = map_to_elements(
            capability["required_elements"], 
            app_model
        )
        
        capabilities.append(capability)
    
    return {
        "capabilities": capabilities,
        "current_phase": "capabilities_defined",
        "messages": [AIMessage(content=f"Defined {len(capabilities)} capabilities")]
    }
```

### Code Generation Nodes

```python
async def generate_actions_node(state: BuilderState) -> dict:
    """Generate C# action code and Python wrappers."""
    
    capabilities = state["capabilities"]
    app_model = state["application_model"]
    
    generated_actions = []
    
    for capability in capabilities:
        for action_name in capability["action_sequence"]:
            # Generate C# code
            csharp_prompt = GENERATE_CSHARP_ACTION_PROMPT.format(
                action_name=action_name,
                capability=capability,
                elements=get_elements_for_action(app_model, action_name)
            )
            
            csharp_response = await llm.ainvoke(csharp_prompt)
            csharp_code = extract_code(csharp_response, "csharp")
            
            # Validate C# syntax
            syntax_result = await codegen_client.check_syntax(csharp_code)
            if not syntax_result["is_valid"]:
                csharp_code = await fix_syntax_errors(csharp_code, syntax_result)
            
            # Generate Python wrapper
            python_prompt = GENERATE_PYTHON_WRAPPER_PROMPT.format(
                action_name=action_name,
                csharp_code=csharp_code
            )
            
            python_response = await llm.ainvoke(python_prompt)
            python_code = extract_code(python_response, "python")
            
            generated_actions.append(GeneratedAction(
                id=f"action_{action_name}",
                name=action_name,
                description=f"Execute {action_name}",
                csharp_code=csharp_code,
                python_wrapper=python_code,
                element_locators=extract_locators(csharp_code),
                parameters=extract_parameters(csharp_code),
                return_type=extract_return_type(csharp_code)
            ))
    
    return {
        "generated_actions": generated_actions,
        "current_phase": "actions_generated",
        "messages": [AIMessage(content=f"Generated {len(generated_actions)} actions")]
    }


async def generate_workflows_node(state: BuilderState) -> dict:
    """Generate workflow code that combines multiple actions."""
    
    capabilities = state["capabilities"]
    actions = state["generated_actions"]
    
    workflows = []
    
    for capability in capabilities:
        if len(capability["action_sequence"]) > 1:
            prompt = GENERATE_WORKFLOW_PROMPT.format(
                capability=capability,
                available_actions=actions
            )
            
            response = await llm.ainvoke(prompt)
            workflow_code = extract_workflow(response)
            
            workflows.append(GeneratedWorkflow(
                id=f"workflow_{capability['id']}",
                name=capability["name"],
                description=capability["description"],
                steps=capability["action_sequence"],
                csharp_code=workflow_code["csharp"],
                python_wrapper=workflow_code["python"],
                error_handling=workflow_code.get("error_handling", "")
            ))
    
    return {
        "generated_workflows": workflows,
        "current_phase": "workflows_generated",
        "messages": [AIMessage(content=f"Generated {len(workflows)} workflows")]
    }


async def generate_agent_code_node(state: BuilderState) -> dict:
    """Generate the complete Python agent class."""
    
    spec = state["agent_spec"]
    actions = state["generated_actions"]
    workflows = state["generated_workflows"]
    capabilities = state["capabilities"]
    
    agent_code = AGENT_TEMPLATE.format(
        agent_name=spec["name"],
        description=spec["description"],
        target_application=spec["target_application"],
        capabilities=[c["name"] for c in capabilities],
        action_methods=generate_action_methods(actions),
        workflow_methods=generate_workflow_methods(workflows),
        tool_definitions=generate_tool_definitions(actions, workflows),
        graph_definition=generate_langgraph_definition(capabilities)
    )
    
    return {
        "agent_code": agent_code,
        "current_phase": "agent_code_generated",
        "messages": [AIMessage(content="Generated Python agent code")]
    }
```

### Instruction Generation

```python
async def generate_instructions_node(state: BuilderState) -> dict:
    """Generate natural language instructions for using the agent."""
    
    spec = state["agent_spec"]
    capabilities = state["capabilities"]
    
    prompt = GENERATE_INSTRUCTIONS_PROMPT.format(
        agent_name=spec["name"],
        target_application=spec["target_application"],
        capabilities=capabilities,
        original_instructions=state["original_instructions"]
    )
    
    response = await llm.ainvoke(prompt)
    instructions = extract_instructions(response)
    
    # Generate example prompts
    example_prompts = []
    for capability in capabilities:
        example_prompt = GENERATE_EXAMPLE_PROMPT.format(
            capability=capability
        )
        example_response = await llm.ainvoke(example_prompt)
        example_prompts.extend(extract_examples(example_response))
    
    return {
        "agent_instructions": instructions,
        "example_prompts": example_prompts,
        "current_phase": "instructions_generated",
        "messages": [AIMessage(content="Generated agent instructions")]
    }
```

### Validation Node

```python
async def validate_agent_node(state: BuilderState) -> dict:
    """Validate the generated agent through testing."""
    
    actions = state["generated_actions"]
    workflows = state["generated_workflows"]
    
    validation_results = []
    
    # Validate C# compilation
    for action in actions:
        compile_result = await codegen_client.compile(action["csharp_code"])
        validation_results.append(ValidationResult(
            test_name=f"compile_{action['name']}",
            passed=compile_result["compilation_success"],
            error_message=compile_result.get("errors", [{}])[0].get("message"),
            execution_time_ms=compile_result["compilation_time_ms"],
            screenshots=[]
        ))
    
    # Validate workflows
    for workflow in workflows:
        compile_result = await codegen_client.compile(workflow["csharp_code"])
        validation_results.append(ValidationResult(
            test_name=f"compile_{workflow['name']}",
            passed=compile_result["compilation_success"],
            error_message=compile_result.get("errors", [{}])[0].get("message"),
            execution_time_ms=compile_result["compilation_time_ms"],
            screenshots=[]
        ))
    
    # Run integration tests if possible
    if is_application_available(state["agent_spec"]["target_application"]):
        integration_results = await run_integration_tests(
            state["agent_spec"],
            actions,
            workflows
        )
        validation_results.extend(integration_results)
    
    all_passed = all(r["passed"] for r in validation_results)
    
    return {
        "validation_results": validation_results,
        "all_tests_passed": all_passed,
        "current_phase": "validation_complete",
        "messages": [AIMessage(content=f"Validation: {sum(r['passed'] for r in validation_results)}/{len(validation_results)} passed")]
    }


async def fix_issues_node(state: BuilderState) -> dict:
    """Attempt to fix validation issues."""
    
    failed_tests = [r for r in state["validation_results"] if not r["passed"]]
    
    fixes_applied = []
    
    for test in failed_tests:
        if test["test_name"].startswith("compile_"):
            # Try to fix compilation errors
            action_name = test["test_name"].replace("compile_", "")
            action = next((a for a in state["generated_actions"] if a["name"] == action_name), None)
            
            if action:
                prompt = FIX_COMPILATION_ERROR_PROMPT.format(
                    code=action["csharp_code"],
                    error=test["error_message"]
                )
                
                response = await llm.ainvoke(prompt)
                fixed_code = extract_code(response, "csharp")
                
                action["csharp_code"] = fixed_code
                fixes_applied.append(action_name)
    
    return {
        "current_phase": "fixes_applied",
        "messages": [AIMessage(content=f"Applied {len(fixes_applied)} fixes")]
    }
```

### Packaging Node

```python
async def package_agent_node(state: BuilderState) -> dict:
    """Package the agent for deployment."""
    
    spec = state["agent_spec"]
    actions = state["generated_actions"]
    workflows = state["generated_workflows"]
    
    # Save to database
    agent_record = await agent_client.create_agent(
        name=spec["name"],
        description=spec["description"],
        target_application=spec["target_application"],
        capabilities=spec["capabilities"],
        instruction_list=state["agent_instructions"]
    )
    
    # Save scripts
    script_ids = []
    for action in actions:
        script = await codegen_client.save_script(
            name=action["name"],
            description=action["description"],
            source_code=action["csharp_code"],
            script_type="action"
        )
        script_ids.append(script["id"])
    
    for workflow in workflows:
        script = await codegen_client.save_script(
            name=workflow["name"],
            description=workflow["description"],
            source_code=workflow["csharp_code"],
            script_type="workflow"
        )
        script_ids.append(script["id"])
    
    # Update agent with script references
    await agent_client.update_agent(
        agent_record["id"],
        script_ids=script_ids
    )
    
    final_agent = BuiltAgent(
        spec=spec,
        actions=actions,
        workflows=workflows,
        instructions=state["agent_instructions"],
        example_prompts=state["example_prompts"],
        python_agent_code=state["agent_code"],
        csharp_scripts=[{"id": s["id"], "code": s["csharp_code"]} for s in actions + workflows],
        metadata={
            "agent_id": agent_record["id"],
            "version": agent_record["active_version"],
            "created_at": agent_record["created_at"]
        }
    )
    
    return {
        "final_agent": final_agent,
        "current_phase": "complete",
        "messages": [AIMessage(content=f"Agent '{spec['name']}' packaged and saved")]
    }
```

## Templates

### Agent Template

```python
AGENT_TEMPLATE = '''
"""
{agent_name} - Auto-generated Agent for {target_application}

{description}

Generated by Cascade Builder Agent
"""

from typing import TypedDict, Annotated, Optional
from langgraph.graph import StateGraph, END
from langgraph.prebuilt import ToolNode
from langchain_core.tools import tool

from cascade_agent.grpc_client import CascadeClient
from cascade_agent.llm import get_llm


class {agent_name}State(TypedDict):
    """State for {agent_name}"""
    messages: Annotated[list, add_messages]
    current_task: Optional[str]
    task_plan: Optional[list[str]]
    execution_step: int
    last_action_result: Optional[dict]
    error: Optional[str]
    completed: bool


# Initialize clients
cascade_client = CascadeClient()
llm = get_llm()


# Action methods
{action_methods}


# Workflow methods
{workflow_methods}


# Tool definitions
{tool_definitions}


# Graph definition
def create_{agent_name.lower()}_graph():
    workflow = StateGraph({agent_name}State)
    
    workflow.add_node("plan", plan_node)
    workflow.add_node("execute", execute_node)
    workflow.add_node("evaluate", evaluate_node)
    workflow.add_node("tools", ToolNode(tools=get_tools()))
    
    workflow.set_entry_point("plan")
    
    workflow.add_edge("plan", "execute")
    workflow.add_conditional_edges(
        "execute",
        check_execution,
        {{"continue": "execute", "evaluate": "evaluate", "error": END}}
    )
    workflow.add_conditional_edges(
        "evaluate",
        check_completion,
        {{"complete": END, "retry": "plan"}}
    )
    
    return workflow.compile()


# Entry point
agent = create_{agent_name.lower()}_graph()


async def run(task: str) -> dict:
    """Run the agent with a task."""
    initial_state = {{
        "messages": [],
        "current_task": task,
        "task_plan": None,
        "execution_step": 0,
        "last_action_result": None,
        "error": None,
        "completed": False
    }}
    
    result = await agent.ainvoke(initial_state)
    return result
'''
```

## Prompts

```python
DESIGN_AGENT_PROMPT = """
Design a specialized agent for automating tasks in the following application.

Application: {application_name}

Available Features/Windows:
{features}

Original Instructions:
{instructions}

Create an agent specification that includes:
1. A descriptive name for the agent
2. A clear description of what it can do
3. List of capabilities (high-level tasks it can perform)
4. Required inputs from users
5. Expected outputs
6. Known limitations

Output as JSON.
"""

GENERATE_CSHARP_ACTION_PROMPT = """
Generate C# code for the following UI automation action.

Action: {action_name}

Capability Context:
{capability}

Available UI Elements:
{elements}

Requirements:
1. Use Cascade.UIAutomation interfaces
2. Include proper error handling
3. Use async/await patterns
4. Add XML documentation
5. Return meaningful results

Generate complete, compilable C# code.
"""

GENERATE_INSTRUCTIONS_PROMPT = """
Create user instructions for an AI agent.

Agent: {agent_name}
Target Application: {target_application}

Capabilities:
{capabilities}

Original Application Instructions:
{original_instructions}

Write clear, concise instructions that:
1. Explain what the agent can do
2. Show how to phrase requests
3. List any limitations
4. Include troubleshooting tips

The instructions should be suitable for end users who want to use this agent.
"""
```

## Output Schema

The Builder Agent produces a complete agent package:

```json
{
  "spec": {
    "name": "NotePadAgent",
    "description": "Agent for automating Notepad tasks",
    "target_application": "Notepad",
    "version": "1.0.0",
    "capabilities": ["create_document", "edit_text", "save_file", "search_replace"]
  },
  "actions": [...],
  "workflows": [...],
  "instructions": "# NotePad Agent Instructions\n...",
  "example_prompts": [
    "Create a new document with the text 'Hello World'",
    "Find and replace all occurrences of 'foo' with 'bar'"
  ],
  "python_agent_code": "...",
  "csharp_scripts": [...],
  "metadata": {
    "agent_id": "uuid",
    "version": "1.0.0",
    "created_at": "2024-01-01T00:00:00Z"
  }
}
```



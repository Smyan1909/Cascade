# Explorer Agent Specification

## Overview

The Explorer Agent is a LangGraph-based agent responsible for understanding Windows applications through systematic UI exploration. It takes an application and instruction manual as input, then discovers, tests, and documents all UI elements and their interactions.

## Architecture

```
python/cascade_agent/explorer/
├── __init__.py
├── graph.py                    # Main LangGraph definition
├── state.py                    # Agent state definition
├── nodes/
│   ├── __init__.py
│   ├── planning.py             # Planning nodes
│   ├── exploration.py          # UI exploration nodes
│   ├── testing.py              # Action testing nodes
│   ├── learning.py             # Knowledge synthesis nodes
│   └── evaluation.py           # Success evaluation nodes
├── tools/
│   ├── __init__.py
│   ├── ui_tools.py             # UI Automation tools
│   ├── vision_tools.py         # Screenshot/OCR tools
│   └── navigation_tools.py     # Navigation helpers
├── prompts/
│   ├── __init__.py
│   ├── planning_prompts.py
│   ├── exploration_prompts.py
│   └── synthesis_prompts.py
└── models/
    ├── __init__.py
    ├── exploration_state.py
    ├── ui_knowledge.py
    └── action_result.py
```

## Agent State

```python
from typing import TypedDict, Annotated, Optional
from langgraph.graph.message import add_messages

class ExplorerState(TypedDict):
    # Input
    target_application: str
    instruction_manual: str
    exploration_goals: list[str]
    
    # Messages
    messages: Annotated[list, add_messages]
    
    # Planning
    current_plan: Optional[ExplorationPlan]
    current_step: int
    
    # Knowledge accumulation
    discovered_windows: list[WindowInfo]
    discovered_elements: list[ElementInfo]
    tested_actions: list[ActionTestResult]
    navigation_paths: list[NavigationPath]
    
    # Application model
    application_model: Optional[ApplicationModel]
    
    # Progress tracking
    exploration_progress: float  # 0.0 - 1.0
    completed_goals: list[str]
    pending_goals: list[str]
    
    # Error handling
    errors: list[ExplorationError]
    retry_count: int
    
    # Termination
    should_terminate: bool
    termination_reason: Optional[str]


class ExplorationPlan(TypedDict):
    goals: list[ExplorationGoal]
    estimated_steps: int
    priority_order: list[str]  # Goal IDs in priority order


class ExplorationGoal(TypedDict):
    id: str
    description: str
    target_elements: list[str]  # Element types or names to find
    required_actions: list[str]  # Actions to verify
    success_criteria: str
    dependencies: list[str]  # Other goal IDs


class WindowInfo(TypedDict):
    title: str
    class_name: str
    automation_id: str
    process_name: str
    bounds: dict
    is_main_window: bool
    child_elements: list[str]  # RuntimeIds


class ElementInfo(TypedDict):
    runtime_id: str
    automation_id: str
    name: str
    control_type: str
    class_name: str
    bounds: dict
    parent_id: str
    supported_patterns: list[str]
    is_clickable: bool
    is_editable: bool
    possible_actions: list[str]
    ocr_text: Optional[str]


class ActionTestResult(TypedDict):
    element_id: str
    action_type: str
    parameters: dict
    success: bool
    before_screenshot: str  # Base64
    after_screenshot: str
    changes_detected: list[str]
    error_message: Optional[str]


class NavigationPath(TypedDict):
    from_state: str  # State identifier
    to_state: str
    actions: list[dict]  # Sequence of actions
    verified: bool
```

## LangGraph Definition

```python
from langgraph.graph import StateGraph, END
from langgraph.prebuilt import ToolNode

def create_explorer_graph() -> StateGraph:
    """Create the Explorer Agent graph."""
    
    workflow = StateGraph(ExplorerState)
    
    # Add nodes
    workflow.add_node("parse_instructions", parse_instructions_node)
    workflow.add_node("create_plan", create_plan_node)
    workflow.add_node("select_next_goal", select_next_goal_node)
    workflow.add_node("explore_ui", explore_ui_node)
    workflow.add_node("test_actions", test_actions_node)
    workflow.add_node("record_findings", record_findings_node)
    workflow.add_node("evaluate_progress", evaluate_progress_node)
    workflow.add_node("synthesize_knowledge", synthesize_knowledge_node)
    workflow.add_node("handle_error", handle_error_node)
    workflow.add_node("tools", ToolNode(tools=get_explorer_tools()))
    
    # Set entry point
    workflow.set_entry_point("parse_instructions")
    
    # Add edges
    workflow.add_edge("parse_instructions", "create_plan")
    workflow.add_edge("create_plan", "select_next_goal")
    
    # Conditional routing from goal selection
    workflow.add_conditional_edges(
        "select_next_goal",
        should_continue_exploration,
        {
            "explore": "explore_ui",
            "synthesize": "synthesize_knowledge",
            "end": END
        }
    )
    
    # Exploration loop
    workflow.add_edge("explore_ui", "test_actions")
    workflow.add_edge("test_actions", "record_findings")
    workflow.add_edge("record_findings", "evaluate_progress")
    
    # Evaluation routing
    workflow.add_conditional_edges(
        "evaluate_progress",
        evaluate_exploration_result,
        {
            "continue": "select_next_goal",
            "retry": "explore_ui",
            "error": "handle_error",
            "complete": "synthesize_knowledge"
        }
    )
    
    # Error handling
    workflow.add_conditional_edges(
        "handle_error",
        handle_error_result,
        {
            "retry": "select_next_goal",
            "abort": END
        }
    )
    
    # Final synthesis
    workflow.add_edge("synthesize_knowledge", END)
    
    return workflow.compile()
```

## Node Implementations

### Planning Nodes

```python
async def parse_instructions_node(state: ExplorerState) -> dict:
    """Parse the instruction manual and extract exploration goals."""
    
    prompt = PARSE_INSTRUCTIONS_PROMPT.format(
        instruction_manual=state["instruction_manual"],
        target_application=state["target_application"]
    )
    
    response = await llm.ainvoke(prompt)
    
    goals = extract_goals_from_response(response)
    
    return {
        "exploration_goals": goals,
        "pending_goals": [g["id"] for g in goals],
        "messages": [AIMessage(content=f"Identified {len(goals)} exploration goals")]
    }


async def create_plan_node(state: ExplorerState) -> dict:
    """Create a structured exploration plan."""
    
    prompt = CREATE_PLAN_PROMPT.format(
        goals=state["exploration_goals"],
        target_application=state["target_application"]
    )
    
    response = await llm.ainvoke(prompt)
    plan = parse_plan_from_response(response)
    
    return {
        "current_plan": plan,
        "current_step": 0,
        "messages": [AIMessage(content=f"Created plan with {plan['estimated_steps']} steps")]
    }


async def select_next_goal_node(state: ExplorerState) -> dict:
    """Select the next exploration goal based on dependencies and progress."""
    
    pending = state["pending_goals"]
    completed = state["completed_goals"]
    plan = state["current_plan"]
    
    # Find next goal with satisfied dependencies
    for goal_id in plan["priority_order"]:
        if goal_id in pending:
            goal = next(g for g in plan["goals"] if g["id"] == goal_id)
            if all(dep in completed for dep in goal["dependencies"]):
                return {
                    "current_step": state["current_step"] + 1,
                    "messages": [AIMessage(content=f"Starting goal: {goal['description']}")]
                }
    
    return {"should_terminate": True, "termination_reason": "All goals completed"}
```

### Exploration Nodes

```python
async def explore_ui_node(state: ExplorerState) -> dict:
    """Explore UI elements for the current goal."""
    
    current_goal = get_current_goal(state)
    
    # Capture current UI state
    screenshot = await vision_client.capture_foreground_window()
    tree_snapshot = await ui_client.capture_tree(max_depth=10)
    ocr_result = await vision_client.recognize_text(screenshot)
    
    # Analyze UI structure
    prompt = EXPLORE_UI_PROMPT.format(
        goal=current_goal,
        tree_snapshot=tree_snapshot,
        ocr_text=ocr_result["full_text"],
        instruction_context=get_relevant_instructions(state, current_goal)
    )
    
    response = await llm.ainvoke(prompt)
    elements_to_explore = parse_elements_from_response(response)
    
    # Discover element details
    discovered = []
    for element_ref in elements_to_explore:
        element = await ui_client.find_element(element_ref)
        if element:
            element_info = await analyze_element(element, screenshot)
            discovered.append(element_info)
    
    return {
        "discovered_elements": state["discovered_elements"] + discovered,
        "messages": [AIMessage(content=f"Discovered {len(discovered)} elements")]
    }


async def analyze_element(element: dict, screenshot: bytes) -> ElementInfo:
    """Analyze a UI element to understand its capabilities."""
    
    patterns = await ui_client.get_patterns(element["runtime_id"])
    
    # Determine possible actions
    actions = []
    if "InvokePattern" in patterns:
        actions.append("click")
    if "ValuePattern" in patterns:
        actions.append("set_value")
    if "TogglePattern" in patterns:
        actions.append("toggle")
    if "SelectionItemPattern" in patterns:
        actions.append("select")
    if "ExpandCollapsePattern" in patterns:
        actions.extend(["expand", "collapse"])
    
    # Try OCR on element region
    ocr_text = None
    try:
        region_capture = await vision_client.capture_region(element["bounding_rectangle"])
        ocr_result = await vision_client.recognize_text(region_capture)
        ocr_text = ocr_result.get("full_text")
    except Exception:
        pass
    
    return ElementInfo(
        runtime_id=element["runtime_id"],
        automation_id=element.get("automation_id", ""),
        name=element.get("name", ""),
        control_type=element["control_type"],
        class_name=element.get("class_name", ""),
        bounds=element["bounding_rectangle"],
        parent_id=element.get("parent_id", ""),
        supported_patterns=patterns,
        is_clickable="InvokePattern" in patterns or element["control_type"] == "Button",
        is_editable="ValuePattern" in patterns,
        possible_actions=actions,
        ocr_text=ocr_text
    )
```

### Testing Nodes

```python
async def test_actions_node(state: ExplorerState) -> dict:
    """Test actions on discovered elements."""
    
    current_goal = get_current_goal(state)
    elements = get_elements_for_goal(state, current_goal)
    
    test_results = []
    
    for element in elements:
        for action in element["possible_actions"]:
            # Skip if already tested
            if is_action_tested(state, element["runtime_id"], action):
                continue
            
            result = await test_single_action(element, action)
            test_results.append(result)
            
            # If action caused navigation, explore new window
            if result["success"] and result["changes_detected"]:
                new_window = await detect_new_window(state)
                if new_window:
                    await explore_new_window(state, new_window)
    
    return {
        "tested_actions": state["tested_actions"] + test_results,
        "messages": [AIMessage(content=f"Tested {len(test_results)} actions")]
    }


async def test_single_action(element: ElementInfo, action: str) -> ActionTestResult:
    """Test a single action on an element."""
    
    # Capture before state
    before_screenshot = await vision_client.capture_foreground_window()
    await vision_client.set_baseline(before_screenshot, f"before_{element['runtime_id']}")
    
    # Execute action
    try:
        if action == "click":
            await ui_client.click(element["runtime_id"])
        elif action == "set_value":
            await ui_client.set_value(element["runtime_id"], "test_value")
        elif action == "toggle":
            await ui_client.toggle(element["runtime_id"])
        elif action == "select":
            await ui_client.select(element["runtime_id"])
        elif action == "expand":
            await ui_client.expand(element["runtime_id"])
        elif action == "collapse":
            await ui_client.collapse(element["runtime_id"])
        
        success = True
        error_message = None
    except Exception as e:
        success = False
        error_message = str(e)
    
    # Wait for UI to stabilize
    await asyncio.sleep(0.5)
    
    # Capture after state
    after_screenshot = await vision_client.capture_foreground_window()
    
    # Detect changes
    change_result = await vision_client.compare_with_baseline(
        after_screenshot, 
        f"before_{element['runtime_id']}"
    )
    
    changes = []
    if change_result["has_changes"]:
        changes = await analyze_changes(before_screenshot, after_screenshot)
    
    return ActionTestResult(
        element_id=element["runtime_id"],
        action_type=action,
        parameters={},
        success=success,
        before_screenshot=base64.b64encode(before_screenshot).decode(),
        after_screenshot=base64.b64encode(after_screenshot).decode(),
        changes_detected=changes,
        error_message=error_message
    )
```

### Synthesis Nodes

```python
async def synthesize_knowledge_node(state: ExplorerState) -> dict:
    """Synthesize all discoveries into a structured application model."""
    
    prompt = SYNTHESIZE_KNOWLEDGE_PROMPT.format(
        discovered_windows=state["discovered_windows"],
        discovered_elements=state["discovered_elements"],
        tested_actions=state["tested_actions"],
        navigation_paths=state["navigation_paths"],
        instruction_manual=state["instruction_manual"]
    )
    
    response = await llm.ainvoke(prompt)
    
    application_model = ApplicationModel(
        name=state["target_application"],
        windows=[
            WindowModel(
                title=w["title"],
                elements=[
                    ElementModel(
                        id=e["automation_id"] or e["runtime_id"],
                        name=e["name"],
                        type=e["control_type"],
                        actions=e["possible_actions"],
                        locator=build_locator(e)
                    )
                    for e in state["discovered_elements"]
                    if e.get("parent_window") == w["title"]
                ]
            )
            for w in state["discovered_windows"]
        ],
        workflows=extract_workflows(state),
        navigation_graph=build_navigation_graph(state["navigation_paths"])
    )
    
    return {
        "application_model": application_model,
        "exploration_progress": 1.0,
        "should_terminate": True,
        "termination_reason": "Exploration complete",
        "messages": [AIMessage(content="Successfully built application model")]
    }
```

## Tools Definition

```python
from langchain_core.tools import tool

@tool
async def capture_screenshot() -> dict:
    """Capture a screenshot of the current foreground window."""
    result = await vision_client.capture_foreground_window()
    return {
        "image_base64": base64.b64encode(result["image_data"]).decode(),
        "width": result["width"],
        "height": result["height"]
    }


@tool
async def get_ui_tree(max_depth: int = 5) -> dict:
    """Get the UI element tree of the current window."""
    result = await ui_client.capture_tree(max_depth=max_depth)
    return result


@tool
async def find_element(
    name: str = None,
    automation_id: str = None,
    control_type: str = None
) -> dict:
    """Find a UI element by various criteria."""
    criteria = {}
    if name:
        criteria["name"] = name
    if automation_id:
        criteria["automation_id"] = automation_id
    if control_type:
        criteria["control_type"] = control_type
    
    result = await ui_client.find_element(criteria)
    return result


@tool
async def click_element(runtime_id: str) -> dict:
    """Click on a UI element."""
    result = await ui_client.click(runtime_id)
    return {"success": result["success"]}


@tool
async def type_text(runtime_id: str, text: str) -> dict:
    """Type text into a UI element."""
    result = await ui_client.type_text(runtime_id, text)
    return {"success": result["success"]}


@tool
async def get_element_value(runtime_id: str) -> dict:
    """Get the current value of a UI element."""
    result = await ui_client.get_value(runtime_id)
    return {"value": result["value"]}


@tool
async def perform_ocr() -> dict:
    """Perform OCR on the current window."""
    screenshot = await vision_client.capture_foreground_window()
    result = await vision_client.recognize_text(screenshot)
    return {
        "text": result["full_text"],
        "words": [
            {"text": w["text"], "bounds": w["bounding_box"]}
            for w in result.get("words", [])
        ]
    }


@tool
async def wait_for_element(
    name: str = None,
    automation_id: str = None,
    timeout_seconds: int = 10
) -> dict:
    """Wait for a UI element to appear."""
    criteria = {}
    if name:
        criteria["name"] = name
    if automation_id:
        criteria["automation_id"] = automation_id
    
    result = await ui_client.wait_for_element(
        criteria, 
        timeout_ms=timeout_seconds * 1000
    )
    return {"found": result["success"], "element": result.get("element")}


def get_explorer_tools():
    """Get all tools available to the Explorer Agent."""
    return [
        capture_screenshot,
        get_ui_tree,
        find_element,
        click_element,
        type_text,
        get_element_value,
        perform_ocr,
        wait_for_element
    ]
```

## Prompts

```python
PARSE_INSTRUCTIONS_PROMPT = """
You are analyzing an instruction manual for a Windows application to identify exploration goals.

Application: {target_application}

Instruction Manual:
{instruction_manual}

Based on this manual, identify the key features and functionalities that need to be explored.
For each feature, create an exploration goal with:
1. A unique ID
2. A description of what to explore
3. Target elements to find (buttons, menus, text fields, etc.)
4. Actions that should be verified (click, type, select, etc.)
5. Success criteria
6. Dependencies on other goals

Output as JSON array of goals.
"""

EXPLORE_UI_PROMPT = """
You are exploring the UI of a Windows application to understand its structure.

Current Goal: {goal}

UI Tree Snapshot:
{tree_snapshot}

OCR Text Detected:
{ocr_text}

Relevant Instructions:
{instruction_context}

Based on the goal and current UI state:
1. Identify elements relevant to the goal
2. Suggest elements to explore further
3. Note any navigation needed to reach target elements

Output the elements to explore with their locator information.
"""

SYNTHESIZE_KNOWLEDGE_PROMPT = """
You are creating a structured model of a Windows application based on exploration results.

Application: {target_application}

Discovered Windows: {discovered_windows}

Discovered Elements: {discovered_elements}

Tested Actions: {tested_actions}

Navigation Paths: {navigation_paths}

Original Instructions: {instruction_manual}

Create a comprehensive application model that includes:
1. All windows and their purposes
2. All interactive elements with their capabilities
3. Workflows for common tasks mentioned in the instructions
4. Navigation paths between different states
5. Any limitations or edge cases discovered

The model should enable an agent to perform any task described in the instructions.
"""
```

## Success Criteria

The Explorer Agent considers exploration complete when:

1. All goals from the instruction manual have been addressed
2. All discoverable UI elements have been catalogued
3. All possible actions have been tested
4. Navigation paths between main states are documented
5. The application model is complete enough to generate automation code

## Error Recovery

- **Element not found**: Retry with alternative locators, use OCR fallback
- **Action failed**: Log error, try alternative approach, continue exploration
- **Window changed unexpectedly**: Re-establish state, update navigation paths
- **Timeout**: Increase timeout, retry operation
- **Maximum retries exceeded**: Mark goal as failed, continue with other goals



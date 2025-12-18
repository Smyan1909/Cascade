"""Worker graph data structures.

NOTE: StepExecutor and build_worker_graph have been removed.
Worker now uses skills-as-context approach with base tools.

Kept:
- StepStatus: May be useful for structured step result reporting
- WorkerState: May be useful for state management in future graph implementations
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Dict, List, TypedDict

from cascade_client.auth.context import CascadeContext

# Import SkillMap for type hints only
from typing import TYPE_CHECKING
if TYPE_CHECKING:
    from agents.explorer.skill_map import SkillMap


@dataclass
class StepStatus:
    """Result of executing a single step."""
    step_index: int
    success: bool
    message: str = ""
    action: str = ""
    
    def model_dump(self) -> Dict[str, Any]:
        return {
            "step_index": self.step_index,
            "success": self.success,
            "message": self.message,
            "action": self.action,
        }


class WorkerState(TypedDict, total=False):
    """State for worker execution tracking."""
    context: CascadeContext
    run_id: str
    task: str
    available_skills: List[Any]  # List of SkillMap
    execution_plan: List[Any]
    current_skill_index: int
    execution_history: List[Dict[str, Any]]
    pending_events: List[Any]
    dry_run: bool
    metadata: Dict[str, Any]
    replan_count: int
    max_replans: int
    failed: bool

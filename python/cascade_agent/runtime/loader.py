"""
Agent loader for dynamic agent loading from database.
"""

from dataclasses import dataclass
from typing import Any, Callable, Optional

from langgraph.graph import StateGraph


@dataclass
class LoadedAgent:
    """Represents a loaded agent ready for execution."""
    id: str
    name: str
    description: str
    target_application: str
    capabilities: list[str]
    instruction_list: str
    scripts: list[dict]
    python_module: Optional[Any]
    tools: list[Callable]
    graph: Optional[StateGraph]
    metadata: dict


class AgentNotFoundError(Exception):
    """Raised when agent is not found."""
    pass


class CompilationError(Exception):
    """Raised when script compilation fails."""
    pass


class AgentLoader:
    """Loads agents from database and prepares them for execution."""
    
    def __init__(self, cascade_client):
        """
        Initialize the agent loader.
        
        Args:
            cascade_client: CascadeClient instance for gRPC communication
        """
        self.client = cascade_client
        self._loaded_agents: dict[str, LoadedAgent] = {}
    
    async def load_agent(
        self,
        agent_id: str = None,
        agent_name: str = None
    ) -> LoadedAgent:
        """
        Load an agent by ID or name.
        
        Args:
            agent_id: Agent UUID
            agent_name: Agent name
        
        Returns:
            LoadedAgent instance
        
        Raises:
            AgentNotFoundError: If agent is not found
            CompilationError: If script compilation fails
        """
        if not agent_id and not agent_name:
            raise ValueError("Either agent_id or agent_name must be provided")
        
        # Get agent definition from database
        agent_def = await self.client.get_agent_definition(
            agent_id or agent_name
        )
        
        if not agent_def.get("result", {}).get("success"):
            raise AgentNotFoundError(
                f"Agent not found: {agent_id or agent_name}"
            )
        
        # Compile scripts
        compiled_scripts = await self._compile_scripts(agent_def.get("scripts", []))
        
        # Create tools from scripts
        tools = self._create_tools(compiled_scripts)
        
        # Create execution graph
        graph = self._create_execution_graph(agent_def, tools)
        
        loaded_agent = LoadedAgent(
            id=agent_def["agent"]["id"],
            name=agent_def["agent"]["name"],
            description=agent_def["agent"]["description"],
            target_application=agent_def["agent"]["target_application"],
            capabilities=agent_def.get("capabilities", []),
            instruction_list=agent_def.get("instruction_list", ""),
            scripts=compiled_scripts,
            python_module=None,
            tools=tools,
            graph=graph,
            metadata=agent_def["agent"].get("metadata", {})
        )
        
        self._loaded_agents[loaded_agent.id] = loaded_agent
        return loaded_agent
    
    async def _compile_scripts(self, scripts: list[dict]) -> list[dict]:
        """Compile all scripts for the agent."""
        compiled = []
        
        for script in scripts:
            result = await self.client.compile(script["source_code"])
            
            if not result.get("compilation_success"):
                raise CompilationError(
                    f"Failed to compile {script['name']}: {result.get('errors')}"
                )
            
            compiled.append({
                **script,
                "assembly": result.get("assembly_bytes")
            })
        
        return compiled
    
    def _create_tools(self, scripts: list[dict]) -> list[Callable]:
        """Create LangChain tools from compiled scripts."""
        tools = []
        
        for script in scripts:
            tool = self._script_to_tool(script)
            if tool:
                tools.append(tool)
        
        return tools
    
    def _script_to_tool(self, script: dict) -> Optional[Callable]:
        """Convert a compiled script to a tool function."""
        # TODO: Implement tool creation from script
        return None
    
    def _create_execution_graph(
        self,
        agent_def: dict,
        tools: list[Callable]
    ) -> Optional[StateGraph]:
        """Create the LangGraph execution graph for the agent."""
        # TODO: Implement dynamic graph creation
        return None
    
    def get_loaded_agent(self, agent_id: str) -> Optional[LoadedAgent]:
        """Get a previously loaded agent."""
        return self._loaded_agents.get(agent_id)
    
    async def unload_agent(self, agent_id: str) -> None:
        """Unload an agent and free resources."""
        if agent_id in self._loaded_agents:
            del self._loaded_agents[agent_id]



using Cascade.Database.Entities;
using Cascade.Database.Filters;

namespace Cascade.Database.Repositories;

/// <summary>
/// Repository interface for Agent entities.
/// </summary>
public interface IAgentRepository
{
    /// <summary>
    /// Gets an agent by its unique identifier.
    /// </summary>
    /// <param name="id">The agent ID.</param>
    /// <returns>The agent if found, otherwise null.</returns>
    Task<Agent?> GetByIdAsync(Guid id);

    /// <summary>
    /// Gets an agent by its name.
    /// </summary>
    /// <param name="name">The agent name.</param>
    /// <returns>The agent if found, otherwise null.</returns>
    Task<Agent?> GetByNameAsync(string name);

    /// <summary>
    /// Gets all agents matching the optional filter.
    /// </summary>
    /// <param name="filter">Optional filter criteria.</param>
    /// <returns>List of matching agents.</returns>
    Task<IReadOnlyList<Agent>> GetAllAsync(AgentFilter? filter = null);

    /// <summary>
    /// Creates a new agent.
    /// </summary>
    /// <param name="agent">The agent to create.</param>
    /// <returns>The created agent with generated ID.</returns>
    Task<Agent> CreateAsync(Agent agent);

    /// <summary>
    /// Updates an existing agent.
    /// </summary>
    /// <param name="agent">The agent to update.</param>
    /// <returns>The updated agent.</returns>
    Task<Agent> UpdateAsync(Agent agent);

    /// <summary>
    /// Deletes an agent by its ID.
    /// </summary>
    /// <param name="id">The agent ID to delete.</param>
    Task DeleteAsync(Guid id);

    /// <summary>
    /// Creates a new version snapshot of an agent.
    /// </summary>
    /// <param name="agentId">The agent ID.</param>
    /// <param name="notes">Optional notes for this version.</param>
    /// <returns>The created agent version.</returns>
    Task<AgentVersion> CreateVersionAsync(Guid agentId, string? notes = null);

    /// <summary>
    /// Gets all versions for an agent.
    /// </summary>
    /// <param name="agentId">The agent ID.</param>
    /// <returns>List of agent versions.</returns>
    Task<IReadOnlyList<AgentVersion>> GetVersionsAsync(Guid agentId);

    /// <summary>
    /// Sets the active version for an agent.
    /// </summary>
    /// <param name="agentId">The agent ID.</param>
    /// <param name="version">The version string to activate.</param>
    Task SetActiveVersionAsync(Guid agentId, string version);

    /// <summary>
    /// Gets the total count of agents matching the filter.
    /// </summary>
    /// <param name="filter">Optional filter criteria.</param>
    /// <returns>The count of matching agents.</returns>
    Task<int> GetCountAsync(AgentFilter? filter = null);
}


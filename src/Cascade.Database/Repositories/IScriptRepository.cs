using Cascade.Database.Entities;
using Cascade.Database.Filters;

namespace Cascade.Database.Repositories;

/// <summary>
/// Repository interface for Script entities.
/// </summary>
public interface IScriptRepository
{
    /// <summary>
    /// Gets a script by its unique identifier.
    /// </summary>
    /// <param name="id">The script ID.</param>
    /// <returns>The script if found, otherwise null.</returns>
    Task<Script?> GetByIdAsync(Guid id);

    /// <summary>
    /// Gets a script by its name.
    /// </summary>
    /// <param name="name">The script name.</param>
    /// <returns>The script if found, otherwise null.</returns>
    Task<Script?> GetByNameAsync(string name);

    /// <summary>
    /// Gets all scripts for a specific agent.
    /// </summary>
    /// <param name="agentId">The agent ID.</param>
    /// <returns>List of scripts for the agent.</returns>
    Task<IReadOnlyList<Script>> GetByAgentIdAsync(Guid agentId);

    /// <summary>
    /// Gets all scripts matching the optional filter.
    /// </summary>
    /// <param name="filter">Optional filter criteria.</param>
    /// <returns>List of matching scripts.</returns>
    Task<IReadOnlyList<Script>> GetAllAsync(ScriptFilter? filter = null);

    /// <summary>
    /// Saves a script (creates if new, updates if existing).
    /// </summary>
    /// <param name="script">The script to save.</param>
    /// <returns>The saved script.</returns>
    Task<Script> SaveAsync(Script script);

    /// <summary>
    /// Deletes a script by its ID.
    /// </summary>
    /// <param name="id">The script ID to delete.</param>
    Task DeleteAsync(Guid id);

    /// <summary>
    /// Creates a new version snapshot of a script.
    /// </summary>
    /// <param name="scriptId">The script ID.</param>
    /// <param name="sourceCode">The source code for this version.</param>
    /// <param name="description">Optional description of changes.</param>
    /// <returns>The created script version.</returns>
    Task<ScriptVersion> CreateVersionAsync(Guid scriptId, string sourceCode, string? description = null);

    /// <summary>
    /// Gets all versions for a script.
    /// </summary>
    /// <param name="scriptId">The script ID.</param>
    /// <returns>List of script versions.</returns>
    Task<IReadOnlyList<ScriptVersion>> GetVersionsAsync(Guid scriptId);

    /// <summary>
    /// Gets the compiled assembly for a specific script version.
    /// </summary>
    /// <param name="scriptId">The script ID.</param>
    /// <param name="version">The version string.</param>
    /// <returns>The compiled assembly bytes if available.</returns>
    Task<byte[]?> GetCompiledAssemblyAsync(Guid scriptId, string version);

    /// <summary>
    /// Saves the compiled assembly for a specific script version.
    /// </summary>
    /// <param name="scriptId">The script ID.</param>
    /// <param name="version">The version string.</param>
    /// <param name="assembly">The compiled assembly bytes.</param>
    Task SaveCompiledAssemblyAsync(Guid scriptId, string version, byte[] assembly);

    /// <summary>
    /// Gets the total count of scripts matching the filter.
    /// </summary>
    /// <param name="filter">Optional filter criteria.</param>
    /// <returns>The count of matching scripts.</returns>
    Task<int> GetCountAsync(ScriptFilter? filter = null);
}


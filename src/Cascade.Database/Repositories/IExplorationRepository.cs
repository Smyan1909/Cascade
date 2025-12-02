using Cascade.Database.Entities;
using Cascade.Database.Filters;

namespace Cascade.Database.Repositories;

/// <summary>
/// Repository interface for exploration sessions and results.
/// </summary>
public interface IExplorationRepository
{
    /// <summary>
    /// Gets an exploration session by its ID.
    /// </summary>
    /// <param name="id">The session ID.</param>
    /// <returns>The session if found, otherwise null.</returns>
    Task<ExplorationSession?> GetSessionAsync(Guid id);

    /// <summary>
    /// Gets all exploration sessions matching the optional filter.
    /// </summary>
    /// <param name="filter">Optional filter criteria.</param>
    /// <returns>List of matching sessions.</returns>
    Task<IReadOnlyList<ExplorationSession>> GetSessionsAsync(ExplorationFilter? filter = null);

    /// <summary>
    /// Creates a new exploration session.
    /// </summary>
    /// <param name="session">The session to create.</param>
    /// <returns>The created session with generated ID.</returns>
    Task<ExplorationSession> CreateSessionAsync(ExplorationSession session);

    /// <summary>
    /// Updates an existing exploration session.
    /// </summary>
    /// <param name="session">The session to update.</param>
    /// <returns>The updated session.</returns>
    Task<ExplorationSession> UpdateSessionAsync(ExplorationSession session);

    /// <summary>
    /// Deletes an exploration session and all its results.
    /// </summary>
    /// <param name="id">The session ID to delete.</param>
    Task DeleteSessionAsync(Guid id);

    /// <summary>
    /// Adds a result to an exploration session.
    /// </summary>
    /// <param name="sessionId">The session ID.</param>
    /// <param name="result">The result to add.</param>
    Task AddResultAsync(Guid sessionId, ExplorationResult result);

    /// <summary>
    /// Gets all results for an exploration session.
    /// </summary>
    /// <param name="sessionId">The session ID.</param>
    /// <returns>List of exploration results.</returns>
    Task<IReadOnlyList<ExplorationResult>> GetResultsAsync(Guid sessionId);

    /// <summary>
    /// Gets the total count of sessions matching the filter.
    /// </summary>
    /// <param name="filter">Optional filter criteria.</param>
    /// <returns>The count of matching sessions.</returns>
    Task<int> GetSessionCountAsync(ExplorationFilter? filter = null);
}


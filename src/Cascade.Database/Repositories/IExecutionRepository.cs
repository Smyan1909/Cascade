using Cascade.Database.Entities;

namespace Cascade.Database.Repositories;

/// <summary>
/// Repository interface for execution records and steps.
/// </summary>
public interface IExecutionRepository
{
    /// <summary>
    /// Records a new execution.
    /// </summary>
    /// <param name="record">The execution record to save.</param>
    /// <returns>The saved execution record.</returns>
    Task<ExecutionRecord> RecordExecutionAsync(ExecutionRecord record);

    /// <summary>
    /// Gets the execution history for an agent.
    /// </summary>
    /// <param name="agentId">The agent ID.</param>
    /// <param name="limit">Maximum number of records to return.</param>
    /// <param name="offset">Number of records to skip.</param>
    /// <returns>List of execution records.</returns>
    Task<IReadOnlyList<ExecutionRecord>> GetHistoryAsync(Guid agentId, int limit = 100, int offset = 0);

    /// <summary>
    /// Gets an execution record by its ID.
    /// </summary>
    /// <param name="id">The execution record ID.</param>
    /// <returns>The execution record if found, otherwise null.</returns>
    Task<ExecutionRecord?> GetExecutionAsync(Guid id);

    /// <summary>
    /// Gets the total number of executions for an agent.
    /// </summary>
    /// <param name="agentId">The agent ID.</param>
    /// <returns>The total count.</returns>
    Task<int> GetTotalExecutionsAsync(Guid agentId);

    /// <summary>
    /// Adds a step to an execution record.
    /// </summary>
    /// <param name="executionId">The execution record ID.</param>
    /// <param name="step">The step to add.</param>
    Task AddStepAsync(Guid executionId, ExecutionStep step);

    /// <summary>
    /// Gets all steps for an execution record.
    /// </summary>
    /// <param name="executionId">The execution record ID.</param>
    /// <returns>List of execution steps in order.</returns>
    Task<IReadOnlyList<ExecutionStep>> GetStepsAsync(Guid executionId);

    /// <summary>
    /// Gets execution statistics for an agent.
    /// </summary>
    /// <param name="agentId">The agent ID.</param>
    /// <returns>Statistics including success rate, average duration, etc.</returns>
    Task<ExecutionStatistics> GetStatisticsAsync(Guid agentId);
}

/// <summary>
/// Statistics about agent executions.
/// </summary>
public class ExecutionStatistics
{
    /// <summary>
    /// Total number of executions.
    /// </summary>
    public int TotalExecutions { get; set; }

    /// <summary>
    /// Number of successful executions.
    /// </summary>
    public int SuccessfulExecutions { get; set; }

    /// <summary>
    /// Number of failed executions.
    /// </summary>
    public int FailedExecutions { get; set; }

    /// <summary>
    /// Success rate as a percentage (0-100).
    /// </summary>
    public double SuccessRate => TotalExecutions > 0 
        ? (double)SuccessfulExecutions / TotalExecutions * 100 
        : 0;

    /// <summary>
    /// Average execution duration in milliseconds.
    /// </summary>
    public double AverageDurationMs { get; set; }

    /// <summary>
    /// Total execution time in milliseconds.
    /// </summary>
    public long TotalDurationMs { get; set; }

    /// <summary>
    /// Timestamp of the last execution.
    /// </summary>
    public DateTime? LastExecutionAt { get; set; }
}


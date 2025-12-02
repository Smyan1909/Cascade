using Cascade.Database.Context;
using Cascade.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cascade.Database.Repositories.Implementations;

/// <summary>
/// Implementation of the execution repository.
/// </summary>
public class ExecutionRepository : IExecutionRepository
{
    private readonly CascadeDbContext _context;

    public ExecutionRepository(CascadeDbContext context)
    {
        _context = context;
    }

    public async Task<ExecutionRecord> RecordExecutionAsync(ExecutionRecord record)
    {
        if (record.Id == Guid.Empty)
        {
            record.Id = Guid.NewGuid();
        }

        _context.ExecutionRecords.Add(record);

        // Update agent's last executed timestamp
        var agent = await _context.Agents.FindAsync(record.AgentId);
        if (agent != null)
        {
            agent.LastExecutedAt = record.StartedAt;
        }

        await _context.SaveChangesAsync();
        return record;
    }

    public async Task<IReadOnlyList<ExecutionRecord>> GetHistoryAsync(Guid agentId, int limit = 100, int offset = 0)
    {
        return await _context.ExecutionRecords
            .Where(r => r.AgentId == agentId)
            .OrderByDescending(r => r.StartedAt)
            .Skip(offset)
            .Take(limit)
            .Include(r => r.Steps.OrderBy(s => s.Order))
            .ToListAsync();
    }

    public async Task<ExecutionRecord?> GetExecutionAsync(Guid id)
    {
        return await _context.ExecutionRecords
            .Include(r => r.Steps.OrderBy(s => s.Order))
            .Include(r => r.Agent)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<int> GetTotalExecutionsAsync(Guid agentId)
    {
        return await _context.ExecutionRecords
            .CountAsync(r => r.AgentId == agentId);
    }

    public async Task AddStepAsync(Guid executionId, ExecutionStep step)
    {
        if (step.Id == Guid.Empty)
        {
            step.Id = Guid.NewGuid();
        }

        step.ExecutionId = executionId;

        // Auto-set order if not provided
        if (step.Order == 0)
        {
            var maxOrder = await _context.ExecutionSteps
                .Where(s => s.ExecutionId == executionId)
                .Select(s => (int?)s.Order)
                .MaxAsync() ?? 0;
            
            step.Order = maxOrder + 1;
        }

        _context.ExecutionSteps.Add(step);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<ExecutionStep>> GetStepsAsync(Guid executionId)
    {
        return await _context.ExecutionSteps
            .Where(s => s.ExecutionId == executionId)
            .OrderBy(s => s.Order)
            .ToListAsync();
    }

    public async Task<ExecutionStatistics> GetStatisticsAsync(Guid agentId)
    {
        var records = await _context.ExecutionRecords
            .Where(r => r.AgentId == agentId)
            .ToListAsync();

        if (records.Count == 0)
        {
            return new ExecutionStatistics();
        }

        var successful = records.Count(r => r.Success);
        var totalDuration = records.Sum(r => (long)r.DurationMs);

        return new ExecutionStatistics
        {
            TotalExecutions = records.Count,
            SuccessfulExecutions = successful,
            FailedExecutions = records.Count - successful,
            AverageDurationMs = (double)totalDuration / records.Count,
            TotalDurationMs = totalDuration,
            LastExecutionAt = records.Max(r => r.StartedAt)
        };
    }
}


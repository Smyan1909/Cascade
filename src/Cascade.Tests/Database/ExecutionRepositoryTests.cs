using Cascade.Database.Entities;
using Cascade.Database.Repositories.Implementations;
using FluentAssertions;
using Xunit;

namespace Cascade.Tests.Database;

public class ExecutionRepositoryTests : IDisposable
{
    private readonly TestDbContextFactory _factory;

    public ExecutionRepositoryTests()
    {
        _factory = new TestDbContextFactory();
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    private async Task<Agent> CreateTestAgentAsync()
    {
        using var context = _factory.CreateContext();
        var agentRepo = new AgentRepository(context);
        return await agentRepo.CreateAsync(new Agent
        {
            Name = $"TestAgent_{Guid.NewGuid()}",
            TargetApplication = "App"
        });
    }

    [Fact]
    public async Task RecordExecutionAsync_ShouldCreateRecord_WithGeneratedId()
    {
        // Arrange
        var agent = await CreateTestAgentAsync();
        using var context = _factory.CreateContext();
        var repository = new ExecutionRepository(context);
        
        var record = new ExecutionRecord
        {
            AgentId = agent.Id,
            TaskDescription = "Add two numbers",
            Success = true,
            Summary = "Added 2 + 2 = 4",
            DurationMs = 1500,
            Logs = new List<string> { "Started", "Completed" }
        };

        // Act
        var result = await repository.RecordExecutionAsync(record);

        // Assert
        result.Id.Should().NotBe(Guid.Empty);
        result.TaskDescription.Should().Be("Add two numbers");
        result.Success.Should().BeTrue();
        result.StartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetExecutionAsync_ShouldReturnRecord_WhenExists()
    {
        // Arrange
        var agent = await CreateTestAgentAsync();
        using var context = _factory.CreateContext();
        var repository = new ExecutionRepository(context);
        
        var record = await repository.RecordExecutionAsync(new ExecutionRecord
        {
            AgentId = agent.Id,
            TaskDescription = "Test task",
            Success = true
        });

        // Act
        var result = await repository.GetExecutionAsync(record.Id);

        // Assert
        result.Should().NotBeNull();
        result!.TaskDescription.Should().Be("Test task");
    }

    [Fact]
    public async Task GetExecutionAsync_ShouldReturnNull_WhenNotExists()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new ExecutionRepository(context);

        // Act
        var result = await repository.GetExecutionAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetHistoryAsync_ShouldReturnExecutionsForAgent()
    {
        // Arrange
        var agent = await CreateTestAgentAsync();
        using var context = _factory.CreateContext();
        var repository = new ExecutionRepository(context);

        await repository.RecordExecutionAsync(new ExecutionRecord
        {
            AgentId = agent.Id,
            TaskDescription = "Task 1",
            Success = true
        });
        await repository.RecordExecutionAsync(new ExecutionRecord
        {
            AgentId = agent.Id,
            TaskDescription = "Task 2",
            Success = false
        });

        // Act
        var history = await repository.GetHistoryAsync(agent.Id);

        // Assert
        history.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetHistoryAsync_WithPagination_ShouldReturnPagedResults()
    {
        // Arrange
        var agent = await CreateTestAgentAsync();
        using var context = _factory.CreateContext();
        var repository = new ExecutionRepository(context);

        for (int i = 0; i < 10; i++)
        {
            await repository.RecordExecutionAsync(new ExecutionRecord
            {
                AgentId = agent.Id,
                TaskDescription = $"Task {i}",
                Success = true
            });
        }

        // Act
        var history = await repository.GetHistoryAsync(agent.Id, limit: 3, offset: 2);

        // Assert
        history.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetTotalExecutionsAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        var agent = await CreateTestAgentAsync();
        using var context = _factory.CreateContext();
        var repository = new ExecutionRepository(context);

        await repository.RecordExecutionAsync(new ExecutionRecord
        {
            AgentId = agent.Id,
            TaskDescription = "Task 1",
            Success = true
        });
        await repository.RecordExecutionAsync(new ExecutionRecord
        {
            AgentId = agent.Id,
            TaskDescription = "Task 2",
            Success = true
        });

        // Act
        var count = await repository.GetTotalExecutionsAsync(agent.Id);

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public async Task AddStepAsync_ShouldAddStepToExecution()
    {
        // Arrange
        var agent = await CreateTestAgentAsync();
        using var context = _factory.CreateContext();
        var repository = new ExecutionRepository(context);
        
        var record = await repository.RecordExecutionAsync(new ExecutionRecord
        {
            AgentId = agent.Id,
            TaskDescription = "Task",
            Success = true
        });

        var step = new ExecutionStep
        {
            Action = "Click Button",
            Parameters = "{\"x\": 100, \"y\": 200}",
            Success = true,
            DurationMs = 50
        };

        // Act
        await repository.AddStepAsync(record.Id, step);

        // Assert
        var steps = await repository.GetStepsAsync(record.Id);
        steps.Should().HaveCount(1);
        steps.First().Action.Should().Be("Click Button");
        steps.First().Order.Should().Be(1);
    }

    [Fact]
    public async Task GetStepsAsync_ShouldReturnStepsInOrder()
    {
        // Arrange
        var agent = await CreateTestAgentAsync();
        using var context = _factory.CreateContext();
        var repository = new ExecutionRepository(context);
        
        var record = await repository.RecordExecutionAsync(new ExecutionRecord
        {
            AgentId = agent.Id,
            TaskDescription = "Task",
            Success = true
        });

        await repository.AddStepAsync(record.Id, new ExecutionStep { Action = "Step 1", Success = true });
        await repository.AddStepAsync(record.Id, new ExecutionStep { Action = "Step 2", Success = true });
        await repository.AddStepAsync(record.Id, new ExecutionStep { Action = "Step 3", Success = true });

        // Act
        var steps = await repository.GetStepsAsync(record.Id);

        // Assert
        steps.Should().HaveCount(3);
        steps[0].Order.Should().Be(1);
        steps[1].Order.Should().Be(2);
        steps[2].Order.Should().Be(3);
    }

    [Fact]
    public async Task GetStatisticsAsync_ShouldReturnCorrectStats()
    {
        // Arrange
        var agent = await CreateTestAgentAsync();
        using var context = _factory.CreateContext();
        var repository = new ExecutionRepository(context);

        await repository.RecordExecutionAsync(new ExecutionRecord
        {
            AgentId = agent.Id,
            TaskDescription = "Task 1",
            Success = true,
            DurationMs = 1000
        });
        await repository.RecordExecutionAsync(new ExecutionRecord
        {
            AgentId = agent.Id,
            TaskDescription = "Task 2",
            Success = true,
            DurationMs = 2000
        });
        await repository.RecordExecutionAsync(new ExecutionRecord
        {
            AgentId = agent.Id,
            TaskDescription = "Task 3",
            Success = false,
            DurationMs = 500
        });

        // Act
        var stats = await repository.GetStatisticsAsync(agent.Id);

        // Assert
        stats.TotalExecutions.Should().Be(3);
        stats.SuccessfulExecutions.Should().Be(2);
        stats.FailedExecutions.Should().Be(1);
        stats.SuccessRate.Should().BeApproximately(66.67, 0.1);
        stats.AverageDurationMs.Should().BeApproximately(1166.67, 0.1);
        stats.TotalDurationMs.Should().Be(3500);
    }

    [Fact]
    public async Task GetStatisticsAsync_ShouldReturnEmptyStats_WhenNoExecutions()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new ExecutionRepository(context);

        // Act
        var stats = await repository.GetStatisticsAsync(Guid.NewGuid());

        // Assert
        stats.TotalExecutions.Should().Be(0);
        stats.SuccessRate.Should().Be(0);
    }
}


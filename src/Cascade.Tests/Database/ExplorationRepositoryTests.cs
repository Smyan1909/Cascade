using Cascade.Database.Entities;
using Cascade.Database.Enums;
using Cascade.Database.Filters;
using Cascade.Database.Repositories.Implementations;
using FluentAssertions;
using Xunit;

namespace Cascade.Tests.Database;

public class ExplorationRepositoryTests : IDisposable
{
    private readonly TestDbContextFactory _factory;

    public ExplorationRepositoryTests()
    {
        _factory = new TestDbContextFactory();
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    [Fact]
    public async Task CreateSessionAsync_ShouldCreateSession_WithGeneratedId()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new ExplorationRepository(context);
        var session = new ExplorationSession
        {
            TargetApplication = "Calculator",
            InstructionManual = "Learn to add numbers",
            Status = ExplorationStatus.Pending,
            Goals = new List<ExplorationGoal>
            {
                new ExplorationGoal
                {
                    Id = "goal1",
                    Description = "Find add button"
                }
            }
        };

        // Act
        var result = await repository.CreateSessionAsync(session);

        // Assert
        result.Id.Should().NotBe(Guid.Empty);
        result.TargetApplication.Should().Be("Calculator");
        result.Status.Should().Be(ExplorationStatus.Pending);
        result.Goals.Should().HaveCount(1);
        result.StartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetSessionAsync_ShouldReturnSession_WhenExists()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new ExplorationRepository(context);
        var session = await repository.CreateSessionAsync(new ExplorationSession
        {
            TargetApplication = "Notepad",
            Status = ExplorationStatus.InProgress
        });

        // Act
        var result = await repository.GetSessionAsync(session.Id);

        // Assert
        result.Should().NotBeNull();
        result!.TargetApplication.Should().Be("Notepad");
        result.Status.Should().Be(ExplorationStatus.InProgress);
    }

    [Fact]
    public async Task GetSessionAsync_ShouldReturnNull_WhenNotExists()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new ExplorationRepository(context);

        // Act
        var result = await repository.GetSessionAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetSessionsAsync_ShouldReturnAllSessions()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new ExplorationRepository(context);
        await repository.CreateSessionAsync(new ExplorationSession 
        { 
            TargetApplication = "App1",
            Status = ExplorationStatus.Completed
        });
        await repository.CreateSessionAsync(new ExplorationSession 
        { 
            TargetApplication = "App2",
            Status = ExplorationStatus.InProgress
        });

        // Act
        var result = await repository.GetSessionsAsync();

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetSessionsAsync_WithStatusFilter_ShouldReturnMatchingSessions()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new ExplorationRepository(context);
        await repository.CreateSessionAsync(new ExplorationSession 
        { 
            TargetApplication = "App1",
            Status = ExplorationStatus.Completed
        });
        await repository.CreateSessionAsync(new ExplorationSession 
        { 
            TargetApplication = "App2",
            Status = ExplorationStatus.InProgress
        });
        await repository.CreateSessionAsync(new ExplorationSession 
        { 
            TargetApplication = "App3",
            Status = ExplorationStatus.Completed
        });

        // Act
        var result = await repository.GetSessionsAsync(new ExplorationFilter
        {
            Status = ExplorationStatus.Completed
        });

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(s => s.Status == ExplorationStatus.Completed);
    }

    [Fact]
    public async Task UpdateSessionAsync_ShouldUpdateSession()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new ExplorationRepository(context);
        var session = await repository.CreateSessionAsync(new ExplorationSession
        {
            TargetApplication = "App",
            Status = ExplorationStatus.InProgress,
            Progress = 0.0f
        });

        // Act
        session.Status = ExplorationStatus.Completed;
        session.Progress = 1.0f;
        session.CompletedAt = DateTime.UtcNow;
        var result = await repository.UpdateSessionAsync(session);

        // Assert
        result.Status.Should().Be(ExplorationStatus.Completed);
        result.Progress.Should().Be(1.0f);
        result.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteSessionAsync_ShouldRemoveSession()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new ExplorationRepository(context);
        var session = await repository.CreateSessionAsync(new ExplorationSession
        {
            TargetApplication = "ToDelete",
            Status = ExplorationStatus.Pending
        });

        // Act
        await repository.DeleteSessionAsync(session.Id);
        var result = await repository.GetSessionAsync(session.Id);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task AddResultAsync_ShouldAddResultToSession()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new ExplorationRepository(context);
        var session = await repository.CreateSessionAsync(new ExplorationSession
        {
            TargetApplication = "App",
            Status = ExplorationStatus.InProgress
        });

        var result = new ExplorationResult
        {
            Type = ExplorationResultType.Window,
            WindowTitle = "Main Window",
            ElementData = "{\"id\": \"window1\"}"
        };

        // Act
        await repository.AddResultAsync(session.Id, result);

        // Assert
        var results = await repository.GetResultsAsync(session.Id);
        results.Should().HaveCount(1);
        results.First().WindowTitle.Should().Be("Main Window");
        results.First().Type.Should().Be(ExplorationResultType.Window);
    }

    [Fact]
    public async Task GetResultsAsync_ShouldReturnAllResultsForSession()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new ExplorationRepository(context);
        var session = await repository.CreateSessionAsync(new ExplorationSession
        {
            TargetApplication = "App",
            Status = ExplorationStatus.InProgress
        });

        await repository.AddResultAsync(session.Id, new ExplorationResult
        {
            Type = ExplorationResultType.Window,
            WindowTitle = "Window 1"
        });
        await repository.AddResultAsync(session.Id, new ExplorationResult
        {
            Type = ExplorationResultType.Element,
            ElementData = "{}"
        });

        // Act
        var results = await repository.GetResultsAsync(session.Id);

        // Assert
        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetSessionCountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new ExplorationRepository(context);
        await repository.CreateSessionAsync(new ExplorationSession 
        { 
            TargetApplication = "App1",
            Status = ExplorationStatus.Pending
        });
        await repository.CreateSessionAsync(new ExplorationSession 
        { 
            TargetApplication = "App2",
            Status = ExplorationStatus.Pending
        });

        // Act
        var count = await repository.GetSessionCountAsync();

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public async Task DeleteSessionAsync_ShouldCascadeDeleteResults()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new ExplorationRepository(context);
        var session = await repository.CreateSessionAsync(new ExplorationSession
        {
            TargetApplication = "App",
            Status = ExplorationStatus.InProgress
        });
        await repository.AddResultAsync(session.Id, new ExplorationResult
        {
            Type = ExplorationResultType.Window,
            WindowTitle = "Window"
        });

        // Act
        await repository.DeleteSessionAsync(session.Id);
        var results = await repository.GetResultsAsync(session.Id);

        // Assert
        results.Should().BeEmpty();
    }
}


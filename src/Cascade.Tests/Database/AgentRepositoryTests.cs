using Cascade.Database.Entities;
using Cascade.Database.Enums;
using Cascade.Database.Filters;
using Cascade.Database.Repositories.Implementations;
using FluentAssertions;
using Xunit;

namespace Cascade.Tests.Database;

public class AgentRepositoryTests : IDisposable
{
    private readonly TestDbContextFactory _factory;

    public AgentRepositoryTests()
    {
        _factory = new TestDbContextFactory();
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateAgent_WithGeneratedId()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new AgentRepository(context);
        var agent = new Agent
        {
            Name = "TestAgent",
            Description = "A test agent",
            TargetApplication = "Calculator",
            Capabilities = new List<string> { "Add", "Subtract" },
            Metadata = new Dictionary<string, string> { { "key", "value" } }
        };

        // Act
        var result = await repository.CreateAsync(agent);

        // Assert
        result.Id.Should().NotBe(Guid.Empty);
        result.Name.Should().Be("TestAgent");
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnAgent_WhenExists()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new AgentRepository(context);
        var agent = await repository.CreateAsync(new Agent
        {
            Name = "TestAgent",
            TargetApplication = "Calculator"
        });

        // Act
        var result = await repository.GetByIdAsync(agent.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("TestAgent");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenNotExists()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new AgentRepository(context);

        // Act
        var result = await repository.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByNameAsync_ShouldReturnAgent_WhenExists()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new AgentRepository(context);
        await repository.CreateAsync(new Agent
        {
            Name = "UniqueAgent",
            TargetApplication = "Notepad"
        });

        // Act
        var result = await repository.GetByNameAsync("UniqueAgent");

        // Assert
        result.Should().NotBeNull();
        result!.TargetApplication.Should().Be("Notepad");
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllAgents()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new AgentRepository(context);
        await repository.CreateAsync(new Agent { Name = "Agent1", TargetApplication = "App1" });
        await repository.CreateAsync(new Agent { Name = "Agent2", TargetApplication = "App2" });
        await repository.CreateAsync(new Agent { Name = "Agent3", TargetApplication = "App3" });

        // Act
        var result = await repository.GetAllAsync();

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetAllAsync_WithFilter_ShouldReturnFilteredAgents()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new AgentRepository(context);
        await repository.CreateAsync(new Agent 
        { 
            Name = "Agent1", 
            TargetApplication = "Calculator",
            Status = AgentStatus.Active
        });
        await repository.CreateAsync(new Agent 
        { 
            Name = "Agent2", 
            TargetApplication = "Notepad",
            Status = AgentStatus.Active
        });
        await repository.CreateAsync(new Agent 
        { 
            Name = "Agent3", 
            TargetApplication = "Calculator",
            Status = AgentStatus.Inactive
        });

        // Act
        var result = await repository.GetAllAsync(new AgentFilter
        {
            TargetApplication = "Calculator"
        });

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(a => a.TargetApplication.Contains("Calculator"));
    }

    [Fact]
    public async Task GetAllAsync_WithStatusFilter_ShouldReturnMatchingAgents()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new AgentRepository(context);
        await repository.CreateAsync(new Agent 
        { 
            Name = "Active1", 
            TargetApplication = "App",
            Status = AgentStatus.Active
        });
        await repository.CreateAsync(new Agent 
        { 
            Name = "Inactive1", 
            TargetApplication = "App",
            Status = AgentStatus.Inactive
        });

        // Act
        var result = await repository.GetAllAsync(new AgentFilter { Status = AgentStatus.Active });

        // Assert
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("Active1");
    }

    [Fact]
    public async Task GetAllAsync_WithPagination_ShouldReturnPagedResults()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new AgentRepository(context);
        for (int i = 1; i <= 10; i++)
        {
            await repository.CreateAsync(new Agent 
            { 
                Name = $"Agent{i:D2}", 
                TargetApplication = "App"
            });
        }

        // Act
        var result = await repository.GetAllAsync(new AgentFilter
        {
            Skip = 2,
            Take = 3
        });

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateAgent()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new AgentRepository(context);
        var agent = await repository.CreateAsync(new Agent
        {
            Name = "Original",
            TargetApplication = "App"
        });

        // Act
        agent.Name = "Updated";
        agent.Description = "Updated description";
        var result = await repository.UpdateAsync(agent);

        // Assert
        result.Name.Should().Be("Updated");
        result.Description.Should().Be("Updated description");
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveAgent()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new AgentRepository(context);
        var agent = await repository.CreateAsync(new Agent
        {
            Name = "ToDelete",
            TargetApplication = "App"
        });

        // Act
        await repository.DeleteAsync(agent.Id);
        var result = await repository.GetByIdAsync(agent.Id);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateVersionAsync_ShouldCreateVersionSnapshot()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new AgentRepository(context);
        var agent = await repository.CreateAsync(new Agent
        {
            Name = "VersionedAgent",
            TargetApplication = "App",
            Capabilities = new List<string> { "Cap1", "Cap2" },
            InstructionList = "Test instructions"
        });

        // Act
        var version = await repository.CreateVersionAsync(agent.Id, "First version");

        // Assert
        version.Should().NotBeNull();
        version.Version.Should().Be("1.0.1");
        version.IsActive.Should().BeTrue();
        version.CapabilitiesSnapshot.Should().BeEquivalentTo(agent.Capabilities);
        version.InstructionListSnapshot.Should().Be("Test instructions");
        version.Notes.Should().Be("First version");
    }

    [Fact]
    public async Task GetVersionsAsync_ShouldReturnAllVersions()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new AgentRepository(context);
        var agent = await repository.CreateAsync(new Agent
        {
            Name = "VersionedAgent",
            TargetApplication = "App"
        });
        await repository.CreateVersionAsync(agent.Id, "Version 1");
        await repository.CreateVersionAsync(agent.Id, "Version 2");

        // Act
        var versions = await repository.GetVersionsAsync(agent.Id);

        // Assert
        versions.Should().HaveCount(2);
    }

    [Fact]
    public async Task SetActiveVersionAsync_ShouldChangeActiveVersion()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new AgentRepository(context);
        var agent = await repository.CreateAsync(new Agent
        {
            Name = "VersionedAgent",
            TargetApplication = "App"
        });
        await repository.CreateVersionAsync(agent.Id, "Version 1");
        await repository.CreateVersionAsync(agent.Id, "Version 2");

        // Act
        await repository.SetActiveVersionAsync(agent.Id, "1.0.1");

        // Assert
        var versions = await repository.GetVersionsAsync(agent.Id);
        versions.Single(v => v.Version == "1.0.1").IsActive.Should().BeTrue();
        versions.Single(v => v.Version == "1.0.2").IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetCountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new AgentRepository(context);
        await repository.CreateAsync(new Agent { Name = "Agent1", TargetApplication = "App" });
        await repository.CreateAsync(new Agent { Name = "Agent2", TargetApplication = "App" });

        // Act
        var count = await repository.GetCountAsync();

        // Assert
        count.Should().Be(2);
    }
}


using Cascade.Database.Entities;
using Cascade.Database.Enums;
using Cascade.Database.Filters;
using Cascade.Database.Repositories.Implementations;
using FluentAssertions;
using Xunit;

namespace Cascade.Tests.Database;

public class ScriptRepositoryTests : IDisposable
{
    private readonly TestDbContextFactory _factory;

    public ScriptRepositoryTests()
    {
        _factory = new TestDbContextFactory();
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    [Fact]
    public async Task SaveAsync_ShouldCreateScript_WithGeneratedId()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new ScriptRepository(context);
        var script = new Script
        {
            Name = "TestScript",
            Description = "A test script",
            SourceCode = "Console.WriteLine(\"Hello\");",
            Type = ScriptType.Action
        };

        // Act
        var result = await repository.SaveAsync(script);

        // Assert
        result.Id.Should().NotBe(Guid.Empty);
        result.Name.Should().Be("TestScript");
        result.Type.Should().Be(ScriptType.Action);
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SaveAsync_ShouldUpdateScript_WhenAlreadyExists()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new ScriptRepository(context);
        var script = await repository.SaveAsync(new Script
        {
            Name = "Original",
            SourceCode = "original code",
            Type = ScriptType.Action
        });

        // Act
        script.SourceCode = "updated code";
        var result = await repository.SaveAsync(script);

        // Assert
        result.SourceCode.Should().Be("updated code");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnScript_WhenExists()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new ScriptRepository(context);
        var script = await repository.SaveAsync(new Script
        {
            Name = "TestScript",
            SourceCode = "code",
            Type = ScriptType.Workflow
        });

        // Act
        var result = await repository.GetByIdAsync(script.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("TestScript");
        result.Type.Should().Be(ScriptType.Workflow);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenNotExists()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new ScriptRepository(context);

        // Act
        var result = await repository.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByNameAsync_ShouldReturnScript_WhenExists()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new ScriptRepository(context);
        await repository.SaveAsync(new Script
        {
            Name = "UniqueScript",
            SourceCode = "code",
            Type = ScriptType.Utility
        });

        // Act
        var result = await repository.GetByNameAsync("UniqueScript");

        // Assert
        result.Should().NotBeNull();
        result!.Type.Should().Be(ScriptType.Utility);
    }

    [Fact]
    public async Task GetByAgentIdAsync_ShouldReturnScriptsForAgent()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var agentRepo = new AgentRepository(context);
        var scriptRepo = new ScriptRepository(context);

        var agent = await agentRepo.CreateAsync(new Agent
        {
            Name = "TestAgent",
            TargetApplication = "App"
        });

        await scriptRepo.SaveAsync(new Script
        {
            Name = "Script1",
            SourceCode = "code1",
            Type = ScriptType.Action,
            AgentId = agent.Id
        });
        await scriptRepo.SaveAsync(new Script
        {
            Name = "Script2",
            SourceCode = "code2",
            Type = ScriptType.Workflow,
            AgentId = agent.Id
        });
        await scriptRepo.SaveAsync(new Script
        {
            Name = "Orphan",
            SourceCode = "orphan code",
            Type = ScriptType.Test
        });

        // Act
        var result = await scriptRepo.GetByAgentIdAsync(agent.Id);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(s => s.AgentId == agent.Id);
    }

    [Fact]
    public async Task GetAllAsync_WithTypeFilter_ShouldReturnMatchingScripts()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new ScriptRepository(context);
        await repository.SaveAsync(new Script { Name = "Script1", SourceCode = "code", Type = ScriptType.Action });
        await repository.SaveAsync(new Script { Name = "Script2", SourceCode = "code", Type = ScriptType.Workflow });
        await repository.SaveAsync(new Script { Name = "Script3", SourceCode = "code", Type = ScriptType.Action });

        // Act
        var result = await repository.GetAllAsync(new ScriptFilter { Type = ScriptType.Action });

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(s => s.Type == ScriptType.Action);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveScript()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new ScriptRepository(context);
        var script = await repository.SaveAsync(new Script
        {
            Name = "ToDelete",
            SourceCode = "code",
            Type = ScriptType.Test
        });

        // Act
        await repository.DeleteAsync(script.Id);
        var result = await repository.GetByIdAsync(script.Id);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateVersionAsync_ShouldCreateVersionSnapshot()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new ScriptRepository(context);
        var script = await repository.SaveAsync(new Script
        {
            Name = "VersionedScript",
            SourceCode = "original code",
            Type = ScriptType.Action
        });

        // Act
        var version = await repository.CreateVersionAsync(script.Id, "new code", "Bug fix");

        // Assert
        version.Should().NotBeNull();
        version.Version.Should().Be("1.0.1");
        version.SourceCode.Should().Be("new code");
        version.ChangeDescription.Should().Be("Bug fix");
    }

    [Fact]
    public async Task GetVersionsAsync_ShouldReturnAllVersions()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new ScriptRepository(context);
        var script = await repository.SaveAsync(new Script
        {
            Name = "VersionedScript",
            SourceCode = "code",
            Type = ScriptType.Action
        });
        await repository.CreateVersionAsync(script.Id, "v1 code", "V1");
        await repository.CreateVersionAsync(script.Id, "v2 code", "V2");

        // Act
        var versions = await repository.GetVersionsAsync(script.Id);

        // Assert
        versions.Should().HaveCount(2);
    }

    [Fact]
    public async Task SaveCompiledAssemblyAsync_ShouldStoreAssembly()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new ScriptRepository(context);
        var script = await repository.SaveAsync(new Script
        {
            Name = "CompiledScript",
            SourceCode = "code",
            Type = ScriptType.Action
        });
        await repository.CreateVersionAsync(script.Id, "code", "Compiled");
        var assemblyBytes = new byte[] { 0x00, 0x01, 0x02 };

        // Act
        await repository.SaveCompiledAssemblyAsync(script.Id, "1.0.1", assemblyBytes);

        // Assert
        var retrieved = await repository.GetCompiledAssemblyAsync(script.Id, "1.0.1");
        retrieved.Should().BeEquivalentTo(assemblyBytes);
    }

    [Fact]
    public async Task GetCountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new ScriptRepository(context);
        await repository.SaveAsync(new Script { Name = "Script1", SourceCode = "code", Type = ScriptType.Action });
        await repository.SaveAsync(new Script { Name = "Script2", SourceCode = "code", Type = ScriptType.Workflow });

        // Act
        var count = await repository.GetCountAsync();

        // Assert
        count.Should().Be(2);
    }
}


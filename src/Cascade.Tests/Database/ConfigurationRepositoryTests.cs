using Cascade.Database.Enums;
using Cascade.Database.Repositories.Implementations;
using FluentAssertions;
using Xunit;

namespace Cascade.Tests.Database;

public class ConfigurationRepositoryTests : IDisposable
{
    private readonly TestDbContextFactory _factory;

    public ConfigurationRepositoryTests()
    {
        _factory = new TestDbContextFactory();
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    [Fact]
    public async Task SetAsync_ShouldCreateConfiguration()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new ConfigurationRepository(context);

        // Act
        await repository.SetAsync("test.key", "test-value", "A test config");

        // Assert
        var result = await repository.GetAsync("test.key");
        result.Should().NotBeNull();
        result!.Value.Should().Be("test-value");
        result.Description.Should().Be("A test config");
    }

    [Fact]
    public async Task SetAsync_ShouldUpdateExistingConfiguration()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new ConfigurationRepository(context);
        await repository.SetAsync("update.key", "original");

        // Act
        await repository.SetAsync("update.key", "updated");

        // Assert
        var result = await repository.GetValueAsync("update.key");
        result.Should().Be("updated");
    }

    [Fact]
    public async Task GetAsync_ShouldReturnConfiguration_WhenExists()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new ConfigurationRepository(context);
        await repository.SetAsync("existing.key", "value", type: ConfigurationType.String);

        // Act
        var result = await repository.GetAsync("existing.key");

        // Assert
        result.Should().NotBeNull();
        result!.Key.Should().Be("existing.key");
        result.Value.Should().Be("value");
        result.Type.Should().Be(ConfigurationType.String);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnNull_WhenNotExists()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new ConfigurationRepository(context);

        // Act
        var result = await repository.GetAsync("nonexistent.key");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetValueAsync_ShouldReturnValue_WhenExists()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new ConfigurationRepository(context);
        await repository.SetAsync("value.key", "the-value");

        // Act
        var result = await repository.GetValueAsync("value.key");

        // Assert
        result.Should().Be("the-value");
    }

    [Fact]
    public async Task GetValueAsync_ShouldReturnDefault_WhenNotExists()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new ConfigurationRepository(context);

        // Act
        var result = await repository.GetValueAsync("missing.key", "default-value");

        // Assert
        result.Should().Be("default-value");
    }

    [Fact]
    public async Task GetIntAsync_ShouldReturnIntValue()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new ConfigurationRepository(context);
        await repository.SetAsync("int.key", "42", type: ConfigurationType.Integer);

        // Act
        var result = await repository.GetIntAsync("int.key");

        // Assert
        result.Should().Be(42);
    }

    [Fact]
    public async Task GetIntAsync_ShouldReturnDefault_WhenNotValid()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new ConfigurationRepository(context);
        await repository.SetAsync("invalid.int", "not-a-number");

        // Act
        var result = await repository.GetIntAsync("invalid.int", 99);

        // Assert
        result.Should().Be(99);
    }

    [Fact]
    public async Task GetBoolAsync_ShouldReturnBoolValue()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new ConfigurationRepository(context);
        await repository.SetAsync("bool.key", "true", type: ConfigurationType.Boolean);

        // Act
        var result = await repository.GetBoolAsync("bool.key");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetBoolAsync_ShouldReturnDefault_WhenNotValid()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new ConfigurationRepository(context);
        await repository.SetAsync("invalid.bool", "maybe");

        // Act
        var result = await repository.GetBoolAsync("invalid.bool", true);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveConfiguration()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new ConfigurationRepository(context);
        await repository.SetAsync("delete.key", "value");

        // Act
        await repository.DeleteAsync("delete.key");

        // Assert
        var exists = await repository.ExistsAsync("delete.key");
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllConfigurations()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new ConfigurationRepository(context);
        await repository.SetAsync("config.one", "value1");
        await repository.SetAsync("config.two", "value2");
        await repository.SetAsync("config.three", "value3");

        // Act
        var result = await repository.GetAllAsync();

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrue_WhenExists()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new ConfigurationRepository(context);
        await repository.SetAsync("exists.key", "value");

        // Act
        var result = await repository.ExistsAsync("exists.key");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalse_WhenNotExists()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new ConfigurationRepository(context);

        // Act
        var result = await repository.ExistsAsync("nonexistent.key");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SetAsync_ShouldSupportEncryptedFlag()
    {
        // Arrange
        using var context = _factory.CreateContext();
        var repository = new ConfigurationRepository(context);

        // Act
        await repository.SetAsync("secret.key", "secret-value", 
            type: ConfigurationType.Secret, isEncrypted: true);

        // Assert
        var result = await repository.GetAsync("secret.key");
        result.Should().NotBeNull();
        result!.Type.Should().Be(ConfigurationType.Secret);
        result.IsEncrypted.Should().BeTrue();
    }
}


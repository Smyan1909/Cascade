using System.Drawing;
using Cascade.UIAutomation.Elements;
using Cascade.UIAutomation.Enums;
using FluentAssertions;
using Moq;
using Xunit;

namespace Cascade.Tests.UIAutomation;

public class ElementCacheTests
{
    private static Mock<IUIElement> CreateMockElement(string runtimeId)
    {
        var mock = new Mock<IUIElement>();
        mock.Setup(e => e.RuntimeId).Returns(runtimeId);
        mock.Setup(e => e.Name).Returns($"Element_{runtimeId}");
        mock.Setup(e => e.IsEnabled).Returns(true);
        return mock;
    }

    [Fact]
    public void Cache_ShouldStoreElement()
    {
        // Arrange
        var cache = new ElementCache();
        var element = CreateMockElement("test-123").Object;

        // Act
        cache.Cache(element);

        // Assert
        cache.Count.Should().Be(1);
        var cached = cache.GetCached("test-123");
        cached.Should().NotBeNull();
        cached!.RuntimeId.Should().Be("test-123");
    }

    [Fact]
    public void GetCached_ShouldReturnNull_WhenNotCached()
    {
        // Arrange
        var cache = new ElementCache();

        // Act
        var result = cache.GetCached("nonexistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetCached_ShouldReturnNull_WhenExpired()
    {
        // Arrange
        var cache = new ElementCache
        {
            DefaultCacheDuration = TimeSpan.FromMilliseconds(50)
        };
        var element = CreateMockElement("test-123").Object;
        cache.Cache(element);

        // Act
        Thread.Sleep(100); // Wait for expiration
        var result = cache.GetCached("test-123");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Invalidate_ShouldRemoveElement()
    {
        // Arrange
        var cache = new ElementCache();
        var element = CreateMockElement("test-123").Object;
        cache.Cache(element);

        // Act
        cache.Invalidate("test-123");

        // Assert
        cache.GetCached("test-123").Should().BeNull();
        cache.Count.Should().Be(0);
    }

    [Fact]
    public void InvalidateAll_ShouldRemoveAllElements()
    {
        // Arrange
        var cache = new ElementCache();
        cache.Cache(CreateMockElement("test-1").Object);
        cache.Cache(CreateMockElement("test-2").Object);
        cache.Cache(CreateMockElement("test-3").Object);

        // Act
        cache.InvalidateAll();

        // Assert
        cache.Count.Should().Be(0);
    }

    [Fact]
    public void IsStale_ShouldReturnTrue_WhenExpired()
    {
        // Arrange
        var cache = new ElementCache
        {
            DefaultCacheDuration = TimeSpan.FromMilliseconds(50)
        };
        var element = CreateMockElement("test-123").Object;
        cache.Cache(element);

        // Act
        Thread.Sleep(100);
        var isStale = cache.IsStale(element);

        // Assert
        isStale.Should().BeTrue();
    }

    [Fact]
    public void IsStale_ShouldReturnFalse_WhenNotExpired()
    {
        // Arrange
        var cache = new ElementCache
        {
            DefaultCacheDuration = TimeSpan.FromSeconds(30)
        };
        var element = CreateMockElement("test-123").Object;
        cache.Cache(element);

        // Act
        var isStale = cache.IsStale(element);

        // Assert
        isStale.Should().BeFalse();
    }

    [Fact]
    public void Cache_ShouldRespectMaxCachedElements()
    {
        // Arrange
        var cache = new ElementCache
        {
            MaxCachedElements = 3
        };

        // Act
        cache.Cache(CreateMockElement("test-1").Object);
        cache.Cache(CreateMockElement("test-2").Object);
        cache.Cache(CreateMockElement("test-3").Object);
        cache.Cache(CreateMockElement("test-4").Object);

        // Assert
        cache.Count.Should().Be(3);
    }

    [Fact]
    public void Cache_ShouldUpdateExistingElement()
    {
        // Arrange
        var cache = new ElementCache();
        var element1 = CreateMockElement("test-123").Object;
        cache.Cache(element1);

        // Create new element with same ID
        var element2 = CreateMockElement("test-123").Object;

        // Act
        cache.Cache(element2);

        // Assert
        cache.Count.Should().Be(1);
    }

    [Fact]
    public void Cache_ShouldSupportCustomDuration()
    {
        // Arrange
        var cache = new ElementCache
        {
            DefaultCacheDuration = TimeSpan.FromSeconds(30)
        };
        var element = CreateMockElement("test-123").Object;

        // Act
        cache.Cache(element, TimeSpan.FromMilliseconds(50));
        Thread.Sleep(100);

        // Assert
        cache.GetCached("test-123").Should().BeNull();
    }

    [Fact]
    public void Cache_ShouldIgnoreNullRuntimeId()
    {
        // Arrange
        var cache = new ElementCache();
        var mock = new Mock<IUIElement>();
        mock.Setup(e => e.RuntimeId).Returns(string.Empty);

        // Act
        cache.Cache(mock.Object);

        // Assert
        cache.Count.Should().Be(0);
    }

    [Fact]
    public async Task RefreshAsync_ShouldRevalidateElement()
    {
        // Arrange
        var cache = new ElementCache();
        var element = CreateMockElement("test-123").Object;
        cache.Cache(element);

        // Act
        var refreshed = await cache.RefreshAsync(element);

        // Assert
        refreshed.Should().NotBeNull();
        cache.GetCached("test-123").Should().NotBeNull();
    }
}


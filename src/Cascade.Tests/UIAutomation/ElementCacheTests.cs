using System;
using System.Threading.Tasks;
using Cascade.Core;
using Cascade.Core.Session;
using Cascade.UIAutomation.Elements;
using FluentAssertions;
using Moq;
using Xunit;

namespace Cascade.Tests.UIAutomation;

public class ElementCacheTests
{
    [Fact]
    public async Task Cache_ReturnsElementUntilEntryExpires()
    {
        var cache = CreateCache();
        cache.DefaultCacheDuration = TimeSpan.FromMilliseconds(30);
        var element = CreateElement("runtime-1");

        cache.Cache(element);

        cache.GetCached("runtime-1").Should().Be(element);
        await Task.Delay(40);
        cache.GetCached("runtime-1").Should().BeNull();
    }

    [Fact]
    public async Task RefreshAsync_UsesProvidedRefresher()
    {
        var refreshedElement = CreateElement("runtime-2");
        var cache = CreateCache(_ => Task.FromResult<IUIElement?>(refreshedElement));

        var result = await cache.RefreshAsync(CreateElement("ignored"));

        result.Should().Be(refreshedElement);
    }

    [Fact]
    public void DisabledCache_SkipsStorageAndMarksStale()
    {
        var cache = CreateCache();
        cache.EnableCaching = false;
        var element = CreateElement("runtime-3");

        cache.Cache(element);

        cache.GetCached("runtime-3").Should().BeNull();
        cache.IsStale(element).Should().BeTrue();
    }

    private static ElementCache CreateCache(Func<IUIElement, Task<IUIElement?>>? refresher = null)
    {
        var handle = new SessionHandle
        {
            SessionId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            VirtualDesktopId = new IntPtr(1),
            UserProfilePath = "test",
            DesktopProfile = VirtualDesktopProfile.Default,
            AcquiredAt = DateTimeOffset.UtcNow
        };

        return new ElementCache(handle, refresher ?? (_ => Task.FromResult<IUIElement?>(null)));
    }

    private static IUIElement CreateElement(string runtimeId)
    {
        var mock = new Mock<IUIElement>();
        mock.SetupGet(e => e.RuntimeId).Returns(runtimeId);
        return mock.Object;
    }
}


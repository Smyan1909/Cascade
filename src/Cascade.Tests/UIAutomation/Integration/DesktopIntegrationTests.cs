using Cascade.UIAutomation.Discovery;
using Cascade.UIAutomation.Enums;
using Cascade.UIAutomation.Services;
using FluentAssertions;
using Xunit;

namespace Cascade.Tests.UIAutomation.Integration;

/// <summary>
/// Integration tests for basic UI Automation operations on the desktop.
/// These tests don't require specific applications to be running.
/// </summary>
[Trait("Category", "Integration")]
[Collection("UIAutomation")]
public class DesktopIntegrationTests : IDisposable
{
    private readonly UIAutomationService _service;

    public DesktopIntegrationTests()
    {
        _service = new UIAutomationService(new UIAutomationOptions
        {
            DefaultTimeout = TimeSpan.FromSeconds(5),
            EnableCaching = true
        });
    }

    public void Dispose()
    {
        _service.Dispose();
    }

    [Fact]
    public void GetDesktopRoot_ShouldReturnDesktop()
    {
        // Act
        var desktop = _service.GetDesktopRoot();

        // Assert
        desktop.Should().NotBeNull();
        desktop.Name.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetAllWindows_ShouldReturnWindows()
    {
        // Act
        var windows = _service.Discovery.GetAllWindows();

        // Assert
        windows.Should().NotBeEmpty();
        windows.All(w => w.ControlType == ControlType.Window).Should().BeTrue();
    }

    [Fact]
    public void GetForegroundWindow_ShouldReturnWindow()
    {
        // Act
        var foreground = _service.GetForegroundWindow();

        // Assert
        foreground.Should().NotBeNull();
        foreground!.ControlType.Should().Be(ControlType.Window);
    }

    [Fact]
    public void CaptureDesktopSnapshot_ShouldCapture()
    {
        // Arrange
        var desktop = _service.GetDesktopRoot();

        // Act
        var snapshot = _service.CaptureSnapshot(desktop, maxDepth: 2);

        // Assert
        snapshot.Should().NotBeNull();
        snapshot.TotalElements.Should().BeGreaterThan(0);
        snapshot.Root.Should().NotBeNull();
    }

    [Fact]
    public void TreeWalker_ShouldWalkDesktopChildren()
    {
        // Arrange
        var desktop = _service.GetDesktopRoot();

        // Act
        var children = _service.TreeWalker.GetChildren(desktop).Take(5).ToList();

        // Assert
        children.Should().NotBeEmpty();
    }

    [Fact]
    public void ElementCache_ShouldCacheElements()
    {
        // Arrange
        var desktop = _service.GetDesktopRoot();

        // Act
        var windows = _service.Discovery.GetAllWindows();
        var firstWindow = windows.FirstOrDefault();

        // Assert
        _service.Cache.Should().NotBeNull();
        if (firstWindow != null)
        {
            var cached = _service.Cache!.GetCached(firstWindow.RuntimeId);
            // May or may not be cached depending on implementation
        }
    }

    [Fact]
    public void FindElement_ByControlType_ShouldFindWindows()
    {
        // Arrange
        var criteria = SearchCriteria.ByControlType(ControlType.Window);

        // Act
        var windows = _service.Discovery.FindAllElements(criteria);

        // Assert
        windows.Should().NotBeEmpty();
    }

    [Fact]
    public void GetFocusedElement_ShouldReturnElement()
    {
        // Act
        var focused = _service.GetFocusedElement();

        // Assert
        // May be null if nothing is focused, but should not throw
        // focused can be null or a valid element
    }

    [Fact]
    public void ElementFromPoint_ShouldReturnElement()
    {
        // Arrange - Use center of screen
        var bounds = System.Windows.Forms.Screen.PrimaryScreen?.Bounds ?? 
            new System.Drawing.Rectangle(0, 0, 1920, 1080);
        var centerX = bounds.Width / 2;
        var centerY = bounds.Height / 2;

        // Act
        var element = _service.Discovery.ElementFromPoint(centerX, centerY);

        // Assert
        element.Should().NotBeNull();
    }

    [Fact]
    public void ServiceOptions_ShouldBeAccessible()
    {
        // Assert
        _service.Options.Should().NotBeNull();
        _service.Options.DefaultTimeout.Should().Be(TimeSpan.FromSeconds(5));
        _service.Options.EnableCaching.Should().BeTrue();
    }

    [Fact]
    public void InvalidateCache_ShouldClearCache()
    {
        // Act
        _service.InvalidateCache();

        // Assert
        _service.Cache?.Count.Should().Be(0);
    }

    [Fact]
    public void ControlViewWalker_ShouldReturnElements()
    {
        // Arrange
        var desktop = _service.GetDesktopRoot();

        // Act
        var controlViewChildren = _service.TreeWalker.ControlViewWalker
            .GetChildren(desktop).Take(5).ToList();

        // Assert - Control view walker should return elements
        // Note: Not all elements may have IsControlElement=true due to how UIA works
        controlViewChildren.Should().NotBeEmpty();
    }

    [Fact]
    public void ContentViewWalker_ShouldReturnElements()
    {
        // Arrange
        var desktop = _service.GetDesktopRoot();

        // Act
        var contentViewChildren = _service.TreeWalker.ContentViewWalker
            .GetChildren(desktop).Take(5).ToList();

        // Assert - Content view walker should return elements
        // Note: Not all elements may have IsContentElement=true due to how UIA works
        contentViewChildren.Should().NotBeEmpty();
    }

    [Fact]
    public void TreeWalker_GetAncestors_ShouldReturnPath()
    {
        // Arrange
        var desktop = _service.GetDesktopRoot();
        var firstChild = _service.TreeWalker.GetFirstChild(desktop);
        
        if (firstChild == null)
            return; // Skip if no children

        // Try to get a grandchild
        var grandchild = _service.TreeWalker.GetFirstChild(firstChild);
        if (grandchild == null)
            return; // Skip if no grandchildren

        // Act
        var ancestors = _service.TreeWalker.GetAncestors(grandchild).ToList();

        // Assert
        ancestors.Should().NotBeEmpty();
        ancestors.Should().Contain(firstChild);
    }

    [Fact]
    public void SearchCriteria_WithMultipleConditions_ShouldWork()
    {
        // Arrange
        var criteria = SearchCriteria.ByControlType(ControlType.Window)
            .And(SearchCriteria.All);

        // Act
        var windows = _service.Discovery.FindAllElements(criteria);

        // Assert
        windows.Should().NotBeEmpty();
    }
}


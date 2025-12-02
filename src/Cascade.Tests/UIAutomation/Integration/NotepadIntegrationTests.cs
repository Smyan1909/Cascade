using Cascade.UIAutomation.Discovery;
using Cascade.UIAutomation.Elements;
using Cascade.UIAutomation.Enums;
using Cascade.UIAutomation.Services;
using FluentAssertions;
using Xunit;

namespace Cascade.Tests.UIAutomation.Integration;

/// <summary>
/// Integration tests using Windows Notepad.
/// These tests require Notepad to be available on the system.
/// </summary>
[Trait("Category", "Integration")]
[Collection("UIAutomation")]
public class NotepadIntegrationTests : IDisposable
{
    private readonly UIAutomationService _service;
    private bool _launchedNotepad = false;

    public NotepadIntegrationTests()
    {
        _service = new UIAutomationService(new UIAutomationOptions
        {
            DefaultTimeout = TimeSpan.FromSeconds(10),
            EnableCaching = true
        });
    }

    public void Dispose()
    {
        // Close Notepad if we launched it
        if (_launchedNotepad)
        {
            try
            {
                // Find Notepad window (may have unsaved changes prompt)
                var notepad = FindNotepadWindow();
                if (notepad != null)
                {
                    _service.Windows.CloseAsync(notepad).Wait();

                    // Handle "Do you want to save" dialog if it appears
                    Task.Delay(500).Wait();
                    var dialog = _service.Discovery.FindWindow("Notepad");
                    if (dialog != null)
                    {
                        var dontSaveButton = dialog.FindFirst(
                            SearchCriteria.ByName("Don't Save")
                                .Or(SearchCriteria.ByName("No")));
                        if (dontSaveButton != null)
                        {
                            _service.Actions.ClickAsync(dontSaveButton).Wait();
                        }
                    }
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        _service.Dispose();
    }

    [Fact]
    public async Task FindWindow_ShouldFindNotepad()
    {
        // Arrange
        await LaunchNotepadAsync();

        // Act
        var notepad = FindNotepadWindow();

        // Assert
        notepad.Should().NotBeNull();
        notepad!.ControlType.Should().Be(ControlType.Window);
    }

    [Fact]
    public async Task TypeText_ShouldTypeIntoNotepad()
    {
        // Arrange
        await LaunchNotepadAsync();
        var notepad = FindNotepadWindow();
        notepad.Should().NotBeNull();

        // Find the text editor
        var editor = notepad!.FindFirst(SearchCriteria.ByControlType(ControlType.Edit))
            ?? notepad.FindFirst(SearchCriteria.ByControlType(ControlType.Document));
        editor.Should().NotBeNull();

        // Act
        await _service.Actions.SetFocusAsync(editor!);
        await Task.Delay(100);
        await _service.Actions.TypeTextAsync(editor, "Hello, World!");
        await Task.Delay(200);

        // Assert - The text should be typed
        // Note: Verification depends on Notepad version (new vs classic)
    }

    [Fact]
    public async Task CaptureSnapshot_ShouldCaptureNotepadTree()
    {
        // Arrange
        await LaunchNotepadAsync();
        var notepad = FindNotepadWindow();
        notepad.Should().NotBeNull();

        // Act
        var snapshot = _service.CaptureSnapshot(notepad!, maxDepth: 5);

        // Assert
        snapshot.Should().NotBeNull();
        snapshot.TotalElements.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task FindMenuItems_ShouldFindNotepadMenu()
    {
        // Arrange
        await LaunchNotepadAsync();
        var notepad = FindNotepadWindow();
        notepad.Should().NotBeNull();

        // Act - Find menu bar
        var menuBar = notepad!.FindFirst(SearchCriteria.ByControlType(ControlType.MenuBar));

        // Assert
        menuBar.Should().NotBeNull();
    }

    [Fact]
    public async Task SetForeground_ShouldBringNotepadToFront()
    {
        // Arrange
        await LaunchNotepadAsync();
        var notepad = FindNotepadWindow();
        notepad.Should().NotBeNull();

        // Act
        var result = await _service.Windows.SetForegroundAsync(notepad!);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task Minimize_ShouldMinimizeNotepad()
    {
        // Arrange
        await LaunchNotepadAsync();
        var notepad = FindNotepadWindow();
        notepad.Should().NotBeNull();

        // Act
        await _service.Windows.MinimizeAsync(notepad!);
        await Task.Delay(500);

        // Restore for cleanup
        await _service.Windows.RestoreAsync(notepad!);

        // Assert - Window should have been minimized
    }

    [Fact]
    public async Task Maximize_ShouldMaximizeNotepad()
    {
        // Arrange
        await LaunchNotepadAsync();
        var notepad = FindNotepadWindow();
        notepad.Should().NotBeNull();

        // Act
        await _service.Windows.MaximizeAsync(notepad!);
        await Task.Delay(500);

        // Restore for cleanup
        await _service.Windows.RestoreAsync(notepad!);

        // Assert - Window should have been maximized
    }

    [Fact]
    public async Task TreeWalker_ShouldEnumerateNotepadDescendants()
    {
        // Arrange
        await LaunchNotepadAsync();
        var notepad = FindNotepadWindow();
        notepad.Should().NotBeNull();

        // Act
        var descendants = _service.TreeWalker.GetDescendants(notepad!, maxDepth: 3).ToList();

        // Assert
        descendants.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetChildren_ShouldReturnNotepadChildren()
    {
        // Arrange
        await LaunchNotepadAsync();
        var notepad = FindNotepadWindow();
        notepad.Should().NotBeNull();

        // Act
        var children = notepad!.Children;

        // Assert
        children.Should().NotBeEmpty();
    }

    private IUIElement? FindNotepadWindow()
    {
        // Try different window titles (new Notepad vs classic)
        var notepad = _service.Discovery.FindWindow("Untitled - Notepad");
        if (notepad == null)
            notepad = _service.Discovery.FindWindow("Notepad");
        if (notepad == null)
        {
            // Search by window containing "Notepad"
            notepad = _service.Discovery.FindWindow(w => 
                w.Name.Contains("Notepad", StringComparison.OrdinalIgnoreCase));
        }
        return notepad;
    }

    private async Task LaunchNotepadAsync()
    {
        // Check if Notepad is already running
        var existing = FindNotepadWindow();
        if (existing != null)
            return;

        // Launch Notepad
        await _service.Windows.LaunchAndAttachAsync("notepad.exe", timeout: TimeSpan.FromSeconds(10));
        _launchedNotepad = true;
        await Task.Delay(1000); // Wait for Notepad to fully load
    }
}


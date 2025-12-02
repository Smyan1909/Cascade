using System.Drawing;
using Cascade.UIAutomation.Elements;
using Cascade.UIAutomation.Enums;
using Cascade.UIAutomation.TreeWalker;
using FluentAssertions;
using Xunit;

namespace Cascade.Tests.UIAutomation;

public class TreeSnapshotTests
{
    private static ElementSnapshot CreateSnapshot(
        string name = "",
        string automationId = "",
        ControlType controlType = ControlType.Unknown,
        List<ElementSnapshot>? children = null)
    {
        return new ElementSnapshot
        {
            RuntimeId = Guid.NewGuid().ToString(),
            Name = name,
            AutomationId = automationId,
            ControlType = controlType.ToString(),
            ControlTypeId = (int)controlType,
            Children = children ?? new List<ElementSnapshot>(),
            BoundingRectangle = new Rectangle(0, 0, 100, 50),
            IsEnabled = true
        };
    }

    [Fact]
    public void TreeSnapshot_ShouldHaveCorrectProperties()
    {
        // Arrange
        var root = CreateSnapshot("Root", controlType: ControlType.Window);
        var capturedAt = DateTime.UtcNow;

        // Act
        var snapshot = new TreeSnapshot(root, capturedAt, 1, 0);

        // Assert
        snapshot.Root.Should().NotBeNull();
        snapshot.Root.Name.Should().Be("Root");
        snapshot.CapturedAt.Should().Be(capturedAt);
        snapshot.TotalElements.Should().Be(1);
        snapshot.MaxDepth.Should().Be(0);
    }

    [Fact]
    public void FindByRuntimeId_ShouldFindElement()
    {
        // Arrange
        var child = CreateSnapshot("Child", controlType: ControlType.Button);
        var root = CreateSnapshot("Root", controlType: ControlType.Window, children: new() { child });
        var snapshot = new TreeSnapshot(root, DateTime.UtcNow, 2, 1);

        // Act
        var found = snapshot.FindByRuntimeId(child.RuntimeId);

        // Assert
        found.Should().NotBeNull();
        found!.Name.Should().Be("Child");
    }

    [Fact]
    public void FindByRuntimeId_ShouldReturnNull_WhenNotFound()
    {
        // Arrange
        var root = CreateSnapshot("Root", controlType: ControlType.Window);
        var snapshot = new TreeSnapshot(root, DateTime.UtcNow, 1, 0);

        // Act
        var found = snapshot.FindByRuntimeId("nonexistent");

        // Assert
        found.Should().BeNull();
    }

    [Fact]
    public void FindByAutomationId_ShouldFindElement()
    {
        // Arrange
        var child = CreateSnapshot("Button", automationId: "btn1", controlType: ControlType.Button);
        var root = CreateSnapshot("Root", controlType: ControlType.Window, children: new() { child });
        var snapshot = new TreeSnapshot(root, DateTime.UtcNow, 2, 1);

        // Act
        var found = snapshot.FindByAutomationId("btn1");

        // Assert
        found.Should().NotBeNull();
        found!.AutomationId.Should().Be("btn1");
    }

    [Fact]
    public void FindByControlType_ShouldFindAllMatchingElements()
    {
        // Arrange
        var button1 = CreateSnapshot("Button 1", controlType: ControlType.Button);
        var button2 = CreateSnapshot("Button 2", controlType: ControlType.Button);
        var text = CreateSnapshot("Text", controlType: ControlType.Text);
        var root = CreateSnapshot("Root", controlType: ControlType.Window, children: new() { button1, text, button2 });
        var snapshot = new TreeSnapshot(root, DateTime.UtcNow, 4, 1);

        // Act
        var buttons = snapshot.FindByControlType(ControlType.Button);

        // Assert
        buttons.Should().HaveCount(2);
        buttons.All(b => b.ControlType == ControlType.Button.ToString()).Should().BeTrue();
    }

    [Fact]
    public void FindAll_ShouldFindByPredicate()
    {
        // Arrange
        var child1 = CreateSnapshot("Test 1", controlType: ControlType.Button);
        var child2 = CreateSnapshot("Other", controlType: ControlType.Button);
        var child3 = CreateSnapshot("Test 2", controlType: ControlType.Button);
        var root = CreateSnapshot("Root", controlType: ControlType.Window, children: new() { child1, child2, child3 });
        var snapshot = new TreeSnapshot(root, DateTime.UtcNow, 4, 1);

        // Act
        var found = snapshot.FindAll(e => e.Name?.StartsWith("Test") == true);

        // Assert
        found.Should().HaveCount(2);
    }

    [Fact]
    public void GetAllElements_ShouldReturnFlatList()
    {
        // Arrange
        var grandchild = CreateSnapshot("Grandchild");
        var child = CreateSnapshot("Child", children: new() { grandchild });
        var root = CreateSnapshot("Root", children: new() { child });
        var snapshot = new TreeSnapshot(root, DateTime.UtcNow, 3, 2);

        // Act
        var all = snapshot.GetAllElements();

        // Assert
        all.Should().HaveCount(3);
        all.Select(e => e.Name).Should().Contain("Root");
        all.Select(e => e.Name).Should().Contain("Child");
        all.Select(e => e.Name).Should().Contain("Grandchild");
    }

    [Fact]
    public void ToJson_ShouldSerializeSnapshot()
    {
        // Arrange
        var root = CreateSnapshot("Root", automationId: "root", controlType: ControlType.Window);
        var snapshot = new TreeSnapshot(root, DateTime.UtcNow, 1, 0);

        // Act
        var json = snapshot.ToJson();

        // Assert
        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("Root");
        json.Should().Contain("Window");
    }

    [Fact]
    public void FromJson_ShouldDeserializeSnapshot()
    {
        // Arrange
        var root = CreateSnapshot("Root", automationId: "root", controlType: ControlType.Window);
        var original = new TreeSnapshot(root, DateTime.UtcNow, 1, 0);
        var json = original.ToJson();

        // Act
        var deserialized = TreeSnapshot.FromJson(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Root.Name.Should().Be("Root");
        deserialized.Root.AutomationId.Should().Be("root");
        deserialized.TotalElements.Should().Be(1);
    }

    [Fact]
    public void FindByControlType_ShouldSearchRecursively()
    {
        // Arrange
        var deepButton = CreateSnapshot("Deep Button", controlType: ControlType.Button);
        var pane = CreateSnapshot("Pane", controlType: ControlType.Pane, children: new() { deepButton });
        var root = CreateSnapshot("Root", controlType: ControlType.Window, children: new() { pane });
        var snapshot = new TreeSnapshot(root, DateTime.UtcNow, 3, 2);

        // Act
        var buttons = snapshot.FindByControlType(ControlType.Button);

        // Assert
        buttons.Should().HaveCount(1);
        buttons[0].Name.Should().Be("Deep Button");
    }
}


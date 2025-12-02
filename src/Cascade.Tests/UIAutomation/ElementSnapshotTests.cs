using System.Drawing;
using Cascade.UIAutomation.Elements;
using Cascade.UIAutomation.Enums;
using FluentAssertions;
using Xunit;

namespace Cascade.Tests.UIAutomation;

public class ElementSnapshotTests
{
    [Fact]
    public void ElementSnapshot_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var snapshot = new ElementSnapshot();

        // Assert
        snapshot.RuntimeId.Should().BeEmpty();
        snapshot.AutomationId.Should().BeNull();
        snapshot.Name.Should().BeNull();
        snapshot.ClassName.Should().BeNull();
        snapshot.ControlType.Should().BeEmpty();
        snapshot.Children.Should().BeEmpty();
        snapshot.SupportedPatterns.Should().BeEmpty();
        snapshot.Properties.Should().BeEmpty();
    }

    [Fact]
    public void ElementSnapshot_ShouldStoreProperties()
    {
        // Arrange & Act
        var snapshot = new ElementSnapshot
        {
            RuntimeId = "123.456.789",
            AutomationId = "btn1",
            Name = "OK Button",
            ClassName = "Button",
            ControlType = ControlType.Button.ToString(),
            ControlTypeId = (int)ControlType.Button,
            BoundingRectangle = new Rectangle(10, 20, 100, 50),
            IsEnabled = true,
            IsOffscreen = false,
            IsContentElement = true,
            IsControlElement = true,
            HasKeyboardFocus = false,
            ProcessId = 1234,
            Depth = 2,
            Value = "Test Value"
        };

        // Assert
        snapshot.RuntimeId.Should().Be("123.456.789");
        snapshot.AutomationId.Should().Be("btn1");
        snapshot.Name.Should().Be("OK Button");
        snapshot.ClassName.Should().Be("Button");
        snapshot.ControlType.Should().Be("Button");
        snapshot.ControlTypeId.Should().Be((int)ControlType.Button);
        snapshot.BoundingRectangle.Should().Be(new Rectangle(10, 20, 100, 50));
        snapshot.IsEnabled.Should().BeTrue();
        snapshot.IsOffscreen.Should().BeFalse();
        snapshot.IsContentElement.Should().BeTrue();
        snapshot.IsControlElement.Should().BeTrue();
        snapshot.HasKeyboardFocus.Should().BeFalse();
        snapshot.ProcessId.Should().Be(1234);
        snapshot.Depth.Should().Be(2);
        snapshot.Value.Should().Be("Test Value");
    }

    [Fact]
    public void ElementSnapshot_ShouldSupportChildren()
    {
        // Arrange
        var child1 = new ElementSnapshot { Name = "Child 1" };
        var child2 = new ElementSnapshot { Name = "Child 2" };

        // Act
        var parent = new ElementSnapshot
        {
            Name = "Parent",
            Children = new List<ElementSnapshot> { child1, child2 }
        };

        // Assert
        parent.Children.Should().HaveCount(2);
        parent.Children[0].Name.Should().Be("Child 1");
        parent.Children[1].Name.Should().Be("Child 2");
    }

    [Fact]
    public void ElementSnapshot_ShouldSupportPatterns()
    {
        // Arrange & Act
        var snapshot = new ElementSnapshot
        {
            SupportedPatterns = new List<string> { "Invoke", "Value", "Toggle" }
        };

        // Assert
        snapshot.SupportedPatterns.Should().HaveCount(3);
        snapshot.SupportedPatterns.Should().Contain("Invoke");
        snapshot.SupportedPatterns.Should().Contain("Value");
        snapshot.SupportedPatterns.Should().Contain("Toggle");
    }

    [Fact]
    public void ElementSnapshot_ShouldSupportProperties()
    {
        // Arrange & Act
        var snapshot = new ElementSnapshot
        {
            Properties = new Dictionary<string, string>
            {
                { "CustomProp1", "Value1" },
                { "CustomProp2", "Value2" }
            }
        };

        // Assert
        snapshot.Properties.Should().HaveCount(2);
        snapshot.Properties["CustomProp1"].Should().Be("Value1");
        snapshot.Properties["CustomProp2"].Should().Be("Value2");
    }

    [Fact]
    public void ToJson_ShouldSerializeSnapshot()
    {
        // Arrange
        var snapshot = new ElementSnapshot
        {
            RuntimeId = "test-123",
            Name = "Test Element",
            ControlType = ControlType.Button.ToString(),
            IsEnabled = true
        };

        // Act
        var json = snapshot.ToJson();

        // Assert
        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("test-123");
        json.Should().Contain("Test Element");
        json.Should().Contain("Button");
    }

    [Fact]
    public void ToJson_WithIndentation_ShouldBeFormatted()
    {
        // Arrange
        var snapshot = new ElementSnapshot
        {
            RuntimeId = "test-123",
            Name = "Test Element"
        };

        // Act
        var json = snapshot.ToJson(indented: true);

        // Assert
        json.Should().Contain("\n");
        json.Should().Contain("  "); // Indentation
    }

    [Fact]
    public void FromJson_ShouldDeserializeSnapshot()
    {
        // Arrange
        var original = new ElementSnapshot
        {
            RuntimeId = "test-123",
            Name = "Test Element",
            ControlType = ControlType.Button.ToString(),
            IsEnabled = true,
            BoundingRectangle = new Rectangle(10, 20, 100, 50)
        };
        var json = original.ToJson();

        // Act
        var deserialized = ElementSnapshot.FromJson(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.RuntimeId.Should().Be("test-123");
        deserialized.Name.Should().Be("Test Element");
        deserialized.ControlType.Should().Be("Button");
        deserialized.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void FromJson_WithChildren_ShouldDeserializeHierarchy()
    {
        // Arrange
        var child = new ElementSnapshot { Name = "Child" };
        var parent = new ElementSnapshot
        {
            Name = "Parent",
            Children = new List<ElementSnapshot> { child }
        };
        var json = parent.ToJson();

        // Act
        var deserialized = ElementSnapshot.FromJson(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Children.Should().HaveCount(1);
        deserialized.Children[0].Name.Should().Be("Child");
    }

    [Fact]
    public void ToString_ShouldReturnReadableRepresentation()
    {
        // Arrange
        var snapshot = new ElementSnapshot
        {
            Name = "OK Button",
            AutomationId = "btnOk",
            ControlType = ControlType.Button.ToString()
        };

        // Act
        var str = snapshot.ToString();

        // Assert
        str.Should().Contain("Button");
        str.Should().Contain("OK Button");
        str.Should().Contain("btnOk");
    }

    [Fact]
    public void ToString_WithNoName_ShouldShowPlaceholder()
    {
        // Arrange
        var snapshot = new ElementSnapshot
        {
            ControlType = ControlType.Button.ToString()
        };

        // Act
        var str = snapshot.ToString();

        // Assert
        str.Should().Contain("(no name)");
    }

    [Fact]
    public void BoundingRectangle_ShouldSerializeCorrectly()
    {
        // Arrange
        var original = new ElementSnapshot
        {
            BoundingRectangle = new Rectangle(100, 200, 300, 400)
        };

        // Act
        var json = original.ToJson();
        var deserialized = ElementSnapshot.FromJson(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.BoundingRectangle.X.Should().Be(100);
        deserialized.BoundingRectangle.Y.Should().Be(200);
        deserialized.BoundingRectangle.Width.Should().Be(300);
        deserialized.BoundingRectangle.Height.Should().Be(400);
    }
}


using Cascade.UIAutomation.Discovery;
using Cascade.UIAutomation.Elements;
using Cascade.UIAutomation.Enums;
using FluentAssertions;
using Moq;
using Xunit;

namespace Cascade.Tests.UIAutomation;

public class ElementLocatorTests
{
    private static Mock<IUIElement> CreateMockElement(
        string name = "",
        string automationId = "",
        ControlType controlType = ControlType.Unknown,
        string className = "",
        IReadOnlyList<IUIElement>? children = null)
    {
        var mock = new Mock<IUIElement>();
        mock.Setup(e => e.Name).Returns(name);
        mock.Setup(e => e.AutomationId).Returns(automationId);
        mock.Setup(e => e.ControlType).Returns(controlType);
        mock.Setup(e => e.ClassName).Returns(className);
        mock.Setup(e => e.RuntimeId).Returns(Guid.NewGuid().ToString());
        mock.Setup(e => e.Children).Returns(children ?? new List<IUIElement>());
        return mock;
    }

    [Fact]
    public void Parse_ShouldParseSimpleControlType()
    {
        // Arrange & Act
        var locator = ElementLocator.Parse("/Button");

        // Assert
        locator.ToString().Should().Be("/Button");
    }

    [Fact]
    public void Parse_ShouldParseAttributeEquals()
    {
        // Arrange & Act
        var locator = ElementLocator.Parse("/Button[@Name='OK']");

        // Assert
        locator.ToString().Should().Contain("@Name='OK'");
    }

    [Fact]
    public void Parse_ShouldParseDescendantSearch()
    {
        // Arrange & Act
        var locator = ElementLocator.Parse("//Button[@Name='OK']");

        // Assert
        locator.ToString().Should().StartWith("//");
    }

    [Fact]
    public void Parse_ShouldParseContainsFunction()
    {
        // Arrange & Act
        var locator = ElementLocator.Parse("//Button[contains(@Name, 'Submit')]");

        // Assert
        locator.ToString().Should().Contain("contains(@Name, 'Submit')");
    }

    [Fact]
    public void Parse_ShouldParseStartsWithFunction()
    {
        // Arrange & Act
        var locator = ElementLocator.Parse("//Edit[starts-with(@AutomationId, 'txt')]");

        // Assert
        locator.ToString().Should().Contain("starts-with");
    }

    [Fact]
    public void Parse_ShouldParseIndex()
    {
        // Arrange & Act
        var locator = ElementLocator.Parse("/Window/Button[1]");

        // Assert
        locator.ToString().Should().Contain("[1]");
    }

    [Fact]
    public void Parse_ShouldParseMultiplePredicates()
    {
        // Arrange & Act
        var locator = ElementLocator.Parse("/Button[@Name='OK'][@AutomationId='btnOk']");

        // Assert
        locator.ToString().Should().Contain("@Name='OK'");
        locator.ToString().Should().Contain("@AutomationId='btnOk'");
    }

    [Fact]
    public void Parse_ShouldParseMultipleSteps()
    {
        // Arrange & Act
        var locator = ElementLocator.Parse("/Window[@Name='Calculator']/Button[@AutomationId='num1']");

        // Assert
        locator.ToString().Should().Contain("/Window");
        locator.ToString().Should().Contain("/Button");
    }

    [Fact]
    public void Parse_ShouldThrowOnEmptyLocator()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => ElementLocator.Parse(""));
    }

    [Fact]
    public void Parse_ShouldThrowOnUnmatchedBracket()
    {
        // Arrange & Act & Assert
        Assert.Throws<FormatException>(() => ElementLocator.Parse("/Button[@Name='OK'"));
    }

    [Fact]
    public void Find_ShouldReturnMatchingElement()
    {
        // Arrange
        var button = CreateMockElement(name: "OK", controlType: ControlType.Button);
        var window = CreateMockElement(
            name: "Test",
            controlType: ControlType.Window,
            children: new[] { button.Object });

        var locator = ElementLocator.Parse("/Button[@Name='OK']");

        // Act
        var result = locator.Find(window.Object);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("OK");
    }

    [Fact]
    public void Find_ShouldReturnNull_WhenNoMatch()
    {
        // Arrange
        var button = CreateMockElement(name: "Cancel", controlType: ControlType.Button);
        var window = CreateMockElement(
            name: "Test",
            controlType: ControlType.Window,
            children: new[] { button.Object });

        var locator = ElementLocator.Parse("/Button[@Name='OK']");

        // Act
        var result = locator.Find(window.Object);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void FindAll_ShouldReturnAllMatchingElements()
    {
        // Arrange
        var button1 = CreateMockElement(name: "Button 1", controlType: ControlType.Button);
        var button2 = CreateMockElement(name: "Button 2", controlType: ControlType.Button);
        var text = CreateMockElement(name: "Text", controlType: ControlType.Text);
        var window = CreateMockElement(
            name: "Test",
            controlType: ControlType.Window,
            children: new[] { button1.Object, text.Object, button2.Object });

        var locator = ElementLocator.Parse("/Button");

        // Act
        var results = locator.FindAll(window.Object);

        // Assert
        results.Should().HaveCount(2);
        results.All(e => e.ControlType == ControlType.Button).Should().BeTrue();
    }

    [Fact]
    public void Find_ShouldWorkWithContains()
    {
        // Arrange
        var button = CreateMockElement(name: "Submit Form", controlType: ControlType.Button);
        var window = CreateMockElement(
            name: "Test",
            controlType: ControlType.Window,
            children: new[] { button.Object });

        var locator = ElementLocator.Parse("/Button[contains(@Name, 'Submit')]");

        // Act
        var result = locator.Find(window.Object);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Contain("Submit");
    }

    [Fact]
    public void Find_ShouldWorkWithIndex()
    {
        // Arrange
        var button1 = CreateMockElement(name: "Button 1", controlType: ControlType.Button);
        var button2 = CreateMockElement(name: "Button 2", controlType: ControlType.Button);
        var window = CreateMockElement(
            name: "Test",
            controlType: ControlType.Window,
            children: new[] { button1.Object, button2.Object });

        var locator = ElementLocator.Parse("/Button[2]");

        // Act
        var result = locator.Find(window.Object);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Button 2");
    }

    [Fact]
    public void Find_ShouldSearchDescendants()
    {
        // Arrange
        var button = CreateMockElement(name: "Deep Button", controlType: ControlType.Button);
        var pane = CreateMockElement(
            name: "Pane",
            controlType: ControlType.Pane,
            children: new[] { button.Object });
        var window = CreateMockElement(
            name: "Test",
            controlType: ControlType.Window,
            children: new[] { pane.Object });

        var locator = ElementLocator.Parse("//Button[@Name='Deep Button']");

        // Act
        var result = locator.Find(window.Object);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Deep Button");
    }
}


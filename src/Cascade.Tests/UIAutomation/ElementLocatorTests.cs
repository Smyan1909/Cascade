using Cascade.Tests.UIAutomation.Fakes;
using Cascade.UIAutomation.Discovery;
using FluentAssertions;
using Xunit;

namespace Cascade.Tests.UIAutomation;

public class ElementLocatorTests
{
    [Fact]
    public void FindAll_WithContainsFilter_ReturnsMatchingElements()
    {
        var root = BuildWindow();
        var locator = ElementLocator.Parse("//Button[contains(@Name,'Submit')]");

        var matches = locator.FindAll(root);

        matches.Should().ContainSingle();
        matches[0].AutomationId.Should().Be("submit");
    }

    [Fact]
    public void Find_WithIndexFilter_ReturnsFirstEditElement()
    {
        var window = BuildWindowWithInputs();
        var locator = ElementLocator.Parse("/Window/Pane/Edit[@ClassName='TextBox'][1]");

        var match = locator.Find(window);

        match.Should().NotBeNull();
        match!.AutomationId.Should().Be("input1");
    }

    [Fact]
    public void Find_WithMatchAnywhere_ReturnsNestedElement()
    {
        var root = BuildWindow();
        var locator = ElementLocator.Parse("//Button[@AutomationId='cancel']");

        var match = locator.Find(root);

        match.Should().NotBeNull();
        match!.AutomationId.Should().Be("cancel");
    }

    private static FakeUIElement BuildWindow()
    {
        var submit = new FakeUIElement("Button", automationId: "submit", name: "Submit Form");
        var cancel = new FakeUIElement("Button", automationId: "cancel", name: "Cancel");
        var pane = new FakeUIElement("Pane");
        pane.AddChild(submit);
        pane.AddChild(cancel);
        var window = new FakeUIElement("Window");
        window.AddChild(pane);
        return window;
    }

    private static FakeUIElement BuildWindowWithInputs()
    {
        var first = new FakeUIElement("Edit", automationId: "input1", className: "TextBox");
        var second = new FakeUIElement("Edit", automationId: "input2", className: "TextBox");
        var pane = new FakeUIElement("Pane");
        pane.AddChild(first);
        pane.AddChild(second);
        var window = new FakeUIElement("Window");
        window.AddChild(pane);
        return window;
    }
}


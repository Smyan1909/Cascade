using Cascade.CodeGen.Generation;
using Cascade.CodeGen.Templates;
using Cascade.UIAutomation.Discovery;
using FluentAssertions;
using Xunit;

namespace Cascade.Tests.CodeGen;

public class CodeGeneratorTests
{
    private readonly ITemplateEngine _templateEngine;
    private readonly ActionCodeGenerator _actionGenerator;

    public CodeGeneratorTests()
    {
        _templateEngine = new ScribanTemplateEngine();
        _actionGenerator = new ActionCodeGenerator(_templateEngine);
    }

    [Fact]
    public async Task GenerateActionAsync_ShouldGenerateCode()
    {
        // Arrange
        var action = new ActionDefinition
        {
            Name = "ClickButton",
            Type = ActionType.Click,
            TargetElement = ElementLocator.Parse("/Window[@Name='Test']/Button[@Name='Submit']")
        };

        // Act
        var result = await _actionGenerator.GenerateActionAsync(action);

        // Assert
        result.Should().NotBeNull();
        result.SourceCode.Should().NotBeNullOrEmpty();
        result.SourceCode.Should().Contain("ClickButton");
        result.Namespace.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GenerateActionsAsync_ShouldGenerateCodeForMultipleActions()
    {
        // Arrange
        var actions = new[]
        {
            new ActionDefinition
            {
                Name = "Action1",
                Type = ActionType.Click,
                TargetElement = ElementLocator.Parse("/Window/Button[@Name='Button1']")
            },
            new ActionDefinition
            {
                Name = "Action2",
                Type = ActionType.Type,
                TargetElement = ElementLocator.Parse("/Window/Edit[@Name='Input']"),
                Parameters = new Dictionary<string, object> { ["text"] = "test" }
            }
        };

        // Act
        var result = await _actionGenerator.GenerateActionsAsync(actions);

        // Assert
        result.Should().NotBeNull();
        result.SourceCode.Should().Contain("Action1");
        result.SourceCode.Should().Contain("Action2");
    }
}


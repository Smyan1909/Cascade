using Cascade.CodeGen.Templates;
using FluentAssertions;
using Xunit;

namespace Cascade.Tests.CodeGen;

public class TemplateEngineTests
{
    private readonly ITemplateEngine _templateEngine;

    public TemplateEngineTests()
    {
        _templateEngine = new ScribanTemplateEngine();
    }

    [Fact]
    public void RenderInline_ShouldRenderSimpleTemplate()
    {
        // Arrange
        var template = "Hello {{ name }}!";
        var model = new { name = "World" };

        // Act
        var result = _templateEngine.RenderInline(template, model);

        // Assert
        result.Should().Be("Hello World!");
    }

    [Fact]
    public void RenderInline_ShouldHandleVariables()
    {
        // Arrange
        var template = "Value: {{ value }}";
        var model = new Dictionary<string, object> { ["value"] = 42 };

        // Act
        var result = _templateEngine.RenderInline(template, model);

        // Assert
        result.Should().Be("Value: 42");
    }

    [Fact]
    public async Task RenderAsync_ShouldRenderWithContext()
    {
        // Arrange
        _templateEngine.RegisterTemplate("TestTemplate", "Namespace: {{ namespace }}, Class: {{ class_name }}");
        var context = new TemplateContext
        {
            Namespace = "Test.Namespace",
            ClassName = "TestClass"
        };

        // Act
        var result = await _templateEngine.RenderAsync("TestTemplate", context);

        // Assert
        result.Should().Contain("Test.Namespace");
        result.Should().Contain("TestClass");
    }

    [Fact]
    public void ValidateTemplate_ShouldReturnValid_ForValidTemplate()
    {
        // Arrange
        var template = "Hello {{ name }}";

        // Act
        var result = _templateEngine.ValidateTemplate(template);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void HasTemplate_ShouldReturnTrue_WhenTemplateExists()
    {
        // Arrange
        _templateEngine.RegisterTemplate("MyTemplate", "Content");

        // Act
        var result = _templateEngine.HasTemplate("MyTemplate");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void GetTemplateNames_ShouldReturnRegisteredTemplates()
    {
        // Arrange
        _templateEngine.RegisterTemplate("Template1", "Content1");
        _templateEngine.RegisterTemplate("Template2", "Content2");

        // Act
        var names = _templateEngine.GetTemplateNames();

        // Assert
        names.Should().Contain("Template1");
        names.Should().Contain("Template2");
    }
}


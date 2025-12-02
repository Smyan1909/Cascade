using Cascade.CodeGen.Compiler;
using Cascade.CodeGen.Execution;
using Cascade.CodeGen.Generation;
using Cascade.CodeGen.Services;
using Cascade.CodeGen.Templates;
using Cascade.UIAutomation.Discovery;
using FluentAssertions;
using Xunit;

namespace Cascade.Tests.CodeGen.Integration;

public class FullPipelineTests
{
    [Fact]
    public async Task Generate_Compile_Execute_ShouldWorkEndToEnd()
    {
        // Arrange
        var templateEngine = new ScribanTemplateEngine();
        var compiler = new RoslynCompiler();
        var executor = new SandboxedExecutor(compiler);
        var actionGenerator = new ActionCodeGenerator(templateEngine);

        var action = new ActionDefinition
        {
            Name = "TestAction",
            Type = ActionType.Click,
            TargetElement = ElementLocator.Parse("/Window[@Name='Test']/Button[@Name='TestButton']")
        };

        // Act - Generate
        var generatedCode = await actionGenerator.GenerateActionAsync(action);

        // Assert - Generation
        generatedCode.SourceCode.Should().NotBeNullOrEmpty();

        // Act - Compile
        var compilation = await compiler.CompileAsync(generatedCode.SourceCode);

        // Assert - Compilation
        if (!compilation.Success)
        {
            var errors = string.Join("\n", compilation.Errors.Select(e => $"{e.Line}:{e.Column} {e.Message}"));
            throw new InvalidOperationException($"Compilation failed:\n{errors}\n\nGenerated code:\n{generatedCode.SourceCode}");
        }
        compilation.Success.Should().BeTrue("Compilation should succeed for generated code");
        compilation.Assembly.Should().NotBeNull();

        // Note: Execution would require mocked UI automation services
        // This demonstrates the pipeline works up to compilation
    }

    [Fact]
    public async Task CodeGenService_ShouldGenerateAndCompile()
    {
        // Arrange
        var options = new CodeGenOptions();
        var service = new CodeGenService(options);

        var action = new ActionDefinition
        {
            Name = "ServiceTest",
            Type = ActionType.Click,
            TargetElement = ElementLocator.Parse("/Window/Button")
        };

        // Act
        var compilation = await service.GenerateAndCompileActionAsync(action);

        // Assert
        if (!compilation.Success)
        {
            var errors = string.Join("\n", compilation.Errors.Select(e => $"{e.Line}:{e.Column} {e.Message}"));
            throw new InvalidOperationException($"Compilation failed:\n{errors}");
        }
        compilation.Success.Should().BeTrue();
    }
}


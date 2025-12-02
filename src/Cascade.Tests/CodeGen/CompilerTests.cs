using Cascade.CodeGen.Compiler;
using FluentAssertions;
using Xunit;

namespace Cascade.Tests.CodeGen;

public class CompilerTests
{
    private readonly IScriptCompiler _compiler;

    public CompilerTests()
    {
        _compiler = new RoslynCompiler();
    }

    [Fact]
    public async Task CompileAsync_ShouldSucceed_WithValidCode()
    {
        // Arrange
        var sourceCode = @"
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public string GetMessage()
        {
            return ""Hello"";
        }
    }
}";

        // Act
        var result = await _compiler.CompileAsync(sourceCode);

        // Assert
        result.Success.Should().BeTrue();
        result.Assembly.Should().NotBeNull();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task CompileAsync_ShouldFail_WithInvalidCode()
    {
        // Arrange
        var sourceCode = @"
public class TestClass
{
    public void InvalidMethod()
    {
        NonExistentMethod(); // Error
    }
}";

        // Act
        var result = await _compiler.CompileAsync(sourceCode);

        // Assert
        result.Success.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void IsValidSyntax_ShouldReturnTrue_ForValidSyntax()
    {
        // Arrange
        var sourceCode = "var x = 42;";

        // Act
        var isValid = _compiler.IsValidSyntax(sourceCode);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void IsValidSyntax_ShouldReturnFalse_ForInvalidSyntax()
    {
        // Arrange
        var sourceCode = "var x = ; // Missing value";

        // Act
        var isValid = _compiler.IsValidSyntax(sourceCode);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public async Task CheckSyntaxAsync_ShouldReturnDiagnostics()
    {
        // Arrange
        var sourceCode = "var x = ;";

        // Act
        var diagnostics = await _compiler.CheckSyntaxAsync(sourceCode);

        // Assert
        diagnostics.Should().NotBeEmpty();
    }
}


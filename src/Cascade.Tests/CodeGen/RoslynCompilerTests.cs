using Cascade.CodeGen.Compilation;
using FluentAssertions;
using Xunit;

namespace Cascade.Tests.CodeGen;

public class RoslynCompilerTests
{
    [Fact]
    public async Task CompileAsync_ReturnsSuccessForValidCode()
    {
        var compiler = new RoslynCompiler();
        var source = """
        using System.Threading.Tasks;

        public class TestScript
        {
            public Task ExecuteAsync() => Task.CompletedTask;
        }
        """;

        var result = await compiler.CompileAsync(source);

        result.Success.Should().BeTrue(result.Errors.FirstOrDefault()?.Message);
        result.AssemblyBytes.Should().NotBeNull();
    }

    [Fact]
    public async Task CompileAsync_ReturnsErrorsForInvalidCode()
    {
        var compiler = new RoslynCompiler();
        var source = "public class {";

        var result = await compiler.CompileAsync(source);

        result.Success.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }
}


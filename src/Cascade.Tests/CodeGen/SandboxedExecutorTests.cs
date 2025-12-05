using Cascade.CodeGen.Compilation;
using Cascade.CodeGen.Execution;
using Cascade.UIAutomation.Discovery;
using Cascade.UIAutomation.Elements;
using Cascade.UIAutomation.Session;
using FluentAssertions;
using Moq;
using Xunit;
using ExecutionContext = Cascade.CodeGen.Execution.ExecutionContext;

namespace Cascade.Tests.CodeGen;

public class SandboxedExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_RunsCompiledScript()
    {
        var compiler = new RoslynCompiler();
        var executor = new SandboxedExecutor();
        var source = """
        using System.Threading;
        using System.Threading.Tasks;

        public class TestScript
        {
            public Task ExecuteAsync(CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }
        """;

        var compilation = await compiler.CompileAsync(source);
        var callContext = new AutomationCallContext(
            new Cascade.Core.Session.SessionHandle
            {
                SessionId = Guid.NewGuid(),
                RunId = Guid.NewGuid()
            },
            VirtualInputProfile.Balanced,
            Guid.NewGuid(),
            CancellationToken.None);

        var executionContext = new ExecutionContext
        {
            ElementDiscovery = Mock.Of<IElementDiscovery>(),
            ActionExecutor = Mock.Of<IGeneratedActionExecutor>(),
            CancellationToken = CancellationToken.None
        };

        var result = await executor.ExecuteAsync(
            compilation,
            "TestScript",
            "ExecuteAsync",
            callContext,
            executionContext);

        result.Success.Should().BeTrue();
    }
}


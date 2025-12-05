using Cascade.CodeGen;
using Cascade.CodeGen.Compilation;
using Cascade.CodeGen.Execution;
using Cascade.CodeGen.Generation;
using Cascade.CodeGen.Services;
using Cascade.Database.Entities;
using Cascade.Database.Enums;
using Cascade.Database.Repositories;
using Cascade.UIAutomation.Session;
using FluentAssertions;
using Moq;
using Xunit;
using ExecutionContext = Cascade.CodeGen.Execution.ExecutionContext;

namespace Cascade.Tests.CodeGen;

public class CodeGenServiceTests
{
    [Fact]
    public async Task ExecuteAsync_CompilesOnceAndUsesCache()
    {
        var generator = new Mock<ICodeGenerator>();
        var compiler = new Mock<IScriptCompiler>();
        var executor = new Mock<IScriptExecutor>();
        var repository = new Mock<IScriptRepository>();
        var options = new CodeGenOptions { CacheCompilations = true, MaxCachedCompilations = 4 };
        var service = new CodeGenService(generator.Object, compiler.Object, executor.Object, repository.Object, options);
        var scriptId = Guid.NewGuid();
        var script = new Script
        {
            Id = scriptId,
            Name = "TestScript",
            SourceCode = "public class Test { public void ExecuteAsync(){} }",
            Type = ScriptType.Action,
            CurrentVersion = "1.0.0",
            TypeName = "Generated.Test",
            MethodName = "ExecuteAsync",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        repository.Setup(r => r.GetByIdAsync(scriptId)).ReturnsAsync(script);
        repository.Setup(r => r.GetCompiledAssemblyAsync(scriptId, script.CurrentVersion)).ReturnsAsync((byte[]?)null);
        repository.Setup(r => r.SaveCompiledAssemblyAsync(scriptId, script.CurrentVersion, It.IsAny<byte[]>())).Returns(Task.CompletedTask);

        var compilation = new CompilationResult { Success = true, AssemblyBytes = new byte[] { 1, 2, 3 } };
        compiler.Setup(c => c.CompileAsync(script.SourceCode, It.IsAny<CompilationOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(compilation);

        executor.Setup(e => e.ExecuteAsync(
            compilation,
            script.TypeName!,
            script.MethodName!,
            It.IsAny<AutomationCallContext>(),
            It.IsAny<ExecutionContext>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExecutionResult { Success = true });

        var callContext = new AutomationCallContext(
            new Cascade.Core.Session.SessionHandle { SessionId = Guid.NewGuid(), RunId = Guid.NewGuid() },
            VirtualInputProfile.Balanced,
            Guid.NewGuid(),
            CancellationToken.None);

        var execContext = new ExecutionContext
        {
            ElementDiscovery = Mock.Of<Cascade.UIAutomation.Discovery.IElementDiscovery>(),
            ActionExecutor = Mock.Of<IGeneratedActionExecutor>(),
            CancellationToken = CancellationToken.None
        };

        var first = await service.ExecuteAsync(scriptId, callContext, execContext);
        var second = await service.ExecuteAsync(scriptId, callContext, execContext);

        first.Success.Should().BeTrue();
        second.Success.Should().BeTrue();
        compiler.Verify(c => c.CompileAsync(script.SourceCode, It.IsAny<CompilationOptions?>(), It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(r => r.SaveCompiledAssemblyAsync(scriptId, script.CurrentVersion, It.IsAny<byte[]>()), Times.Once);
        executor.Verify(e => e.ExecuteAsync(
            compilation,
            script.TypeName!,
            script.MethodName!,
            It.IsAny<AutomationCallContext>(),
            It.IsAny<ExecutionContext>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task SaveGeneratedScriptAsync_UsesMetadataForTypeName()
    {
        var generator = new Mock<ICodeGenerator>();
        var compiler = new Mock<IScriptCompiler>();
        var executor = new Mock<IScriptExecutor>();
        var repository = new Mock<IScriptRepository>();
        var options = new CodeGenOptions();
        var service = new CodeGenService(generator.Object, compiler.Object, executor.Object, repository.Object, options);
        var generated = new GeneratedCode
        {
            SourceCode = "// test",
            Namespace = "Cascade.Generated",
            Metadata = new CodeGenerationMetadata
            {
                Parameters = new Dictionary<string, object>
                {
                    { "className", "MyAction" },
                    { "entryMethod", "RunAsync" }
                }
            }
        };

        Script? savedScript = null;
        repository.Setup(r => r.SaveAsync(It.IsAny<Script>())).Returns<Script>(s =>
        {
            savedScript = s;
            return Task.FromResult(s);
        });

        var script = await service.SaveGeneratedScriptAsync("name", "desc", ScriptType.Action, generated);

        script.TypeName.Should().Be("Cascade.Generated.MyAction");
        script.MethodName.Should().Be("RunAsync");
        script.SourceCode.Should().Be("// test");
        savedScript.Should().NotBeNull();
    }
}



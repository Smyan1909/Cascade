using Cascade.CodeGen;
using Cascade.CodeGen.Generation;
using Cascade.CodeGen.Services;
using Cascade.CodeGen.Templates;
using FluentAssertions;
using Xunit;

namespace Cascade.Tests.CodeGen;

public class CodeGeneratorTests
{
    [Fact]
    public async Task GenerateActionAsync_ProducesMethodPerAction()
    {
        var registry = new TemplateRegistry();
        registry.RegisterFromAssembly(typeof(CodeGenerator).Assembly, "Cascade.CodeGen.Templates.BuiltIn.");
        var engine = new ScribanTemplateEngine(registry);
        var factory = new TemplateContextFactory(new CodeGenOptions());
        var generator = new CodeGenerator(engine, factory, new CodeGenOptions());

        var action = new ActionDefinition
        {
            Name = "ClickSubmit",
            Description = "Clicks the submit button",
            Type = ActionType.Click,
            TargetElement = new ElementLocator
            {
                AutomationId = "submit"
            }
        };

        var result = await generator.GenerateActionAsync(action);

        result.SourceCode.Should().Contain("public async Task ClickSubmitAsync");
        result.Metadata.Parameters.Should().ContainKey("entryMethod");
        result.Metadata.Parameters["entryMethod"].Should().Be("ClickSubmitAsync");
    }
}


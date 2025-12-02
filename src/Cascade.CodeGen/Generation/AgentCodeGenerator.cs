using Cascade.CodeGen.Templates;

namespace Cascade.CodeGen.Generation;

/// <summary>
/// Generates code for agent classes.
/// </summary>
public class AgentCodeGenerator : ICodeGenerator
{
    private readonly ITemplateEngine _templateEngine;
    private readonly ActionCodeGenerator _actionGenerator;
    private readonly string _defaultNamespace;

    /// <summary>
    /// Creates a new agent code generator.
    /// </summary>
    public AgentCodeGenerator(ITemplateEngine templateEngine, string defaultNamespace = "Cascade.Generated")
    {
        _templateEngine = templateEngine ?? throw new ArgumentNullException(nameof(templateEngine));
        _actionGenerator = new ActionCodeGenerator(templateEngine, defaultNamespace);
        _defaultNamespace = defaultNamespace;
    }

    /// <inheritdoc />
    public Task<GeneratedCode> GenerateActionAsync(ActionDefinition action)
    {
        return _actionGenerator.GenerateActionAsync(action);
    }

    /// <inheritdoc />
    public Task<GeneratedCode> GenerateActionsAsync(IEnumerable<ActionDefinition> actions)
    {
        return _actionGenerator.GenerateActionsAsync(actions);
    }

    /// <inheritdoc />
    public Task<GeneratedCode> GenerateWorkflowAsync(WorkflowDefinition workflow)
    {
        throw new NotImplementedException("Use WorkflowGenerator for workflow generation");
    }

    /// <inheritdoc />
    public async Task<GeneratedCode> GenerateAgentAsync(AgentDefinition agent)
    {
        if (agent == null)
            throw new ArgumentNullException(nameof(agent));

        var actionsData = agent.Actions.Select(a => new
        {
            name = ToPascalCase(a.Name ?? "Action"),
            code = _actionGenerator.GenerateActionCode(a)
        }).ToList();

        var context = new TemplateContext
        {
            Namespace = _defaultNamespace,
            ClassName = agent.Name ?? "GeneratedAgent",
            Variables = new Dictionary<string, object>
            {
                ["agent"] = new
                {
                    name = agent.Name ?? "Agent",
                    description = agent.Description ?? "",
                    capabilities = agent.Capabilities ?? Array.Empty<string>(),
                    target_application = agent.TargetApplication ?? "",
                    actions = actionsData
                }
            }
        };

        var sourceCode = await _templateEngine.RenderAsync("AgentClass", context);

        return new GeneratedCode
        {
            SourceCode = sourceCode,
            FileName = $"{agent.Name ?? "Agent"}.cs",
            Namespace = _defaultNamespace,
            RequiredUsings = new[]
            {
                "Cascade.UIAutomation",
                "Cascade.UIAutomation.Discovery",
                "Cascade.Core",
                "System.Threading.Tasks",
                "System.Collections.Generic"
            },
            RequiredReferences = Array.Empty<string>(),
            Metadata = new CodeGenerationMetadata
            {
                GeneratedAt = DateTime.UtcNow,
                GeneratorVersion = "1.0.0",
                TemplateUsed = "AgentClass",
                Parameters = new Dictionary<string, object>
                {
                    ["actionCount"] = agent.Actions.Count,
                    ["capabilityCount"] = agent.Capabilities.Count
                }
            }
        };
    }

    /// <inheritdoc />
    public Task<string> OptimizeAsync(string sourceCode)
    {
        return Task.FromResult(CodeOptimizer.Optimize(sourceCode));
    }

    private static string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
        if (input.Length == 1)
            return input.ToUpperInvariant();
        return char.ToUpperInvariant(input[0]) + input.Substring(1);
    }
}


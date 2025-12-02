using Cascade.CodeGen.Templates;

namespace Cascade.CodeGen.Generation;

/// <summary>
/// Generates code for workflows.
/// </summary>
public class WorkflowGenerator : ICodeGenerator
{
    private readonly ITemplateEngine _templateEngine;
    private readonly ActionCodeGenerator _actionGenerator;
    private readonly string _defaultNamespace;

    /// <summary>
    /// Creates a new workflow generator.
    /// </summary>
    public WorkflowGenerator(ITemplateEngine templateEngine, string defaultNamespace = "Cascade.Generated")
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
    public async Task<GeneratedCode> GenerateWorkflowAsync(WorkflowDefinition workflow)
    {
        if (workflow == null)
            throw new ArgumentNullException(nameof(workflow));

        var stepsData = workflow.Steps
            .OrderBy(s => s.Order)
            .Select((step, index) => new
            {
                index = index,
                description = step.Description ?? step.Name,
                code = _actionGenerator.GenerateActionCode(step.Action),
                delay_after = step.DelayAfter.HasValue ? (int)step.DelayAfter.Value.TotalMilliseconds : (int?)null
            }).ToList();

        var context = new TemplateContext
        {
            Namespace = _defaultNamespace,
            ClassName = workflow.Name ?? "GeneratedWorkflow",
            Variables = new Dictionary<string, object>
            {
                ["workflow"] = new
                {
                    name = workflow.Name ?? "Workflow",
                    description = workflow.Description ?? "",
                    steps = stepsData
                }
            }
        };

        var sourceCode = await _templateEngine.RenderAsync("WorkflowScript", context);

        return new GeneratedCode
        {
            SourceCode = sourceCode,
            FileName = $"{workflow.Name ?? "Workflow"}.cs",
            Namespace = _defaultNamespace,
            RequiredUsings = new[]
            {
                "Cascade.UIAutomation",
                "Cascade.UIAutomation.Discovery",
                "Cascade.Core",
                "System.Threading.Tasks"
            },
            RequiredReferences = Array.Empty<string>(),
            Metadata = new CodeGenerationMetadata
            {
                GeneratedAt = DateTime.UtcNow,
                GeneratorVersion = "1.0.0",
                TemplateUsed = "WorkflowScript",
                Parameters = new Dictionary<string, object>
                {
                    ["stepCount"] = workflow.Steps.Count
                }
            }
        };
    }

    /// <inheritdoc />
    public Task<GeneratedCode> GenerateAgentAsync(AgentDefinition agent)
    {
        throw new NotImplementedException("Use AgentCodeGenerator for agent generation");
    }

    /// <inheritdoc />
    public Task<string> OptimizeAsync(string sourceCode)
    {
        return Task.FromResult(CodeOptimizer.Optimize(sourceCode));
    }
}


using Cascade.CodeGen.Templates;

namespace Cascade.CodeGen.Generation;

/// <summary>
/// Generates code for UI automation actions.
/// </summary>
public class ActionCodeGenerator : ICodeGenerator
{
    private readonly ITemplateEngine _templateEngine;
    private readonly string _defaultNamespace;

    /// <summary>
    /// Creates a new action code generator.
    /// </summary>
    public ActionCodeGenerator(ITemplateEngine templateEngine, string defaultNamespace = "Cascade.Generated")
    {
        _templateEngine = templateEngine ?? throw new ArgumentNullException(nameof(templateEngine));
        _defaultNamespace = defaultNamespace;
    }

    /// <inheritdoc />
    public async Task<GeneratedCode> GenerateActionAsync(ActionDefinition action)
    {
        return await GenerateActionsAsync(new[] { action });
    }

    /// <inheritdoc />
    public async Task<GeneratedCode> GenerateActionsAsync(IEnumerable<ActionDefinition> actions)
    {
        var actionsList = actions.ToList();
        if (!actionsList.Any())
            throw new ArgumentException("At least one action is required", nameof(actions));

        var className = actionsList.First().Name ?? "GeneratedActions";
        var actionsData = actionsList.Select(a => new
        {
            name = ToPascalCase(a.Name ?? "Action"),
            code = GenerateActionCode(a)
        }).ToList();

        var context = new TemplateContext
        {
            Namespace = _defaultNamespace,
            ClassName = className,
            Variables = new Dictionary<string, object>
            {
                ["actions"] = actionsData
            }
        };

        var sourceCode = await _templateEngine.RenderAsync("ActionScript", context);

        return new GeneratedCode
        {
            SourceCode = sourceCode,
            FileName = $"{className}.cs",
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
                TemplateUsed = "ActionScript",
                Parameters = new Dictionary<string, object>
                {
                    ["actionCount"] = actionsList.Count
                }
            }
        };
    }

    /// <inheritdoc />
    public async Task<GeneratedCode> GenerateWorkflowAsync(WorkflowDefinition workflow)
    {
        // This will be implemented in WorkflowGenerator
        throw new NotImplementedException("Use WorkflowGenerator for workflow generation");
    }

    /// <inheritdoc />
    public async Task<GeneratedCode> GenerateAgentAsync(AgentDefinition agent)
    {
        // This will be implemented in AgentCodeGenerator
        throw new NotImplementedException("Use AgentCodeGenerator for agent generation");
    }

    /// <inheritdoc />
    public Task<string> OptimizeAsync(string sourceCode)
    {
        return Task.FromResult(CodeOptimizer.Optimize(sourceCode));
    }

    /// <summary>
    /// Generates code string for a single action.
    /// </summary>
    public string GenerateActionCode(ActionDefinition action)
    {
        var locatorString = action.TargetElement?.ToString() ?? "";
        var code = new List<string>();

        // Find element first
        code.Add("var root = _discovery.GetDesktopRoot();");
        code.Add($"var locator = ElementLocator.Parse(\"{locatorString}\");");
        code.Add("var element = locator.Find(root);");
        code.Add("if (element == null) throw new InvalidOperationException(\"Element not found\");");

        // Generate action code based on type
        switch (action.Type)
        {
            case ActionType.Click:
                code.Add("await element.ClickAsync();");
                break;
            case ActionType.DoubleClick:
                code.Add("await element.DoubleClickAsync();");
                break;
            case ActionType.RightClick:
                code.Add("await element.RightClickAsync();");
                break;
            case ActionType.Type:
                var text = action.Parameters.TryGetValue("text", out var textValue) ? textValue?.ToString() ?? "" : "";
                code.Add($"await element.TypeTextAsync(\"{text}\");");
                break;
            case ActionType.SetValue:
                var value = action.Parameters.TryGetValue("value", out var valueValue) ? valueValue?.ToString() ?? "" : "";
                code.Add($"await element.SetValueAsync(\"{value}\");");
                break;
            case ActionType.Invoke:
                code.Add("await element.InvokeAsync();");
                break;
            case ActionType.Focus:
                code.Add("await element.SetFocusAsync();");
                break;
            default:
                code.Add($"// Action type {action.Type} not yet implemented");
                break;
        }

        // Add delay if specified
        if (action.Delay.HasValue)
        {
            code.Add($"await Task.Delay({(int)action.Delay.Value.TotalMilliseconds});");
        }

        return string.Join("\n            ", code);
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


using System.Linq;
using System.Text;
using Cascade.CodeGen.Execution;
using Cascade.CodeGen.Templates;
using Cascade.UIAutomation.Discovery;

namespace Cascade.CodeGen.Generation;

public sealed class CodeGenerator : ICodeGenerator
{
    private readonly ITemplateEngine _templateEngine;
    private readonly TemplateContextFactory _contextFactory;
    private readonly CodeGenOptions _options;

    public CodeGenerator(
        ITemplateEngine templateEngine,
        TemplateContextFactory contextFactory,
        CodeGenOptions options)
    {
        _templateEngine = templateEngine ?? throw new ArgumentNullException(nameof(templateEngine));
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public Task<GeneratedCode> GenerateActionAsync(ActionDefinition action, CancellationToken cancellationToken = default)
    {
        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        return GenerateActionsAsync(new[] { action }, cancellationToken);
    }

    public async Task<GeneratedCode> GenerateActionsAsync(IEnumerable<ActionDefinition> actions, CancellationToken cancellationToken = default)
    {
        var actionList = actions?.ToList() ?? throw new ArgumentNullException(nameof(actions));
        if (actionList.Count == 0)
        {
            throw new ArgumentException("At least one action must be provided.", nameof(actions));
        }

        var className = $"{CamelToPascal(actionList.First().Name)}Actions";
        var context = _contextFactory.Create(_options.DefaultNamespace, className);

        var actionModels = actionList.Select(action => new
        {
            method_name = CamelToPascal(action.Name),
            code = GenerateActionBody(action)
        }).ToList();

        context.Variables["actions"] = actionModels;
        var entryMethod = $"{CamelToPascal(actionList.First().Name)}Async";

        var source = await _templateEngine.RenderAsync(TemplateNames.ActionScript, context, cancellationToken)
            .ConfigureAwait(false);

        return new GeneratedCode
        {
            SourceCode = source,
            FileName = $"{className}.cs",
            Namespace = context.Namespace,
            Metadata = new CodeGenerationMetadata
            {
                GeneratedAt = DateTime.UtcNow,
                GeneratorVersion = _options.GeneratorVersion,
                TemplateUsed = TemplateNames.ActionScript,
                Parameters = new Dictionary<string, object>
                {
                    ["methodCount"] = actionModels.Count,
                    ["className"] = className,
                    ["entryMethod"] = entryMethod
                }
            }
        };
    }

    public async Task<GeneratedCode> GenerateWorkflowAsync(WorkflowDefinition workflow, CancellationToken cancellationToken = default)
    {
        if (workflow is null)
        {
            throw new ArgumentNullException(nameof(workflow));
        }

        var className = $"{CamelToPascal(workflow.Name)}Workflow";
        var context = _contextFactory.Create(_options.DefaultNamespace, className);

        var steps = workflow.Steps
            .OrderBy(step => step.Order)
            .Select(step => new
            {
                name = step.Name,
                description = step.Description,
                code = GenerateActionBody(step.Action),
                delay_after = step.DelayAfter.HasValue ? step.DelayAfter.Value.TotalMilliseconds.ToString("F0") : null
            })
            .ToList();

        context.Variables["workflow"] = new
        {
            workflow.Name,
            steps
        };

        var source = await _templateEngine.RenderAsync(TemplateNames.WorkflowScript, context, cancellationToken)
            .ConfigureAwait(false);

        return new GeneratedCode
        {
            SourceCode = source,
            FileName = $"{className}.cs",
            Namespace = context.Namespace,
            Metadata = new CodeGenerationMetadata
            {
                GeneratedAt = DateTime.UtcNow,
                GeneratorVersion = _options.GeneratorVersion,
                TemplateUsed = TemplateNames.WorkflowScript,
                Parameters = new Dictionary<string, object>
                {
                    ["stepCount"] = steps.Count,
                    ["className"] = className
                }
            }
        };
    }

    public Task<string> OptimizeAsync(string sourceCode, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(sourceCode?.Trim() ?? string.Empty);
    }

    private string GenerateActionBody(ActionDefinition action)
    {
        var builder = new StringBuilder();
        builder.AppendLine("var criteria = new SearchCriteria();");

        void AppendAssignment(string property, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                builder.AppendLine($"criteria.{property} = {ToLiteral(value)};");
            }
        }

        AppendAssignment(nameof(SearchCriteria.AutomationId), action.TargetElement.AutomationId);
        AppendAssignment(nameof(SearchCriteria.Name), action.TargetElement.Name);
        AppendAssignment(nameof(SearchCriteria.ClassName), action.TargetElement.ClassName);
        AppendAssignment(nameof(SearchCriteria.NameContains), action.TargetElement.NameContains);

        if (action.TargetElement.IsEnabled.HasValue)
        {
            builder.AppendLine($"criteria.IsEnabled = {action.TargetElement.IsEnabled.Value.ToString().ToLowerInvariant()};");
        }

        if (action.TargetElement.IsOffscreen.HasValue)
        {
            builder.AppendLine($"criteria.IsOffscreen = {action.TargetElement.IsOffscreen.Value.ToString().ToLowerInvariant()};");
        }

        builder.AppendLine($"var element = await _discovery.WaitForElementAsync(criteria, TimeSpan.FromSeconds({_options.DefaultActionTimeoutSeconds}), token).ConfigureAwait(false);");
        builder.AppendLine("if (element is null)");
        builder.AppendLine("{");
        builder.AppendLine($"    throw new InvalidOperationException(\"Failed to locate element for action '{action.Name}'.\");");
        builder.AppendLine("}");

        builder.AppendLine();
        builder.AppendLine($"var runtimeAction = new ActionRuntimeRequest({ToLiteral(action.Name)}, ActionType.{action.Type})");
        builder.AppendLine("{");
        builder.AppendLine($"    Description = {ToLiteral(action.Description)},");
        builder.AppendLine($"    Delay = {ToTimeSpanLiteral(action.Delay)},");
        builder.AppendLine($"    RetryCount = {action.RetryCount},");
        builder.AppendLine($"    CaptureScreenshotBefore = {action.CaptureScreenshotBefore.ToString().ToLowerInvariant()},");
        builder.AppendLine($"    CaptureScreenshotAfter = {action.CaptureScreenshotAfter.ToString().ToLowerInvariant()}");
        builder.AppendLine("};");

        foreach (var parameter in action.Parameters)
        {
            builder.AppendLine($"runtimeAction.Parameters[{ToLiteral(parameter.Key)}] = {ToLiteral(parameter.Value?.ToString() ?? string.Empty)};");
        }

        builder.AppendLine("await _actionExecutor.ExecuteAsync(runtimeAction, element, _context, token).ConfigureAwait(false);");

        return Indent(builder.ToString(), 12);
    }

    private static string ToLiteral(string? value)
    {
        if (value is null)
        {
            return "null";
        }

        return "@\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static string ToTimeSpanLiteral(TimeSpan? value)
    {
        if (value is null)
        {
            return "null";
        }

        return $"TimeSpan.FromMilliseconds({value.Value.TotalMilliseconds.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)})";
    }

    private static string CamelToPascal(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "GeneratedAction";
        }

        var cleaned = new string(value.Where(char.IsLetterOrDigit).ToArray());
        if (string.IsNullOrEmpty(cleaned))
        {
            return "GeneratedAction";
        }

        return char.ToUpperInvariant(cleaned[0]) + cleaned[1..];
    }

    private static string Indent(string value, int spaces)
    {
        var indent = new string(' ', spaces);
        var lines = value.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        for (var i = 0; i < lines.Length; i++)
        {
            if (!string.IsNullOrEmpty(lines[i]))
            {
                lines[i] = indent + lines[i];
            }
        }

        return string.Join(Environment.NewLine, lines);
    }
}


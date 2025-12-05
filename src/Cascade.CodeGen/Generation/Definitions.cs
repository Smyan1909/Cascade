using Cascade.UIAutomation.Discovery;

namespace Cascade.CodeGen.Generation;

public sealed class ElementLocator
{
    public string? AutomationId { get; set; }
    public string? Name { get; set; }
    public string? ClassName { get; set; }
    public string? NameContains { get; set; }
    public bool? IsEnabled { get; set; }
    public bool? IsOffscreen { get; set; }

    public SearchCriteria ToSearchCriteria()
    {
        var criteria = new SearchCriteria
        {
            AutomationId = AutomationId,
            Name = Name,
            ClassName = ClassName,
            NameContains = NameContains,
            IsEnabled = IsEnabled,
            IsOffscreen = IsOffscreen
        };

        return criteria;
    }

    public static ElementLocator FromSearchCriteria(SearchCriteria criteria)
    {
        return new ElementLocator
        {
            AutomationId = criteria.AutomationId,
            Name = criteria.Name,
            ClassName = criteria.ClassName,
            NameContains = criteria.NameContains,
            IsEnabled = criteria.IsEnabled,
            IsOffscreen = criteria.IsOffscreen
        };
    }
}

public enum ActionType
{
    Click,
    DoubleClick,
    RightClick,
    Type,
    SetValue,
    Invoke,
    WaitForElement,
    Custom
}

public sealed class ActionDefinition
{
    public string Name { get; set; } = "Action";
    public string Description { get; set; } = string.Empty;
    public ActionType Type { get; set; } = ActionType.Click;
    public ElementLocator TargetElement { get; set; } = new();
    public Dictionary<string, object> Parameters { get; set; } = new();
    public TimeSpan? Delay { get; set; }
    public int RetryCount { get; set; } = 3;
    public bool CaptureScreenshotBefore { get; set; }
    public bool CaptureScreenshotAfter { get; set; }
}

public enum ErrorHandling
{
    StopOnError,
    ContinueOnError,
    RetryThenContinue,
    RetryThenStop
}

public sealed class WorkflowStep
{
    public int Order { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ActionDefinition Action { get; set; } = new();
    public string? Condition { get; set; }
    public TimeSpan? DelayAfter { get; set; }
    public WorkflowStep? OnSuccess { get; set; }
    public WorkflowStep? OnFailure { get; set; }
}

public sealed class WorkflowDefinition
{
    public string Name { get; set; } = "Workflow";
    public string Description { get; set; } = string.Empty;
    public IReadOnlyList<WorkflowStep> Steps { get; set; } = Array.Empty<WorkflowStep>();
    public Dictionary<string, object> InputParameters { get; set; } = new();
    public Dictionary<string, object> OutputParameters { get; set; } = new();
    public ErrorHandling ErrorHandling { get; set; } = ErrorHandling.StopOnError;
}

public sealed class GeneratedCode
{
    public string SourceCode { get; set; } = string.Empty;
    public string FileName { get; set; } = "Generated.cs";
    public string Namespace { get; set; } = "Cascade.Generated";
    public IReadOnlyList<string> RequiredUsings { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> RequiredReferences { get; set; } = Array.Empty<string>();
    public CodeGenerationMetadata Metadata { get; set; } = new();
}

public sealed class CodeGenerationMetadata
{
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public string GeneratorVersion { get; set; } = "1.0.0";
    public string TemplateUsed { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
}


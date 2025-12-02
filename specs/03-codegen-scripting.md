# Code Generation and Scripting Module Specification

## Overview

The `Cascade.CodeGen` module provides dynamic C# code generation, compilation, and execution capabilities. It enables the system to generate UI automation scripts at runtime, compile them using Roslyn, and execute them in a sandboxed environment.

## Dependencies

```xml
<PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.8.0" />
<PackageReference Include="Microsoft.CodeAnalysis.CSharp.Scripting" Version="4.8.0" />
<PackageReference Include="Scriban" Version="5.9.0" />
```

## Architecture

```
Cascade.CodeGen/
├── Templates/
│   ├── ITemplateEngine.cs          # Template engine interface
│   ├── ScribanTemplateEngine.cs    # Scriban implementation
│   ├── TemplateRegistry.cs         # Template management
│   ├── TemplateContext.cs          # Template data context
│   └── BuiltIn/                    # Built-in templates
│       ├── ActionScript.sbn
│       ├── WorkflowScript.sbn
│       ├── AgentClass.sbn
│       └── TestScript.sbn
├── Compiler/
│   ├── IScriptCompiler.cs          # Compiler interface
│   ├── RoslynCompiler.cs           # Roslyn implementation
│   ├── CompilationResult.cs        # Compilation result
│   ├── CompilationOptions.cs       # Compiler options
│   └── ReferenceResolver.cs        # Assembly resolution
├── Execution/
│   ├── IScriptExecutor.cs          # Executor interface
│   ├── SandboxedExecutor.cs        # Isolated execution
│   ├── ExecutionContext.cs         # Runtime context
│   ├── ExecutionResult.cs          # Execution result
│   └── SecurityPolicy.cs           # Sandbox restrictions
├── Generation/
│   ├── ICodeGenerator.cs           # Generator interface
│   ├── ActionCodeGenerator.cs      # UI action code gen
│   ├── WorkflowGenerator.cs        # Workflow code gen
│   ├── AgentCodeGenerator.cs       # Agent class gen
│   └── CodeOptimizer.cs            # Code optimization
├── Recording/
│   ├── IActionRecorder.cs          # Recorder interface
│   ├── ActionRecorder.cs           # Implementation
│   ├── RecordedAction.cs           # Recorded action model
│   └── RecordingSession.cs         # Recording session
├── Persistence/
│   ├── IScriptRepository.cs        # Script storage interface
│   ├── ScriptRepository.cs         # Implementation
│   └── ScriptVersion.cs            # Version tracking
└── Services/
    ├── CodeGenService.cs           # Main service facade
    └── CodeGenOptions.cs           # Configuration
```

## Core Interfaces

### ITemplateEngine

```csharp
public interface ITemplateEngine
{
    // Template rendering
    Task<string> RenderAsync(string templateName, object model);
    Task<string> RenderAsync(string templateName, TemplateContext context);
    string RenderInline(string templateContent, object model);
    
    // Template management
    void RegisterTemplate(string name, string content);
    void RegisterTemplateFile(string name, string filePath);
    bool HasTemplate(string name);
    IReadOnlyList<string> GetTemplateNames();
    
    // Validation
    ValidationResult ValidateTemplate(string templateContent);
}

public class TemplateContext
{
    public Dictionary<string, object> Variables { get; set; } = new();
    public string? Namespace { get; set; }
    public string? ClassName { get; set; }
    public IReadOnlyList<string> Usings { get; set; } = new List<string>();
    public IReadOnlyList<string> References { get; set; } = new List<string>();
    
    // Helper methods available in templates
    public Func<string, string> ToCamelCase { get; }
    public Func<string, string> ToPascalCase { get; }
    public Func<string, string> ToSnakeCase { get; }
    public Func<object, string> ToJson { get; }
}
```

### Built-in Templates

#### ActionScript.sbn
```scriban
// Auto-generated UI Automation Script
// Generated: {{ date.now | date.to_string "%Y-%m-%d %H:%M:%S" }}

using Cascade.UIAutomation;
using Cascade.Core;
using System.Threading.Tasks;

namespace {{ namespace }}
{
    public class {{ class_name }}
    {
        private readonly IElementDiscovery _discovery;
        private readonly IActionExecutor _executor;
        
        public {{ class_name }}(IElementDiscovery discovery, IActionExecutor executor)
        {
            _discovery = discovery;
            _executor = executor;
        }
        
        {{ for action in actions }}
        public async Task {{ action.name | string.pascalcase }}Async()
        {
            {{ action.code | string.indent 12 }}
        }
        {{ end }}
    }
}
```

#### WorkflowScript.sbn
```scriban
// Auto-generated Workflow Script
// Workflow: {{ workflow.name }}

using Cascade.UIAutomation;
using Cascade.Core;
using System.Threading.Tasks;

namespace {{ namespace }}
{
    public class {{ class_name }} : IWorkflow
    {
        {{ for step in workflow.steps }}
        private async Task Step{{ for.index }}Async(WorkflowContext context)
        {
            // {{ step.description }}
            {{ step.code | string.indent 12 }}
        }
        {{ end }}
        
        public async Task ExecuteAsync(WorkflowContext context)
        {
            {{ for step in workflow.steps }}
            await Step{{ for.index }}Async(context);
            {{ if step.has_delay }}
            await Task.Delay({{ step.delay_ms }});
            {{ end }}
            {{ end }}
        }
    }
}
```

## Compiler Interfaces

### IScriptCompiler

```csharp
public interface IScriptCompiler
{
    // Compilation
    Task<CompilationResult> CompileAsync(string sourceCode, CompilationOptions? options = null);
    Task<CompilationResult> CompileFilesAsync(IEnumerable<string> filePaths, CompilationOptions? options = null);
    
    // Scripting (immediate evaluation)
    Task<ScriptResult<T>> EvaluateAsync<T>(string expression, ScriptGlobals? globals = null);
    Task<ScriptResult> EvaluateAsync(string script, ScriptGlobals? globals = null);
    
    // Syntax checking
    Task<IReadOnlyList<Diagnostic>> CheckSyntaxAsync(string sourceCode);
    bool IsValidSyntax(string sourceCode);
}
```

### CompilationResult

```csharp
public class CompilationResult
{
    public bool Success { get; set; }
    public byte[]? AssemblyBytes { get; set; }
    public Assembly? Assembly { get; set; }
    public IReadOnlyList<CompilationError> Errors { get; set; } = new List<CompilationError>();
    public IReadOnlyList<CompilationWarning> Warnings { get; set; } = new List<CompilationWarning>();
    public TimeSpan CompilationTime { get; set; }
    
    // Convenience methods
    public T? CreateInstance<T>(string typeName) where T : class;
    public object? CreateInstance(string typeName);
    public MethodInfo? GetMethod(string typeName, string methodName);
}

public class CompilationError
{
    public string Code { get; set; }
    public string Message { get; set; }
    public int Line { get; set; }
    public int Column { get; set; }
    public string? FilePath { get; set; }
    public CompilationErrorSeverity Severity { get; set; }
}

public enum CompilationErrorSeverity
{
    Info,
    Warning,
    Error
}
```

### CompilationOptions

```csharp
public class CompilationOptions
{
    // Output
    public string? AssemblyName { get; set; }
    public OutputKind OutputKind { get; set; } = OutputKind.DynamicallyLinkedLibrary;
    public OptimizationLevel OptimizationLevel { get; set; } = OptimizationLevel.Release;
    
    // References
    public IReadOnlyList<string> References { get; set; } = new List<string>();
    public IReadOnlyList<Assembly> AssemblyReferences { get; set; } = new List<Assembly>();
    public bool IncludeDefaultReferences { get; set; } = true;
    
    // Preprocessor
    public IReadOnlyList<string> PreprocessorSymbols { get; set; } = new List<string>();
    
    // Language
    public LanguageVersion LanguageVersion { get; set; } = LanguageVersion.Latest;
    public NullableContextOptions NullableContextOptions { get; set; } = NullableContextOptions.Enable;
    
    // Behavior
    public bool TreatWarningsAsErrors { get; set; } = false;
    public IReadOnlyList<string> SuppressedWarnings { get; set; } = new List<string>();
}
```

### Default References

```csharp
public static class DefaultReferences
{
    public static IReadOnlyList<Assembly> GetDefaultAssemblies()
    {
        return new[]
        {
            typeof(object).Assembly,                    // mscorlib/System.Private.CoreLib
            typeof(Console).Assembly,                   // System.Console
            typeof(Task).Assembly,                      // System.Threading.Tasks
            typeof(Enumerable).Assembly,                // System.Linq
            typeof(List<>).Assembly,                    // System.Collections
            typeof(JsonSerializer).Assembly,            // System.Text.Json
            typeof(IUIElement).Assembly,                // Cascade.UIAutomation
            typeof(CaptureResult).Assembly,             // Cascade.Vision
        };
    }
}
```

## Execution Interfaces

### IScriptExecutor

```csharp
public interface IScriptExecutor
{
    // Execute compiled assembly
    Task<ExecutionResult> ExecuteAsync(
        CompilationResult compilation, 
        string typeName, 
        string methodName,
        ExecutionContext? context = null);
    
    // Execute with parameters
    Task<ExecutionResult<T>> ExecuteAsync<T>(
        CompilationResult compilation,
        string typeName,
        string methodName,
        object?[]? parameters = null,
        ExecutionContext? context = null);
    
    // Execute inline script
    Task<ExecutionResult> ExecuteScriptAsync(
        string script,
        ExecutionContext? context = null);
    
    // Cancellation
    Task CancelAsync(Guid executionId);
}
```

### ExecutionContext

```csharp
public class ExecutionContext
{
    public Guid ExecutionId { get; } = Guid.NewGuid();
    
    // Services available to scripts
    public IServiceProvider Services { get; set; }
    public IElementDiscovery ElementDiscovery { get; set; }
    public IScreenCapture ScreenCapture { get; set; }
    public IOcrEngine OcrEngine { get; set; }
    
    // Variables
    public Dictionary<string, object> Variables { get; set; } = new();
    
    // Constraints
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
    public CancellationToken CancellationToken { get; set; }
    public SecurityPolicy SecurityPolicy { get; set; } = SecurityPolicy.Default;
    
    // Logging
    public Action<string>? LogInfo { get; set; }
    public Action<string>? LogWarning { get; set; }
    public Action<string, Exception>? LogError { get; set; }
}
```

### ExecutionResult

```csharp
public class ExecutionResult
{
    public bool Success { get; set; }
    public object? ReturnValue { get; set; }
    public Exception? Exception { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public Guid ExecutionId { get; set; }
    public IReadOnlyList<string> Logs { get; set; } = new List<string>();
    public ExecutionStatus Status { get; set; }
}

public class ExecutionResult<T> : ExecutionResult
{
    public new T? ReturnValue { get; set; }
}

public enum ExecutionStatus
{
    Completed,
    Failed,
    Timeout,
    Cancelled,
    SecurityViolation
}
```

### SecurityPolicy

```csharp
public class SecurityPolicy
{
    // File system
    public bool AllowFileRead { get; set; } = false;
    public bool AllowFileWrite { get; set; } = false;
    public IReadOnlyList<string> AllowedReadPaths { get; set; } = new List<string>();
    public IReadOnlyList<string> AllowedWritePaths { get; set; } = new List<string>();
    
    // Network
    public bool AllowNetwork { get; set; } = false;
    public IReadOnlyList<string> AllowedHosts { get; set; } = new List<string>();
    
    // Process
    public bool AllowProcessStart { get; set; } = false;
    public IReadOnlyList<string> AllowedProcesses { get; set; } = new List<string>();
    
    // Resources
    public int MaxMemoryMB { get; set; } = 512;
    public int MaxThreads { get; set; } = 10;
    public TimeSpan MaxExecutionTime { get; set; } = TimeSpan.FromMinutes(10);
    
    // Reflection
    public bool AllowReflection { get; set; } = false;
    public bool AllowUnsafeCode { get; set; } = false;
    
    // Presets
    public static SecurityPolicy Default { get; } = new();
    public static SecurityPolicy Strict { get; } = new()
    {
        AllowFileRead = false,
        AllowFileWrite = false,
        AllowNetwork = false,
        AllowProcessStart = false,
        AllowReflection = false,
        MaxMemoryMB = 256,
        MaxThreads = 5,
        MaxExecutionTime = TimeSpan.FromMinutes(1)
    };
    public static SecurityPolicy Permissive { get; } = new()
    {
        AllowFileRead = true,
        AllowFileWrite = true,
        AllowNetwork = true,
        AllowProcessStart = true,
        AllowReflection = true,
        MaxMemoryMB = 2048,
        MaxThreads = 50,
        MaxExecutionTime = TimeSpan.FromHours(1)
    };
}
```

## Code Generation

### ICodeGenerator

```csharp
public interface ICodeGenerator
{
    // Generate action code
    Task<GeneratedCode> GenerateActionAsync(ActionDefinition action);
    Task<GeneratedCode> GenerateActionsAsync(IEnumerable<ActionDefinition> actions);
    
    // Generate workflow
    Task<GeneratedCode> GenerateWorkflowAsync(WorkflowDefinition workflow);
    
    // Generate agent class
    Task<GeneratedCode> GenerateAgentAsync(AgentDefinition agent);
    
    // Optimize code
    Task<string> OptimizeAsync(string sourceCode);
}

public class GeneratedCode
{
    public string SourceCode { get; set; }
    public string FileName { get; set; }
    public string Namespace { get; set; }
    public IReadOnlyList<string> RequiredUsings { get; set; }
    public IReadOnlyList<string> RequiredReferences { get; set; }
    public CodeGenerationMetadata Metadata { get; set; }
}

public class CodeGenerationMetadata
{
    public DateTime GeneratedAt { get; set; }
    public string GeneratorVersion { get; set; }
    public string TemplateUsed { get; set; }
    public Dictionary<string, object> Parameters { get; set; }
}
```

### ActionDefinition

```csharp
public class ActionDefinition
{
    public string Name { get; set; }
    public string Description { get; set; }
    public ActionType Type { get; set; }
    public ElementLocator TargetElement { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
    public TimeSpan? Delay { get; set; }
    public int RetryCount { get; set; } = 3;
    public bool CaptureScreenshotBefore { get; set; } = false;
    public bool CaptureScreenshotAfter { get; set; } = false;
}

public enum ActionType
{
    Click,
    DoubleClick,
    RightClick,
    Type,
    SetValue,
    Select,
    Check,
    Uncheck,
    Expand,
    Collapse,
    Scroll,
    DragDrop,
    Invoke,
    Focus,
    WaitForElement,
    WaitForText,
    CaptureScreenshot,
    RunOcr,
    Custom
}
```

### WorkflowDefinition

```csharp
public class WorkflowDefinition
{
    public string Name { get; set; }
    public string Description { get; set; }
    public IReadOnlyList<WorkflowStep> Steps { get; set; } = new List<WorkflowStep>();
    public Dictionary<string, object> InputParameters { get; set; } = new();
    public Dictionary<string, object> OutputParameters { get; set; } = new();
    public ErrorHandling ErrorHandling { get; set; } = ErrorHandling.StopOnError;
}

public class WorkflowStep
{
    public int Order { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public ActionDefinition Action { get; set; }
    public string? Condition { get; set; }  // C# expression
    public TimeSpan? DelayAfter { get; set; }
    public WorkflowStep? OnSuccess { get; set; }
    public WorkflowStep? OnFailure { get; set; }
}

public enum ErrorHandling
{
    StopOnError,
    ContinueOnError,
    RetryThenContinue,
    RetryThenStop
}
```

## Action Recording

### IActionRecorder

```csharp
public interface IActionRecorder
{
    // Recording control
    Task<RecordingSession> StartRecordingAsync(RecordingOptions? options = null);
    Task StopRecordingAsync(Guid sessionId);
    Task PauseRecordingAsync(Guid sessionId);
    Task ResumeRecordingAsync(Guid sessionId);
    
    // Recording state
    bool IsRecording { get; }
    RecordingSession? CurrentSession { get; }
    
    // Events
    event EventHandler<RecordedAction>? ActionRecorded;
    event EventHandler<RecordingSession>? RecordingStarted;
    event EventHandler<RecordingSession>? RecordingStopped;
}

public class RecordingOptions
{
    public bool RecordMouseClicks { get; set; } = true;
    public bool RecordKeystrokes { get; set; } = true;
    public bool RecordScrolls { get; set; } = true;
    public bool CaptureScreenshots { get; set; } = true;
    public TimeSpan MinActionInterval { get; set; } = TimeSpan.FromMilliseconds(100);
    public IReadOnlyList<string> ExcludedProcesses { get; set; } = new List<string>();
    public Rectangle? CaptureRegion { get; set; }
}

public class RecordingSession
{
    public Guid SessionId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public RecordingState State { get; set; }
    public IReadOnlyList<RecordedAction> Actions { get; set; } = new List<RecordedAction>();
    public RecordingOptions Options { get; set; }
    
    // Generate code from recording
    public GeneratedCode ToCode(string className);
    public WorkflowDefinition ToWorkflow(string workflowName);
}

public class RecordedAction
{
    public int Index { get; set; }
    public DateTime Timestamp { get; set; }
    public ActionType Type { get; set; }
    public ElementSnapshot? TargetElement { get; set; }
    public Point? MousePosition { get; set; }
    public string? TypedText { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
    public byte[]? Screenshot { get; set; }
    public TimeSpan? DurationSincePrevious { get; set; }
}

public enum RecordingState
{
    NotStarted,
    Recording,
    Paused,
    Stopped
}
```

## Script Persistence

### IScriptRepository

```csharp
public interface IScriptRepository
{
    // CRUD operations
    Task<ScriptRecord> SaveAsync(ScriptRecord script);
    Task<ScriptRecord?> GetAsync(Guid scriptId);
    Task<ScriptRecord?> GetByNameAsync(string name, string? version = null);
    Task<IReadOnlyList<ScriptRecord>> GetAllAsync(ScriptFilter? filter = null);
    Task DeleteAsync(Guid scriptId);
    
    // Versioning
    Task<ScriptRecord> CreateVersionAsync(Guid scriptId, string newSourceCode);
    Task<IReadOnlyList<ScriptVersion>> GetVersionsAsync(Guid scriptId);
    Task<ScriptRecord?> GetVersionAsync(Guid scriptId, string version);
    
    // Compilation cache
    Task<byte[]?> GetCompiledAssemblyAsync(Guid scriptId, string version);
    Task SaveCompiledAssemblyAsync(Guid scriptId, string version, byte[] assembly);
}

public class ScriptRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string SourceCode { get; set; }
    public string CurrentVersion { get; set; }
    public ScriptType Type { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? AgentId { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class ScriptVersion
{
    public Guid ScriptId { get; set; }
    public string Version { get; set; }
    public string SourceCode { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ChangeDescription { get; set; }
}

public enum ScriptType
{
    Action,
    Workflow,
    Agent,
    Test,
    Utility
}

public class ScriptFilter
{
    public ScriptType? Type { get; set; }
    public Guid? AgentId { get; set; }
    public string? NameContains { get; set; }
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
}
```

## Service Configuration

```csharp
public class CodeGenOptions
{
    // Templates
    public string? TemplateDirectory { get; set; }
    public IReadOnlyList<string> AdditionalTemplates { get; set; } = new List<string>();
    
    // Compilation
    public CompilationOptions DefaultCompilationOptions { get; set; } = new();
    public bool CacheCompilations { get; set; } = true;
    public int MaxCachedCompilations { get; set; } = 100;
    
    // Execution
    public SecurityPolicy DefaultSecurityPolicy { get; set; } = SecurityPolicy.Default;
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(5);
    
    // Code generation
    public string DefaultNamespace { get; set; } = "Cascade.Generated";
    public bool IncludeDebugInfo { get; set; } = true;
    public bool OptimizeGeneratedCode { get; set; } = true;
}
```

## Usage Examples

### Generate and Execute Action Code
```csharp
var action = new ActionDefinition
{
    Name = "ClickSubmitButton",
    Type = ActionType.Click,
    TargetElement = ElementLocator.Parse("/Window[@Name='Form']/Button[@Name='Submit']")
};

var generator = new ActionCodeGenerator(templateEngine);
var generatedCode = await generator.GenerateActionAsync(action);

var compiler = new RoslynCompiler();
var compilation = await compiler.CompileAsync(generatedCode.SourceCode);

if (compilation.Success)
{
    var executor = new SandboxedExecutor();
    var result = await executor.ExecuteAsync(
        compilation,
        "Cascade.Generated.ClickSubmitButton",
        "ExecuteAsync",
        new ExecutionContext { ElementDiscovery = discovery });
    
    Console.WriteLine($"Execution completed: {result.Success}");
}
```

### Record and Generate Workflow
```csharp
var recorder = new ActionRecorder();
var session = await recorder.StartRecordingAsync();

// User performs actions...

await recorder.StopRecordingAsync(session.SessionId);

// Convert to workflow
var workflow = session.ToWorkflow("MyRecordedWorkflow");
var workflowCode = await generator.GenerateWorkflowAsync(workflow);

// Save for later use
await scriptRepository.SaveAsync(new ScriptRecord
{
    Name = "MyRecordedWorkflow",
    SourceCode = workflowCode.SourceCode,
    Type = ScriptType.Workflow
});
```

### Execute Script with Variables
```csharp
var context = new ExecutionContext
{
    Variables = new Dictionary<string, object>
    {
        ["username"] = "testuser",
        ["password"] = "testpass"
    },
    ElementDiscovery = discovery,
    SecurityPolicy = SecurityPolicy.Default
};

var script = @"
    var loginWindow = await ElementDiscovery.WaitForElementAsync(
        SearchCriteria.ByName(""Login""), 
        TimeSpan.FromSeconds(10));
    
    var userField = loginWindow.FindFirst(SearchCriteria.ByAutomationId(""username""));
    await userField.TypeTextAsync(Variables[""username""].ToString());
    
    var passField = loginWindow.FindFirst(SearchCriteria.ByAutomationId(""password""));
    await passField.TypeTextAsync(Variables[""password""].ToString());
    
    var submitButton = loginWindow.FindFirst(SearchCriteria.ByName(""Submit""));
    await submitButton.ClickAsync();
";

var result = await executor.ExecuteScriptAsync(script, context);
```



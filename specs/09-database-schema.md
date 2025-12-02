# Database Schema Specification

## Overview

The `Cascade.Database` module provides data persistence for agents, scripts, exploration results, execution history, and configuration. It supports both SQLite for local development and PostgreSQL for distributed deployments.

## Architecture

```
Cascade.Database/
├── Entities/
│   ├── Agent.cs
│   ├── AgentVersion.cs
│   ├── Script.cs
│   ├── ScriptVersion.cs
│   ├── ExplorationSession.cs
│   ├── ExplorationResult.cs
│   ├── ExecutionRecord.cs
│   ├── ExecutionStep.cs
│   └── Configuration.cs
├── Repositories/
│   ├── IRepository.cs
│   ├── IAgentRepository.cs
│   ├── IScriptRepository.cs
│   ├── IExplorationRepository.cs
│   ├── IExecutionRepository.cs
│   └── Implementations/
├── Context/
│   ├── CascadeDbContext.cs
│   └── CascadeDbContextFactory.cs
├── Migrations/
│   ├── Initial/
│   └── ...
└── Configuration/
    ├── DatabaseOptions.cs
    └── EntityConfigurations/
```

## Entity Definitions

### Agent Entity

```csharp
public class Agent
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TargetApplication { get; set; } = string.Empty;
    public string ActiveVersion { get; set; } = "1.0.0";
    public AgentStatus Status { get; set; } = AgentStatus.Active;
    
    // JSON columns
    public List<string> Capabilities { get; set; } = new();
    public string InstructionList { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
    
    // Timestamps
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastExecutedAt { get; set; }
    
    // Navigation
    public ICollection<AgentVersion> Versions { get; set; } = new List<AgentVersion>();
    public ICollection<Script> Scripts { get; set; } = new List<Script>();
    public ICollection<ExecutionRecord> Executions { get; set; } = new List<ExecutionRecord>();
}

public enum AgentStatus
{
    Active,
    Inactive,
    Draft,
    Archived
}
```

### AgentVersion Entity

```csharp
public class AgentVersion
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public string Version { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    
    // Snapshot of agent state at this version
    public string InstructionListSnapshot { get; set; } = string.Empty;
    public List<string> CapabilitiesSnapshot { get; set; } = new();
    public List<Guid> ScriptIdsSnapshot { get; set; } = new();
    
    public DateTime CreatedAt { get; set; }
    
    // Navigation
    public Agent Agent { get; set; } = null!;
}
```

### Script Entity

```csharp
public class Script
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SourceCode { get; set; } = string.Empty;
    public string CurrentVersion { get; set; } = "1.0.0";
    public ScriptType Type { get; set; }
    
    // Compilation
    public byte[]? CompiledAssembly { get; set; }
    public DateTime? LastCompiledAt { get; set; }
    public string? CompilationErrors { get; set; }
    
    // Metadata
    public Dictionary<string, string> Metadata { get; set; } = new();
    public string? TypeName { get; set; }
    public string? MethodName { get; set; }
    
    // Timestamps
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Foreign keys
    public Guid? AgentId { get; set; }
    
    // Navigation
    public Agent? Agent { get; set; }
    public ICollection<ScriptVersion> Versions { get; set; } = new List<ScriptVersion>();
}

public enum ScriptType
{
    Action,
    Workflow,
    Agent,
    Test,
    Utility
}
```

### ScriptVersion Entity

```csharp
public class ScriptVersion
{
    public Guid Id { get; set; }
    public Guid ScriptId { get; set; }
    public string Version { get; set; } = string.Empty;
    public string SourceCode { get; set; } = string.Empty;
    public string? ChangeDescription { get; set; }
    public byte[]? CompiledAssembly { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    // Navigation
    public Script Script { get; set; } = null!;
}
```

### ExplorationSession Entity

```csharp
public class ExplorationSession
{
    public Guid Id { get; set; }
    public string TargetApplication { get; set; } = string.Empty;
    public string? InstructionManual { get; set; }
    public ExplorationStatus Status { get; set; }
    public float Progress { get; set; }
    
    // Goals
    public List<ExplorationGoal> Goals { get; set; } = new();
    public List<string> CompletedGoals { get; set; } = new();
    public List<string> FailedGoals { get; set; } = new();
    
    // Timestamps
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    // Output
    public Guid? GeneratedAgentId { get; set; }
    
    // Navigation
    public ICollection<ExplorationResult> Results { get; set; } = new List<ExplorationResult>();
    public Agent? GeneratedAgent { get; set; }
}

public class ExplorationGoal
{
    public string Id { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> TargetElements { get; set; } = new();
    public List<string> RequiredActions { get; set; } = new();
    public string SuccessCriteria { get; set; } = string.Empty;
    public List<string> Dependencies { get; set; } = new();
}

public enum ExplorationStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Cancelled
}
```

### ExplorationResult Entity

```csharp
public class ExplorationResult
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public ExplorationResultType Type { get; set; }
    
    // Content based on type
    public string? WindowTitle { get; set; }
    public string? ElementData { get; set; }  // JSON
    public string? ActionTestResult { get; set; }  // JSON
    public string? NavigationPath { get; set; }  // JSON
    
    // Screenshots
    public byte[]? Screenshot { get; set; }
    public string? OcrText { get; set; }
    
    public DateTime CapturedAt { get; set; }
    
    // Navigation
    public ExplorationSession Session { get; set; } = null!;
}

public enum ExplorationResultType
{
    Window,
    Element,
    ActionTest,
    NavigationPath,
    Error
}
```

### ExecutionRecord Entity

```csharp
public class ExecutionRecord
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public string? UserId { get; set; }
    public string? SessionId { get; set; }
    
    public string TaskDescription { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Summary { get; set; }
    
    // Timing
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public int DurationMs { get; set; }
    
    // Results
    public string? ResultData { get; set; }  // JSON
    public List<string> Logs { get; set; } = new();
    
    // Navigation
    public Agent Agent { get; set; } = null!;
    public ICollection<ExecutionStep> Steps { get; set; } = new List<ExecutionStep>();
}
```

### ExecutionStep Entity

```csharp
public class ExecutionStep
{
    public Guid Id { get; set; }
    public Guid ExecutionId { get; set; }
    public int Order { get; set; }
    
    public string Action { get; set; } = string.Empty;
    public string? Parameters { get; set; }  // JSON
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Result { get; set; }  // JSON
    
    public int DurationMs { get; set; }
    public byte[]? Screenshot { get; set; }
    
    // Navigation
    public ExecutionRecord Execution { get; set; } = null!;
}
```

### Configuration Entity

```csharp
public class Configuration
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ConfigurationType Type { get; set; }
    public bool IsEncrypted { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum ConfigurationType
{
    String,
    Integer,
    Boolean,
    Json,
    Secret
}
```

## Database Schema (SQL)

```sql
-- Agents table
CREATE TABLE agents (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL,
    description TEXT,
    target_application VARCHAR(255) NOT NULL,
    active_version VARCHAR(50) DEFAULT '1.0.0',
    status VARCHAR(50) DEFAULT 'Active',
    capabilities JSONB DEFAULT '[]',
    instruction_list TEXT,
    metadata JSONB DEFAULT '{}',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    last_executed_at TIMESTAMP WITH TIME ZONE,
    
    CONSTRAINT uq_agents_name UNIQUE (name)
);

CREATE INDEX idx_agents_target_app ON agents(target_application);
CREATE INDEX idx_agents_status ON agents(status);

-- Agent versions table
CREATE TABLE agent_versions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    agent_id UUID NOT NULL REFERENCES agents(id) ON DELETE CASCADE,
    version VARCHAR(50) NOT NULL,
    notes TEXT,
    is_active BOOLEAN DEFAULT false,
    instruction_list_snapshot TEXT,
    capabilities_snapshot JSONB DEFAULT '[]',
    script_ids_snapshot JSONB DEFAULT '[]',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    
    CONSTRAINT uq_agent_versions UNIQUE (agent_id, version)
);

CREATE INDEX idx_agent_versions_agent ON agent_versions(agent_id);

-- Scripts table
CREATE TABLE scripts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL,
    description TEXT,
    source_code TEXT NOT NULL,
    current_version VARCHAR(50) DEFAULT '1.0.0',
    type VARCHAR(50) NOT NULL,
    compiled_assembly BYTEA,
    last_compiled_at TIMESTAMP WITH TIME ZONE,
    compilation_errors TEXT,
    metadata JSONB DEFAULT '{}',
    type_name VARCHAR(255),
    method_name VARCHAR(255),
    agent_id UUID REFERENCES agents(id) ON DELETE SET NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    
    CONSTRAINT uq_scripts_name UNIQUE (name)
);

CREATE INDEX idx_scripts_type ON scripts(type);
CREATE INDEX idx_scripts_agent ON scripts(agent_id);

-- Script versions table
CREATE TABLE script_versions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    script_id UUID NOT NULL REFERENCES scripts(id) ON DELETE CASCADE,
    version VARCHAR(50) NOT NULL,
    source_code TEXT NOT NULL,
    change_description TEXT,
    compiled_assembly BYTEA,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    
    CONSTRAINT uq_script_versions UNIQUE (script_id, version)
);

CREATE INDEX idx_script_versions_script ON script_versions(script_id);

-- Exploration sessions table
CREATE TABLE exploration_sessions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    target_application VARCHAR(255) NOT NULL,
    instruction_manual TEXT,
    status VARCHAR(50) DEFAULT 'Pending',
    progress REAL DEFAULT 0,
    goals JSONB DEFAULT '[]',
    completed_goals JSONB DEFAULT '[]',
    failed_goals JSONB DEFAULT '[]',
    started_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    completed_at TIMESTAMP WITH TIME ZONE,
    generated_agent_id UUID REFERENCES agents(id) ON DELETE SET NULL
);

CREATE INDEX idx_exploration_sessions_status ON exploration_sessions(status);
CREATE INDEX idx_exploration_sessions_target ON exploration_sessions(target_application);

-- Exploration results table
CREATE TABLE exploration_results (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id UUID NOT NULL REFERENCES exploration_sessions(id) ON DELETE CASCADE,
    type VARCHAR(50) NOT NULL,
    window_title VARCHAR(255),
    element_data JSONB,
    action_test_result JSONB,
    navigation_path JSONB,
    screenshot BYTEA,
    ocr_text TEXT,
    captured_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_exploration_results_session ON exploration_results(session_id);
CREATE INDEX idx_exploration_results_type ON exploration_results(type);

-- Execution records table
CREATE TABLE execution_records (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    agent_id UUID NOT NULL REFERENCES agents(id) ON DELETE CASCADE,
    user_id VARCHAR(255),
    session_id VARCHAR(255),
    task_description TEXT NOT NULL,
    success BOOLEAN DEFAULT false,
    error_message TEXT,
    summary TEXT,
    started_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    completed_at TIMESTAMP WITH TIME ZONE,
    duration_ms INTEGER DEFAULT 0,
    result_data JSONB,
    logs JSONB DEFAULT '[]'
);

CREATE INDEX idx_execution_records_agent ON execution_records(agent_id);
CREATE INDEX idx_execution_records_user ON execution_records(user_id);
CREATE INDEX idx_execution_records_started ON execution_records(started_at);

-- Execution steps table
CREATE TABLE execution_steps (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    execution_id UUID NOT NULL REFERENCES execution_records(id) ON DELETE CASCADE,
    step_order INTEGER NOT NULL,
    action VARCHAR(255) NOT NULL,
    parameters JSONB,
    success BOOLEAN DEFAULT false,
    error TEXT,
    result JSONB,
    duration_ms INTEGER DEFAULT 0,
    screenshot BYTEA
);

CREATE INDEX idx_execution_steps_execution ON execution_steps(execution_id);

-- Configuration table
CREATE TABLE configurations (
    key VARCHAR(255) PRIMARY KEY,
    value TEXT NOT NULL,
    description TEXT,
    type VARCHAR(50) DEFAULT 'String',
    is_encrypted BOOLEAN DEFAULT false,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);
```

## Repository Interfaces

```csharp
public interface IAgentRepository
{
    Task<Agent?> GetByIdAsync(Guid id);
    Task<Agent?> GetByNameAsync(string name);
    Task<IReadOnlyList<Agent>> GetAllAsync(AgentFilter? filter = null);
    Task<Agent> CreateAsync(Agent agent);
    Task<Agent> UpdateAsync(Agent agent);
    Task DeleteAsync(Guid id);
    
    // Versions
    Task<AgentVersion> CreateVersionAsync(Guid agentId, string notes);
    Task<IReadOnlyList<AgentVersion>> GetVersionsAsync(Guid agentId);
    Task SetActiveVersionAsync(Guid agentId, string version);
}

public interface IScriptRepository
{
    Task<Script?> GetByIdAsync(Guid id);
    Task<Script?> GetByNameAsync(string name);
    Task<IReadOnlyList<Script>> GetByAgentIdAsync(Guid agentId);
    Task<IReadOnlyList<Script>> GetAllAsync(ScriptFilter? filter = null);
    Task<Script> SaveAsync(Script script);
    Task DeleteAsync(Guid id);
    
    // Versions
    Task<ScriptVersion> CreateVersionAsync(Guid scriptId, string sourceCode, string? description);
    Task<IReadOnlyList<ScriptVersion>> GetVersionsAsync(Guid scriptId);
    
    // Compilation cache
    Task<byte[]?> GetCompiledAssemblyAsync(Guid scriptId, string version);
    Task SaveCompiledAssemblyAsync(Guid scriptId, string version, byte[] assembly);
}

public interface IExplorationRepository
{
    Task<ExplorationSession?> GetSessionAsync(Guid id);
    Task<IReadOnlyList<ExplorationSession>> GetSessionsAsync(ExplorationFilter? filter = null);
    Task<ExplorationSession> CreateSessionAsync(ExplorationSession session);
    Task<ExplorationSession> UpdateSessionAsync(ExplorationSession session);
    
    Task AddResultAsync(Guid sessionId, ExplorationResult result);
    Task<IReadOnlyList<ExplorationResult>> GetResultsAsync(Guid sessionId);
}

public interface IExecutionRepository
{
    Task<ExecutionRecord> RecordExecutionAsync(ExecutionRecord record);
    Task<IReadOnlyList<ExecutionRecord>> GetHistoryAsync(Guid agentId, int limit = 100, int offset = 0);
    Task<ExecutionRecord?> GetExecutionAsync(Guid id);
    Task<int> GetTotalExecutionsAsync(Guid agentId);
}
```

## Database Context

```csharp
public class CascadeDbContext : DbContext
{
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<AgentVersion> AgentVersions => Set<AgentVersion>();
    public DbSet<Script> Scripts => Set<Script>();
    public DbSet<ScriptVersion> ScriptVersions => Set<ScriptVersion>();
    public DbSet<ExplorationSession> ExplorationSessions => Set<ExplorationSession>();
    public DbSet<ExplorationResult> ExplorationResults => Set<ExplorationResult>();
    public DbSet<ExecutionRecord> ExecutionRecords => Set<ExecutionRecord>();
    public DbSet<ExecutionStep> ExecutionSteps => Set<ExecutionStep>();
    public DbSet<Configuration> Configurations => Set<Configuration>();
    
    public CascadeDbContext(DbContextOptions<CascadeDbContext> options) : base(options)
    {
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Apply configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CascadeDbContext).Assembly);
        
        // JSON columns for PostgreSQL
        modelBuilder.Entity<Agent>()
            .Property(a => a.Capabilities)
            .HasColumnType("jsonb");
        
        modelBuilder.Entity<Agent>()
            .Property(a => a.Metadata)
            .HasColumnType("jsonb");
        
        // Indexes
        modelBuilder.Entity<Agent>()
            .HasIndex(a => a.Name)
            .IsUnique();
        
        modelBuilder.Entity<Script>()
            .HasIndex(s => s.Name)
            .IsUnique();
    }
}
```

## Database Configuration

```csharp
public class DatabaseOptions
{
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.SQLite;
    public string ConnectionString { get; set; } = "Data Source=cascade.db";
    
    // SQLite specific
    public string? SqliteFilePath { get; set; }
    
    // PostgreSQL specific
    public string? Host { get; set; }
    public int Port { get; set; } = 5432;
    public string? Database { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool UseSsl { get; set; } = true;
    
    // Migration
    public bool AutoMigrate { get; set; } = true;
    
    // Performance
    public int MaxPoolSize { get; set; } = 100;
    public int CommandTimeout { get; set; } = 30;
}

public enum DatabaseProvider
{
    SQLite,
    PostgreSQL
}
```

## Migration Support

```csharp
public static class DatabaseMigrator
{
    public static async Task MigrateAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CascadeDbContext>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        
        if (options.AutoMigrate)
        {
            await context.Database.MigrateAsync();
        }
    }
}
```



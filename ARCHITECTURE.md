# Cascade Architecture

## Overview

Cascade is an AI-powered agent builder that automates the creation of specialized Windows application agents. It uses Microsoft UI Automation combined with AI exploration to understand applications, then generates automation code and creates purpose-built agents that can interact with those applications through natural language.

## System Components

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                            Cascade Architecture                              │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌────────────────────────────────────────────────────────────────────────┐ │
│  │                      Python Agent Layer (LangGraph)                     │ │
│  │                                                                         │ │
│  │  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐        │ │
│  │  │    Explorer     │  │     Builder     │  │     Runtime     │        │ │
│  │  │     Agent       │  │      Agent      │  │      Agent      │        │ │
│  │  │                 │  │                 │  │                 │        │ │
│  │  │ - Plan creation │  │ - Code synthesis│  │ - Dynamic load  │        │ │
│  │  │ - UI discovery  │  │ - Agent specs   │  │ - Task execute  │        │ │
│  │  │ - Knowledge acc │  │ - Validation    │  │ - Conversation  │        │ │
│  │  └────────┬────────┘  └────────┬────────┘  └────────┬────────┘        │ │
│  │           │                    │                    │                  │ │
│  │  ┌────────┴────────────────────┴────────────────────┴────────┐        │ │
│  │  │                   LLM Abstraction Layer                    │        │ │
│  │  │          (OpenAI / Anthropic / Azure OpenAI)               │        │ │
│  │  └────────────────────────────┬───────────────────────────────┘        │ │
│  └───────────────────────────────┼────────────────────────────────────────┘ │
│                                  │                                           │
│                            gRPC  │  (Bidirectional Streaming)                │
│                                  │                                           │
│  ┌───────────────────────────────┼────────────────────────────────────────┐ │
│  │                      C# Backend Layer                                   │ │
│  │                               │                                         │ │
│  │  ┌────────────────────────────┴───────────────────────────────┐        │ │
│  │  │                      gRPC Server                            │        │ │
│  │  │              (Cascade.Grpc.Server)                          │        │ │
│  │  └──────┬─────────────────┬─────────────────┬─────────────────┘        │ │
│  │         │                 │                 │                           │ │
│  │  ┌──────┴──────┐   ┌──────┴──────┐   ┌──────┴──────┐                   │ │
│  │  │     UI      │   │   Vision    │   │   CodeGen   │                   │ │
│  │  │ Automation  │   │   & OCR     │   │  & Script   │                   │ │
│  │  │             │   │             │   │             │                   │ │
│  │  │ - Elements  │   │ - Screenshot│   │ - Templates │                   │ │
│  │  │ - Actions   │   │ - OCR       │   │ - Compiler  │                   │ │
│  │  │ - Patterns  │   │ - Analysis  │   │ - Execution │                   │ │
│  │  └─────────────┘   └─────────────┘   └─────────────┘                   │ │
│  │                                                                         │ │
│  │  ┌──────────────────────────────────────────────────────────────┐      │ │
│  │  │                     Core Services                             │      │ │
│  │  │  ┌────────────────┐  ┌────────────────┐  ┌────────────────┐  │      │ │
│  │  │  │   Database     │  │  Configuration │  │    Logging     │  │      │ │
│  │  │  │ (SQLite/PgSQL) │  │   Management   │  │  & Telemetry   │  │      │ │
│  │  │  └────────────────┘  └────────────────┘  └────────────────┘  │      │ │
│  │  └──────────────────────────────────────────────────────────────┘      │ │
│  └────────────────────────────────────────────────────────────────────────┘ │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Component Details

### C# Backend Layer

The C# backend is responsible for all Windows-specific operations and provides services to the Python agent layer via gRPC.

#### Cascade.Core
- **Purpose**: Shared abstractions, models, and interfaces
- **Key Components**:
  - `IElement` - UI element abstraction
  - `IAction` - Action execution interface
  - `AgentDefinition` - Agent metadata model
  - Common DTOs and value objects

#### Cascade.UIAutomation
- **Purpose**: Microsoft UI Automation wrapper
- **Key Components**:
  - `ElementDiscovery` - Find elements by various criteria
  - `TreeWalker` - Navigate UI hierarchy
  - `ActionExecutor` - Click, type, scroll operations
  - `PatternProvider` - Support for UIA patterns (Invoke, Value, etc.)

#### Cascade.Vision
- **Purpose**: Visual analysis and OCR
- **Key Components**:
  - `ScreenCapture` - Screenshot functionality
  - `OcrEngine` - Text extraction (Windows OCR + Tesseract)
  - `ElementAnalyzer` - Visual element detection
  - `ChangeDetector` - UI change monitoring

#### Cascade.CodeGen
- **Purpose**: Dynamic code generation and execution
- **Key Components**:
  - `TemplateEngine` - C# code templates
  - `RoslynCompiler` - Runtime compilation
  - `ScriptExecutor` - Sandboxed execution
  - `ActionRecorder` - Capture and replay

#### Cascade.Database
- **Purpose**: Data persistence
- **Key Components**:
  - Entity Framework Core context
  - Repository implementations
  - Migration management
  - Support for SQLite (local) and PostgreSQL (distributed)

#### Cascade.Grpc.Server
- **Purpose**: gRPC service host
- **Key Components**:
  - Service implementations
  - Streaming handlers
  - Authentication interceptors
  - Error handling middleware

#### Cascade.CLI
- **Purpose**: Command-line interface
- **Key Components**:
  - Agent management commands
  - Server control
  - Configuration utilities

### Python Agent Layer

The Python layer implements the AI agents using LangGraph for stateful, graph-based agent workflows.

#### Explorer Agent
- **Purpose**: Understand applications through exploration
- **Workflow**:
  1. **Planning**: Parse instruction manual, decompose into exploration goals
  2. **Discovery**: Systematically explore UI tree and visual elements
  3. **Learning**: Test actions, record outcomes, build knowledge base
  4. **Synthesis**: Compile findings into structured application model

#### Builder Agent
- **Purpose**: Generate specialized agents
- **Workflow**:
  1. **Analysis**: Review exploration results and requirements
  2. **Design**: Create agent specification and capability list
  3. **Generation**: Synthesize automation code and agent definition
  4. **Validation**: Test generated agent against requirements

#### Runtime Agent
- **Purpose**: Execute tasks using generated agents
- **Workflow**:
  1. **Loading**: Dynamically load agent from database
  2. **Planning**: Interpret user request, create execution plan
  3. **Execution**: Run automation code via gRPC
  4. **Recovery**: Handle errors and adapt to UI changes

#### LLM Abstraction
- **Purpose**: Unified interface for multiple LLM providers
- **Features**:
  - Provider-agnostic API
  - Automatic fallback chains
  - Token management and rate limiting
  - Cost tracking

## Data Flow

### Agent Creation Flow
```
┌──────────────┐     ┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│  Instruction │────▶│   Explorer   │────▶│   Builder    │────▶│   Database   │
│    Manual    │     │    Agent     │     │    Agent     │     │   Storage    │
└──────────────┘     └──────────────┘     └──────────────┘     └──────────────┘
                            │                    │
                            ▼                    ▼
                     ┌──────────────┐     ┌──────────────┐
                     │  C# Backend  │     │  Generated   │
                     │  (UI/Vision) │     │    Code      │
                     └──────────────┘     └──────────────┘
```

### Agent Execution Flow
```
┌──────────────┐     ┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│    User      │────▶│   Runtime    │────▶│  C# Backend  │────▶│   Target     │
│   Request    │     │    Agent     │     │  (Execute)   │     │ Application  │
└──────────────┘     └──────────────┘     └──────────────┘     └──────────────┘
                            │                    │
                            ▼                    ▼
                     ┌──────────────┐     ┌──────────────┐
                     │   Database   │     │   Results    │
                     │   (Load)     │     │   & State    │
                     └──────────────┘     └──────────────┘
```

## Communication Protocol

### gRPC Services

| Service | Purpose | Streaming |
|---------|---------|-----------|
| `UIAutomationService` | Element discovery and actions | Server streaming for tree walks |
| `VisionService` | Screenshots and OCR | Unary |
| `CodeGenService` | Code generation and execution | Unary |
| `AgentService` | Agent CRUD operations | Unary |
| `SessionService` | Session management | Bidirectional streaming |

### Message Flow
1. Python agents call C# services via gRPC
2. C# executes operations and returns results
3. Streaming used for real-time UI monitoring
4. All operations are logged for debugging

## Security Considerations

### Script Sandboxing
- Generated C# scripts run in isolated AppDomains
- Limited file system and network access
- Resource consumption limits

### Authentication
- gRPC channel authentication
- API key management for LLM providers
- Credential encryption at rest

### Audit Logging
- All agent actions logged
- Execution history tracked
- Error states recorded

## Scalability

### Local Mode (SQLite)
- Single-user, single-machine
- Embedded database
- No additional infrastructure

### Distributed Mode (PostgreSQL)
- Multi-user support
- Shared agent repository
- Horizontal scaling of C# backends
- Load balancing for gRPC services

## Technology Stack

### C# Backend
- .NET 8.0
- gRPC (Grpc.AspNetCore)
- Entity Framework Core
- Microsoft.Windows.SDK.Contracts (UI Automation)
- Windows.Media.Ocr (OCR)
- Roslyn (Code compilation)

### Python Agent Layer
- Python 3.11+
- LangGraph
- grpcio
- OpenAI / Anthropic SDKs
- Pydantic (validation)

### Infrastructure
- Docker (containerization)
- PostgreSQL (distributed mode)
- SQLite (local mode)



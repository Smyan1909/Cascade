# Cascade Architecture

## Overview

Cascade is an AI-powered agent builder that automates the creation of specialized Windows application agents. It uses Microsoft UI Automation combined with AI exploration to understand applications, then generates automation code and creates purpose-built agents that can interact with those applications through natural language.

## Hidden Desktop Automation Requirements

To ensure users can continue working on their primary desktop while Cascade automates applications, the platform now satisfies the following requirements:

- **Virtual Desktop Isolation**: All automation (exploration and execution) runs inside a hidden Windows Virtual Desktop session that mirrors the target application environment without showing UI on the user’s main desktop.
- **Virtual Input Devices**: Automation interacts with applications through a dedicated virtual keyboard and virtual mouse bound to the hidden session so no physical input is hijacked.
- **Concurrent User Control**: The user retains complete control of their desktop and can continue using the target application after automation completes. The system synchronizes any state changes made inside the hidden session back to the primary session when needed (e.g., using roaming user profiles or cloud storage).
- **Session Lifecycle Management**: Sessions are created, monitored, and destroyed on demand. Long-running tasks reuse existing sessions when possible, and failures are contained within the hidden desktop.
- **Safety & Auditability**: Every automation run is scoped to a session identifier, allowing precise auditing, throttling, and cleanup without affecting the user workspace.

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
│                      gRPC Session Channel (Bidirectional)                    │
│                                  │                                           │
│  ┌───────────────────────────────┼────────────────────────────────────────┐ │
│  │                     gRPC + Session Services                             │ │
│  │  ┌──────────────────────┐  ┌────────────────────────────────────────┐  │ │
│  │  │ SessionService       │  │ Cascade.Grpc.Server                    │  │ │
│  │  │ - Create/attach      │  │ - UIAutomationService                  │  │ │
│  │  │ - Monitor/teardown   │  │ - VisionService                        │  │ │
│  │  │ - Resource quotas    │  │ - CodeGenService                       │  │ │
│  │  └─────────┬────────────┘  │ - AgentService                         │  │ │
│  │            │               └────────────────────────────────────────┘  │ │
│  └────────────┼──────────────────────────────────────────────────────────┘ │
│               │                                                              │
│  ┌────────────┴────────────┐   ┌──────────────────────────────────────────┐ │
│  │  Session Orchestrator   │   │         Core Automation Services        │ │
│  │  (Virtual Desktop Host) │   │ (UI Automation • Vision • CodeGen)      │ │
│  │  - Virtual desktop API  │   │ - Session-targeted UIA calls            │ │
│  │  - Virtual mouse/keys   │   │ - Off-screen capture                    │ │
│  │  - Session health       │   │ - Script execution                      │ │
│  └────────────┬────────────┘   └──────────────────────────────────────────┘ │
│               │                                                              │
│  ┌────────────┴──────────────────────────────────────────────────────────┐ │
│  │         Hidden Automation Desktops (per session)                      │ │
│  │  - Virtual display + input pipeline                                   │ │
│  │  - Target applications run isolated from user desktop                 │ │
│  │  - State synchronized when required                                   │ │
│  └────────────┬──────────────────────────────────────────────────────────┘ │
│               │                                                              │
│        Target Windows Applications (user can keep primary desktop active)    │
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

#### Session Orchestrator
- **Purpose**: Manage hidden Windows Virtual Desktop sessions and virtual input pipelines
- **Key Components**:
  - `SessionManager` – creates, reuses, and tears down hidden desktops bound to automation runs
  - `VirtualDesktopHost` – interfaces with the Windows Virtual Desktop API and isolates user profiles
  - `InputRouter` – exposes virtual keyboard/mouse drivers scoped to the session so physical input remains untouched
  - `SessionHealthMonitor` – restarts unhealthy sessions and enforces runtime quotas

#### Cascade.UIAutomation
- **Purpose**: Microsoft UI Automation wrapper
- **Key Components**:
  - `ElementDiscovery` - Find elements by various criteria within a specific session handle
  - `TreeWalker` - Navigate UI hierarchy off-screen using session-bound automation peers
  - `ActionExecutor` - Dispatch virtual clicks, typing, and scrolling through the InputRouter
  - `PatternProvider` - Support for UIA patterns (Invoke, Value, etc.) with session affinity tokens

#### Cascade.Vision
- **Purpose**: Visual analysis and OCR
- **Key Components**:
  - `ScreenCapture` - Off-screen capture pipeline that pulls frames from hidden desktops
  - `OcrEngine` - Text extraction (Windows OCR + Tesseract)
  - `ElementAnalyzer` - Visual element detection
  - `ChangeDetector` - UI change monitoring

#### Cascade.CodeGen
- **Purpose**: Dynamic code generation and execution
- **Key Components**:
  - `TemplateEngine` - C# code templates
  - `RoslynCompiler` - Runtime compilation
  - `ScriptExecutor` - Sandboxed execution that injects session/context tokens into every call
  - `ActionRecorder` - Capture and replay

#### Cascade.Database
- **Purpose**: Data persistence
- **Key Components**:
  - Entity Framework Core context
  - Repository implementations
  - Migration management
  - Support for SQLite (local) and PostgreSQL (distributed)
  - Session metadata tables (session ids, quotas, run history)

#### Cascade.Grpc.Server
- **Purpose**: gRPC service host
- **Key Components**:
  - Service implementations
  - Streaming handlers
  - Authentication interceptors
  - Error handling middleware
  - `SessionService` façade that exposes create/attach/detach operations to Python agents

#### Cascade.CLI
- **Purpose**: Command-line interface
- **Key Components**:
  - Agent management commands
  - Server & session host control
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
  0. **Session Acquire**: Request or attach to a hidden virtual desktop session through SessionService
  1. **Loading**: Dynamically load agent from database
  2. **Planning**: Interpret user request, create execution plan
  3. **Execution**: Run automation code via gRPC targeting the assigned session
  4. **Recovery**: Handle errors, rehydrate sessions, and adapt to UI changes
  5. **Release**: Persist results and gracefully release the session so the user can resume manual control

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
                     │ Session      │     │  C# Backend  │
                     │ Orchestrator │     │ (UI/Vision/  │
                     │ (Hidden VDs) │     │   CodeGen)   │
                     └──────┬───────┘     └──────┬───────┘
                            │                    │
                            ▼                    ▼
                     ┌──────────────┐     ┌──────────────┐
                     │ Hidden Desk  │     │  Generated   │
                     │  Automation  │     │    Code      │
                     └──────────────┘     └──────────────┘
```
1. Explorer Agent requests a hidden desktop session before probing the UI.
2. All UI inspection happens inside the session via virtual input/output.
3. Builder Agent consumes captured knowledge to produce automation code.
4. Database stores both the generated agents and metadata about the session used.

### Agent Execution Flow
```
┌──────────────┐     ┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│    User      │────▶│   Runtime    │────▶│ Session +    │────▶│ Hidden Desk  │
│   Request    │     │    Agent     │     │ gRPC Services│     │  Applications│
└──────────────┘     └──────────────┘     └──────────────┘     └──────┬───────┘
                            │                    │                     │
                            ▼                    ▼                     │
                     ┌──────────────┐     ┌──────────────┐             │
                     │   Database   │     │   Results    │             │
                     │   (Load)     │     │   & State    │             │
                     └──────────────┘     └──────────────┘             │
                                                                         ▼
                                                                User resumes app
```
1. Runtime Agent loads a specialized agent and requests/attaches to a session.
2. gRPC + SessionService route every automation call to that hidden desktop.
3. Results and logs are persisted; when finished, the session is released so the user can continue manual work.

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
2. Each automation RPC includes a `session_id` so the backend routes calls to the correct hidden desktop
3. C# executes operations and returns results
4. Streaming used for real-time UI monitoring
5. All operations are logged for debugging

## Security Considerations

### Script Sandboxing
- Generated C# scripts run in isolated AppDomains
- Limited file system and network access
- Resource consumption limits
- Automation always occurs inside hidden desktop sessions so pointer/keyboard events never leak to the user’s workspace

### Authentication
- gRPC channel authentication
- API key management for LLM providers
- Credential encryption at rest

### Audit Logging
- All agent actions logged
- Execution history tracked
- Error states recorded
- Session lifecycle events (create/attach/detach) recorded for post-mortem analysis

## Scalability

### Local Mode (SQLite)
- Single-user, single-machine
- Embedded database
- No additional infrastructure
- Session orchestrator spins up a single hidden desktop alongside the primary desktop

### Distributed Mode (PostgreSQL)
- Multi-user support
- Shared agent repository
- Horizontal scaling of C# backends
- Load balancing for gRPC services
- Session pools allow multiple hidden desktops per automation host with quota enforcement

## Technology Stack

### C# Backend
- .NET 8.0
- gRPC (Grpc.AspNetCore)
- Entity Framework Core
- Microsoft.Windows.SDK.Contracts (UI Automation)
- Windows.Media.Ocr (OCR)
- Roslyn (Code compilation)
- Windows Virtual Desktop / VirtualDisplay APIs
- Virtual HID (keyboard/mouse) drivers

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
- Windows Virtual Desktop host components (RDP stack, Hyper-V optional)



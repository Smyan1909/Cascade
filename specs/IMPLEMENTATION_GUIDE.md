# Cascade Implementation Guide

## Recommended Implementation Order

```
Phase 1: Foundation (C# Core)
├── 1. Database Schema (09)
├── 2. UI Automation (01)
└── 3. Vision/OCR (02)

Phase 2: Code Generation & Communication
├── 4. CodeGen/Scripting (03)
└── 5. gRPC Protocol (04)

Phase 3: Python Agent Infrastructure  
├── 6. LLM Abstraction (08)
└── 7. Explorer Agent (05)

Phase 4: Agent Generation & Execution
├── 8. Builder Agent (06)
└── 9. Agent Runtime (07)

Phase 5: Deployment
└── 10. Distribution (10)
```

---

## Hidden Desktop Automation Requirements

From this point forward, every module must satisfy these global constraints:

1. **Automation in Hidden Sessions** – All UI automation runs inside Windows Virtual Desktop sessions hosted by the Cascade Session Host service. The user keeps full control of their physical desktop.
2. **Virtual Input Only** – UIAutomation and Vision modules interact through virtual keyboard/mouse devices scoped to the session; physical input is never hijacked.
3. **Session Lifecycle** – Explorer, Builder, and Runtime agents must request, monitor, and release sessions via the new `SessionService` gRPC API. Failures must trigger reacquire/retry logic.
4. **State Sync for Users** – After automation completes, the target application remains usable so the user can continue working or override the agent’s changes.
5. **Auditability** – Every RPC and database record carries a `session_id/run_id` so automation can be traced without exposing the hidden desktop.

Keep these requirements in mind while following the per-module guidance below.

---

## Module-by-Module Implementation Guide

### 1. Database Schema (Start Here)

**Why first?** Everything else needs persistence. Agents, scripts, exploration results, and execution history all need storage.

**Implementation approach:**
- Start with Entity Framework Core DbContext and entities
- Model automation session tables early (`AutomationSession`, `SessionEvent`) so hidden desktop metadata is first-class
- Implement SQLite first (simpler local development)
- Add PostgreSQL support after SQLite works
- Use EF Core migrations for schema management
- Create repository interfaces and implementations (including `ISessionRepository`)

**Testing protocol:**
- **Unit tests**: CRUD operations on each repository with in-memory SQLite
- **Integration tests**: Full database operations with actual SQLite file
- **Test data**: Create seed data fixtures for consistent testing
- **Validation**: Test entity constraints, unique indexes, foreign keys
- **Migration tests**: Verify migrations apply cleanly up and down
- **Session lifecycle tests**: Create/release session records, ensure execution records link correctly

**Success criteria:** Can persist agents, scripts, sessions, and execution history; migrations work reliably

---

### 2. UI Automation

**Why second?** This is the core capability that makes everything else possible. Without UI interaction, there's no agent.

**Implementation approach:**
- Start with `ElementDiscovery` scoped to a `SessionHandle`
- Implement `TreeWalker` for navigating the hidden desktop hierarchy
- Add action methods (click, type, scroll) via the virtual HID provider
- Implement pattern support (Invoke, Value, Toggle, etc.)
- Add element caching for performance
- Build `ElementLocator` for XPath-like queries
- Integrate the Session Orchestrator early so UIA calls fail fast without a session

**Testing protocol:**
- **Unit tests**: Mock the UIA COM interfaces, test your wrapper logic
- **Integration tests inside hidden session**: Launch Calculator/Notepad within a virtual desktop and drive them via virtual input *(deferred until the Session Host orchestration layer lands in Phase 3; current Phase 1 only validates the database + contract surface).*
  - Test finding Calculator window
  - Test clicking number buttons
  - Test reading display value
  - Test tree walking depth
- **Integration tests with Notepad**: For text input testing *(same deferment while we finish Session Host work).*
  - Test typing text
  - Test value patterns
  - Test menu navigation
- **Performance tests**: Measure tree walk times, cache hit rates
- **Stress tests**: Rapid element lookups, concurrent access
- **Session failover tests**: Kill the hidden desktop mid-run and ensure UIA recovers gracefully

> **Note:** Hidden-desktop automation tests are blocked until `SessionService` is fully implemented. Track re-enablement in Milestone 3 when the Explorer agent first consumes the service.

**Success criteria:** Can programmatically operate Calculator and Notepad reliably inside hidden sessions without affecting the user's desktop

---

### 3. Vision/OCR

**Why third?** Independent of UI Automation but needed for hybrid element finding. Can be developed in parallel.

**Implementation approach:**
- Start with `ScreenCapture` driven by virtual desktop duplication APIs
- Implement Windows OCR engine first (built-in, no dependencies)
- Add Tesseract as fallback
- Build change detection for UI monitoring
- Add image preprocessing pipeline for OCR accuracy
- Ensure capture coordinates are session-relative and independent of the user’s monitors

**Testing protocol:**
- **Unit tests**: Image processing functions, OCR result parsing
- **Integration tests**: 
  - Capture screenshots of known windows running inside a hidden desktop
  - OCR on images with known text (create test images)
  - Compare OCR accuracy between Windows OCR and Tesseract
- **Change detection tests**:
  - Capture baseline, make UI change, verify detection
  - Test threshold sensitivity
- **Visual regression tests**: Store expected screenshots, compare against actual
- **Session isolation tests**: Verify that capturing the hidden desktop never mirrors content to the primary desktop

**Success criteria:** OCR accuracy >95% on standard UI text; change detection works reliably within hidden desktops

---

### 4. CodeGen/Scripting

**Why fourth?** Depends on UI Automation abstractions for the generated code templates.

**Implementation approach:**
- Start with Scriban template engine setup
- Create templates for simple actions (click, type) that accept an `AutomationCallContext`
- Implement Roslyn compiler wrapper
- Build sandboxed execution environment that requires a valid session token before running code
- Add script versioning and persistence (uses Database)
- Create action recording capability

**Testing protocol:**
- **Unit tests**: Template rendering, code syntax validation
- **Compilation tests**: 
  - Compile valid code - should succeed
  - Compile invalid code - should fail gracefully with good errors
  - Test all template variations
- **Execution tests**:
  - Execute simple scripts in sandbox
  - Verify sandbox restrictions (no file access, no network)
  - Test timeout handling
- **Session injection tests**: Ensure generated code fails fast when session context is missing
- **Round-trip tests**: Record actions → generate code → compile → execute → verify same result

**Success criteria:** Can generate, compile, and execute session-scoped UI automation scripts safely

---

### 5. gRPC Protocol

**Why fifth?** Exposes all C# services to Python. Depends on modules 1-4 being functional.

**Implementation approach:**
- Generate C# server code from proto files (including the new `SessionService`)
- Implement service wrappers for each module
- Add streaming for tree walking and UI monitoring
- Implement error handling interceptor
- Add logging interceptor
- Setup health checks
- Route every automation RPC through session middleware that injects/validates `session_id`

**Testing protocol:**
- **Unit tests**: Service method logic, request/response mapping
- **Integration tests**:
  - Start server, call each endpoint from test client
  - Test streaming endpoints (GetDescendants, CaptureTree)
  - Test error conditions return appropriate gRPC status codes
- **Session tests**: Create/destroy sessions via SessionService, ensure automation RPCs fail without a valid handle
- **Performance tests**: Measure latency for each call type
- **Connection tests**: Connect/disconnect cycles, reconnection handling
- **Load tests**: Concurrent requests, sustained throughput

**Success criteria:** All C# functionality (including session management) accessible via gRPC with <50ms latency for simple calls

---

### 6. LLM Abstraction (Python)

**Why sixth?** First Python component. Needed before any agent implementation.

**Implementation approach:**
- Implement base interfaces and types
- Create OpenAI provider first (most common)
- Add Anthropic provider
- Add Azure OpenAI provider
- Implement rate limiting middleware
- Add cost tracking middleware
- Build fallback chain logic
- Provide helpers to prepend “hidden desktop” system prompts so reasoning stays aligned with session constraints

**Testing protocol:**
- **Unit tests**: Mock API responses, test parsing and error handling
- **Provider tests** (with real API calls, use sparingly):
  - Simple completion request to each provider
  - Streaming completion
  - Tool/function calling
- **Fallback tests**: Mock primary failure, verify fallback activates
- **Rate limit tests**: Verify rate limiter prevents excess calls
- **Cost tracking tests**: Verify token counts and cost calculations
- **Prompt context tests**: Ensure session metadata is inserted correctly and removed when not needed

**Success criteria:** Can get completions from all providers with automatic fallback while respecting hidden desktop context

---

### 7. Explorer Agent

**Why seventh?** First full agent. Uses all C# services + LLM. This is where the magic happens.

**Implementation approach:**
- Build the LangGraph state and graph structure first
- Add a `prepare_session` node to acquire a hidden desktop before exploration
- Implement instruction parsing node
- Implement planning node
- Build session-aware UI exploration tools (wrappers around gRPC client)
- Implement exploration loop
- Add action testing logic
- Build knowledge synthesis that releases the session when complete

**Testing protocol:**
- **Unit tests**: Individual node logic with mocked services
- **Graph tests**: Verify correct node transitions for various states
- **Integration tests with simple apps**:
  - Test on Calculator with instruction: "Understand how to add two numbers"
  - Test on Notepad with instruction: "Understand how to create and save a file"
- **Exploration quality tests**:
  - Did it find all expected elements?
  - Did it correctly identify actionable elements?
  - Is the generated application model accurate?
- **Convergence tests**: Does it eventually terminate? How many steps?
- **Session resilience tests**: Drop the session mid-run and verify the agent reacquires and resumes

**Success criteria:** Can explore Calculator inside a hidden desktop and produce accurate application models without disturbing the user’s workspace

---

### 8. Builder Agent

**Why eighth?** Depends on Explorer output. Generates the specialized agents.

**Implementation approach:**
- Build state and graph structure
- Implement design phase nodes
- Create C# code generation (uses CodeGen module)
- Implement Python agent code generation
- Build validation loop that reuses or acquires a hidden session before running smoke tests
- Add packaging logic (database storage)

**Testing protocol:**
- **Unit tests**: Code generation templates, validation logic
- **Integration tests**:
  - Feed it a known Explorer output for Calculator
  - Verify generated C# code compiles
  - Verify generated agent has expected capabilities
- **Code quality tests**:
  - Generated code follows conventions
  - No obvious errors or anti-patterns
- **End-to-end tests**: Explorer → Builder → verify generated agent is functional
- **Session tests**: Ensure sessions are released even when validation fails

**Success criteria:** Produces working agents from exploration results while running all validations inside hidden desktops

---

### 9. Agent Runtime

**Why ninth?** Executes agents produced by Builder. Final piece of the agent pipeline.

**Implementation approach:**
- Implement agent loader from database
- Build execution context management with session metadata
- Implement the plan-execute loop
- Add conversation memory
- Build error recovery strategies (including session reacquire/retry)
- Add execution history + session logging

**Testing protocol:**
- **Unit tests**: Loading logic, state management, error recovery
- **Integration tests**:
  - Load a pre-created agent
  - Execute simple tasks
  - Verify results match expectations
- **Conversation tests**: Multi-turn interactions maintain context
- **Recovery tests**: Inject failures, verify recovery works
- **Full pipeline test**: Explorer → Builder → Runtime → Execute task
- **Concurrent session tests**: Run multiple agents simultaneously and ensure users retain full control of desktop

**Success criteria:** Can load and execute generated agents reliably inside hidden desktops while leaving the user workspace untouched

---

### 10. Distribution

**Why last?** Packaging and deployment. Everything must work first.

**Implementation approach:**
- Create Windows installer (WiX or similar)
- Ship the Session Host Windows service + virtual drivers alongside the gRPC server
- Build Docker images (logic/services) while ensuring they can talk to Windows Session Host nodes
- Create docker-compose for full stack
- Add configuration management
- Implement health checks and monitoring (including session host metrics)
- Create installation scripts

**Testing protocol:**
- **Installation tests**: Fresh Windows VM, install, verify all components work
- **Docker tests**: Build images, run compose, verify services communicate
- **Upgrade tests**: Install v1, upgrade to v2, verify data preserved
- **Configuration tests**: Various config combinations work correctly
- **Uninstall tests**: Clean removal, no artifacts left behind
- **Session host tests**: Verify hidden desktops spin up, virtual input works, user desktop unaffected

**Success criteria:** Clean install on fresh Windows machine; Docker deployment works; Session Host runs reliably

---

## Cross-Cutting Testing Strategies

### Continuous Integration
- Run unit tests on every commit
- Run integration tests nightly
- Run full pipeline tests weekly

### Test Environment
- Keep a dedicated Windows VM/machine for UI Automation tests
- Use Docker for database and service tests
- Mock LLM calls in CI (real calls in manual testing)
- Maintain at least one Windows Session Host machine (with GPU) for integration tests

### Test Data Management
- Create standardized test applications (or use Calculator/Notepad)
- Maintain test instruction manuals
- Version control expected outputs

### Monitoring in Tests
- Log all gRPC calls during tests
- Capture screenshots on test failures
- Record LLM token usage
- Track session lifecycle events (create/heartbeat/release) to catch leaks

---

## Implementation Tips

1. **Build vertical slices**: Get Calculator working end-to-end before expanding
2. **Mock early**: Create mocks for C# services before they exist so Python development can proceed
3. **Fail fast**: Each module should validate inputs and fail with clear errors
4. **Log everything**: You'll need visibility into what the agents are "thinking"
5. **Version your agents**: Database stores versions so you can roll back
6. **Always release sessions**: No matter the outcome, hidden desktops must be cleaned up so users can resume manual control

---

## Dependency Graph

```
                          ┌─────────────────┐
                          │   Distribution  │
                          │      (10)       │
                          └────────┬────────┘
                                   │
                    ┌──────────────┼──────────────┐
                    │              │              │
                    ▼              ▼              ▼
             ┌──────────┐   ┌──────────┐   ┌──────────┐
             │  Agent   │   │ Builder  │   │ Explorer │
             │ Runtime  │   │  Agent   │   │  Agent   │
             │   (07)   │   │   (06)   │   │   (05)   │
             └────┬─────┘   └────┬─────┘   └────┬─────┘
                  │              │              │
                  └──────────────┼──────────────┘
                                 │
                          ┌──────┴──────┐
                          │ Session Host│
                          │ (Hidden VD) │
                          └──────┬──────┘
                                 │
                 ┌───────────────┼───────────────┐
                 │                               │
                 ▼                               ▼
          ┌──────────┐                   ┌──────────┐
          │   LLM    │                   │   gRPC   │
          │ Abstract │                   │ Protocol │
          │   (08)   │                   │   (04)   │
          └──────────┘                   └────┬─────┘
                                             │
                      ┌──────────────────────┼──────────────────────┐
                      │                      │                      │
                      ▼                      ▼                      ▼
               ┌──────────┐          ┌──────────┐          ┌──────────┐
               │ CodeGen  │          │  Vision  │          │    UI    │
               │   (03)   │          │   (02)   │          │  Auto    │
               └────┬─────┘          └────┬─────┘          └────┬─────┘
                    │                     │                     │
                    └──────────────────────┼─────────────────────┘
                                           │
                                    ┌──────┴──────┐
                                    │  Database   │
                                    │   Schema    │
                                    │    (09)     │
                                    └─────────────┘
```

---

## Milestone Checkpoints

### Milestone 1: C# Foundation Complete
- [ ] Database: CRUD operations work, migrations run
- [ ] UI Automation: Can operate Calculator
- [ ] Vision: Can capture screenshots and run OCR
- [ ] CodeGen: Can compile and execute simple scripts

### Milestone 2: Communication Layer Complete
- [ ] gRPC server runs and exposes all services
- [ ] Python client can call all endpoints
- [ ] Streaming works for tree walking
- [ ] SessionService can create/attach/release hidden desktops

### Milestone 3: First Agent Works
- [ ] LLM abstraction works with OpenAI
- [ ] Explorer can explore Calculator inside a hidden desktop
- [ ] Application model is generated

### Milestone 4: Full Pipeline Works
- [ ] Builder generates working agent from exploration
- [ ] Runtime can execute generated agent
- [ ] End-to-end: instruction → exploration → agent → task execution

### Milestone 5: Production Ready
- [ ] Distribution packages work
- [ ] Docker deployment works
- [ ] Documentation complete
- [ ] Session Host installer + drivers validated on clean Windows machines


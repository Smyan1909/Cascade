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

## Local Session Automation (current scope)

For now, automation runs in the current user session using standard UI Automation + SendInput. No hidden virtual desktops, no custom HID drivers. The agent may briefly take control of the desktop to perform actions. Hidden/isolated sessions are deferred.

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

**Why second?** Core capability for any agent action.

**Implementation approach:**
- Use `ElementDiscovery` and `TreeWalker` in the current user session/desktop.
- Actions (click, type, scroll) use standard SendInput.
- Implement patterns (Invoke, Value, Toggle, etc.) and element caching.
- Build `ElementLocator` for XPath-like queries.

**Testing protocol:**
- **Unit tests**: Mock UIA COM interfaces; test wrapper logic.
- **Integration tests (local session)**: Launch Calculator/Notepad in the current session; drive via UIA + SendInput.
  - Find Calculator window; click numbers; read display value.
  - Notepad: type text, value patterns, menu navigation.
- **Performance/stress**: tree walk times, cache hits, rapid lookups.

**Success criteria:** Can operate Calculator and Notepad reliably in the current user session.

---

### 3. Vision/OCR

**Why third?** Supports hybrid element finding.

**Implementation approach:**
- `ScreenCapture` using standard desktop duplication APIs in the current session.
- Use Windows OCR first; add Tesseract as fallback.
- Build change detection and preprocessing for accuracy.
- Coordinates are relative to the current desktop.

**Testing protocol:**
- **Unit tests**: Image processing, OCR parsing.
- **Integration (local session)**: Capture screenshots of known windows; OCR known text; compare OCR engines.
- **Change detection**: Baseline vs. modified window; threshold checks.
- **Visual regression**: Expected vs. actual screenshots.

**Success criteria:** OCR accuracy >95% on standard UI text; reliable change detection on the current desktop.

---

### 4. CodeGen/Scripting

**Why fourth?** Builds on UIA abstractions.

**Implementation approach:**
- Scriban templates for actions (click, type) targeting current-session UIA.
- Roslyn compiler wrapper; sandboxed execution (no file/network).
- Script versioning/persistence; action recording.

**Testing protocol:**
- **Unit**: Template rendering, syntax validation.
- **Compilation**: Valid/invalid code; all template variants.
- **Execution**: Execute simple scripts against current session; enforce timeouts.
- **Round-trip**: Record → generate → compile → execute → verify result.

**Success criteria:** Generate, compile, and execute scripts safely in the current session.

---

### 5. gRPC Protocol

**Why fifth?** Exposes services to Python/other clients.

**Implementation approach:**
- Generate server code from protos; SessionService can be a thin stub (current-session context) with future expansion.
- Service wrappers for UIA, Vision, CodeGen, Agent.
- Streaming for tree walking/monitoring; logging/error interceptors; health checks.
- Session middleware becomes optional/lenient for current-session; keep request metadata for audit if needed.

**Testing protocol:**
- **Unit**: Service logic, mappings.
- **Integration**: Start server; call endpoints (including streaming); verify status codes.
- **Performance/connection/load**: Latency, reconnects, concurrency.

**Success criteria:** All services callable with <50ms latency for simple calls; no hidden-session requirement.

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

**Why seventh?** First full agent using C# services + LLM.

**Implementation approach:**
- LangGraph state/graph; instruction parsing; planning.
- UI exploration tools call current-session UIA/Vision.
- Exploration loop; action testing; synthesize findings.

**Testing protocol:**
- **Unit**: Node logic with mocks.
- **Graph**: Transitions for various states.
- **Integration (current session)**: Calculator (“add two numbers”), Notepad (“create and save a file”).
- **Quality**: expected elements/actionability/model accuracy.
- **Convergence**: termination/steps.

**Success criteria:** Explore Calculator/Notepad in current session and produce accurate models.

---

### 8. Builder Agent

**Why eighth?** Builds agents from Explorer output.

**Implementation approach:**
- State/graph structure; design nodes.
- C# and Python code generation (using CodeGen).
- Validation loop runs in current session; package results.

**Testing protocol:**
- **Unit**: Templates, validation logic.
- **Integration**: Known Explorer output for Calculator; generated C# compiles; generated agent capabilities verified.
- **Quality**: conventions, no obvious errors.
- **End-to-end**: Explorer → Builder → functional agent.

**Success criteria:** Produces working agents validated in the current session.

---

### 9. Agent Runtime

**Why ninth?** Executes generated agents.

**Implementation approach:**
- Agent loader from DB; execution context in current session.
- Plan-execute loop; conversation memory; error recovery.
- Execution history + logging.

**Testing protocol:**
- **Unit**: Loading, state, recovery.
- **Integration**: Execute simple tasks; verify results.
- **Conversation**: Multi-turn context.
- **Recovery**: Inject failures; verify recovery.
- **Full pipeline**: Explorer → Builder → Runtime → Execute task.
- **Concurrency**: Multiple agents; current session access managed.

**Success criteria:** Load and execute agents reliably in the current session.

---

### 10. Distribution

**Why last?** Packaging and deployment.

**Implementation approach:**
- Create Windows installer (WiX or similar) for the current-session stack.
- Docker images for services where applicable.
- Configuration management; health checks; monitoring.
- Installation scripts; upgrade/uninstall validation.

**Testing protocol:**
- **Installation**: Fresh Windows VM, verify components work.
- **Docker**: Build/run compose (where applicable).
- **Upgrade**: v1 → v2 data preserved.
- **Configuration**: Various combos.
- **Uninstall**: Clean removal.

**Success criteria:** Clean install; services run locally in the current session model.

---

## Cross-Cutting Testing Strategies

### Continuous Integration
- Run unit tests on every commit
- Run integration tests nightly
- Run full pipeline tests weekly

### Test Environment
- Use a Windows machine/VM for UI Automation tests (current session).
- Use Docker for database/service tests.
- Mock LLM calls in CI (real calls in manual testing).

### Test Data Management
- Standardized test apps (Calculator/Notepad) and fixtures.
- Version control expected outputs.

### Monitoring in Tests
- Log gRPC calls; capture screenshots on failures; record LLM token usage.

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


# gRPC Protocol Specification

## Overview

The `Cascade.Grpc.Server` module defines the communication protocol between the Python agent layer and the C# backend. It uses gRPC for high-performance, strongly-typed communication with support for bidirectional streaming.

## Dependencies

```xml
<PackageReference Include="Grpc.AspNetCore" Version="2.59.0" />
<PackageReference Include="Google.Protobuf" Version="3.25.0" />
```

## Architecture

```
protos/
├── cascade.proto              # Common types and enums
├── session.proto              # Hidden desktop session orchestration
├── ui_automation.proto        # UI Automation service
├── vision.proto               # Vision/OCR service
├── codegen.proto              # Code generation service
└── agent.proto                # Agent management service

Cascade.Grpc.Server/
├── Services/
│   ├── UIAutomationGrpcService.cs
│   ├── VisionGrpcService.cs
│   ├── CodeGenGrpcService.cs
│   └── AgentGrpcService.cs
├── Interceptors/
│   ├── LoggingInterceptor.cs
│   ├── ErrorHandlingInterceptor.cs
│   └── AuthenticationInterceptor.cs
├── Mappers/
│   └── ProtobufMappers.cs
└── Startup/
    └── GrpcServerConfiguration.cs
```

## Service Definitions

### cascade.proto (Common Types)

```protobuf
syntax = "proto3";

package cascade;

option csharp_namespace = "Cascade.Grpc";

// Common result wrapper
message Result {
    bool success = 1;
    string error_message = 2;
    string error_code = 3;
}

// Rectangle for bounding boxes
message Rectangle {
    int32 x = 1;
    int32 y = 2;
    int32 width = 3;
    int32 height = 4;
}

// Point coordinates
message Point {
    int32 x = 1;
    int32 y = 2;
}

// Timestamp
message Timestamp {
    int64 seconds = 1;
    int32 nanos = 2;
}

// Key-value pair for metadata
message KeyValue {
    string key = 1;
    string value = 2;
}

// Session context propagated with every automation call
message SessionContext {
    string session_id = 1;
    string agent_id = 2;
    string run_id = 3;
}
```

### session.proto (Hidden Desktop Sessions)

```protobuf
syntax = "proto3";

package cascade.session;

import "cascade.proto";

option csharp_namespace = "Cascade.Grpc.Session";

service SessionService {
    rpc CreateSession(CreateSessionRequest) returns (SessionResponse);
    rpc AttachSession(AttachSessionRequest) returns (SessionResponse);
    rpc ReleaseSession(ReleaseSessionRequest) returns (cascade.Result);
    rpc Heartbeat(SessionHeartbeatRequest) returns (SessionResponse);
    rpc StreamEvents(SessionEventRequest) returns (stream SessionEvent);
}

message CreateSessionRequest {
    string agent_id = 1;
    string run_id = 2;
    VirtualDesktopProfile profile = 3;
}

message AttachSessionRequest {
    string session_id = 1;
    string agent_id = 2;
}

message ReleaseSessionRequest {
    string session_id = 1;
    string reason = 2;
}

message SessionHeartbeatRequest {
    string session_id = 1;
    SessionMetrics metrics = 2;
}

message SessionResponse {
    cascade.Result result = 1;
    SessionContext session = 2;
    VirtualDesktopProfile profile = 3;
    SessionState state = 4;
}

message SessionEventRequest {
    string agent_id = 1;
}

message SessionEvent {
    SessionContext session = 1;
    SessionState state = 2;
    string message = 3;
}

enum SessionState {
    SESSION_STATE_UNSPECIFIED = 0;
    SESSION_READY = 1;
    SESSION_IN_USE = 2;
    SESSION_DRAINING = 3;
    SESSION_TERMINATED = 4;
}

message VirtualDesktopProfile {
    int32 width = 1;
    int32 height = 2;
    int32 dpi = 3;
    bool enable_gpu = 4;
}

message SessionMetrics {
    double cpu_percent = 1;
    double memory_percent = 2;
    double input_latency_ms = 3;
}
```

### ui_automation.proto

> **Session Field**: All request messages include `SessionContext session = 100;` (omitted in the listings below for brevity). The backend rejects any automation call without a valid session.

```protobuf
syntax = "proto3";

package cascade.uiautomation;

import "cascade.proto";

option csharp_namespace = "Cascade.Grpc.UIAutomation";

// UI Automation Service
service UIAutomationService {
    // Element discovery
    rpc GetDesktopRoot(Empty) returns (ElementResponse);
    rpc GetForegroundWindow(Empty) returns (ElementResponse);
    rpc FindWindow(FindWindowRequest) returns (ElementResponse);
    rpc FindElement(FindElementRequest) returns (ElementResponse);
    rpc FindAllElements(FindElementRequest) returns (ElementListResponse);
    rpc WaitForElement(WaitForElementRequest) returns (ElementResponse);
    
    // Tree walking - server streaming for large trees
    rpc GetChildren(ElementRequest) returns (stream ElementResponse);
    rpc GetDescendants(GetDescendantsRequest) returns (stream ElementResponse);
    rpc CaptureTree(CaptureTreeRequest) returns (TreeSnapshotResponse);
    
    // Actions
    rpc Click(ClickRequest) returns (ActionResponse);
    rpc DoubleClick(ElementRequest) returns (ActionResponse);
    rpc RightClick(ElementRequest) returns (ActionResponse);
    rpc TypeText(TypeTextRequest) returns (ActionResponse);
    rpc SetValue(SetValueRequest) returns (ActionResponse);
    rpc Invoke(ElementRequest) returns (ActionResponse);
    rpc SetFocus(ElementRequest) returns (ActionResponse);
    rpc Scroll(ScrollRequest) returns (ActionResponse);
    
    // Patterns
    rpc GetPatterns(ElementRequest) returns (PatternsResponse);
    rpc GetValue(ElementRequest) returns (ValueResponse);
    rpc GetToggleState(ElementRequest) returns (ToggleStateResponse);
    rpc Toggle(ElementRequest) returns (ActionResponse);
    rpc Expand(ElementRequest) returns (ActionResponse);
    rpc Collapse(ElementRequest) returns (ActionResponse);
    rpc Select(ElementRequest) returns (ActionResponse);
    
    // Window management
    rpc SetForeground(ElementRequest) returns (ActionResponse);
    rpc Minimize(ElementRequest) returns (ActionResponse);
    rpc Maximize(ElementRequest) returns (ActionResponse);
    rpc Restore(ElementRequest) returns (ActionResponse);
    rpc Close(ElementRequest) returns (ActionResponse);
    rpc MoveWindow(MoveWindowRequest) returns (ActionResponse);
    rpc ResizeWindow(ResizeWindowRequest) returns (ActionResponse);
    
    // Process management
    rpc AttachToProcess(AttachProcessRequest) returns (ElementResponse);
    rpc LaunchAndAttach(LaunchRequest) returns (ElementResponse);
}

message Empty {}

message ElementRequest {
    string runtime_id = 1;
}

message FindWindowRequest {
    string title = 1;
    bool exact_match = 2;
}

message FindElementRequest {
    string runtime_id = 1;  // Optional: search from this element
    SearchCriteria criteria = 2;
    int32 timeout_ms = 3;
}

message SearchCriteria {
    string automation_id = 1;
    string name = 2;
    string name_contains = 3;
    string class_name = 4;
    string control_type = 5;
    bool enabled_only = 6;
    bool visible_only = 7;
}

message WaitForElementRequest {
    string runtime_id = 1;
    SearchCriteria criteria = 2;
    int32 timeout_ms = 3;
    int32 polling_interval_ms = 4;
}

message GetDescendantsRequest {
    string runtime_id = 1;
    int32 max_depth = 2;
}

message CaptureTreeRequest {
    string runtime_id = 1;
    int32 max_depth = 2;
    bool include_offscreen = 3;
}

message ClickRequest {
    string runtime_id = 1;
    ClickType click_type = 2;
    Point offset = 3;
}

enum ClickType {
    LEFT = 0;
    RIGHT = 1;
    MIDDLE = 2;
    DOUBLE = 3;
}

message TypeTextRequest {
    string runtime_id = 1;
    string text = 2;
    bool clear_first = 3;
    int32 delay_between_keys_ms = 4;
}

message SetValueRequest {
    string runtime_id = 1;
    string value = 2;
}

message ScrollRequest {
    string runtime_id = 1;
    ScrollDirection direction = 2;
    int32 amount = 3;
}

enum ScrollDirection {
    UP = 0;
    DOWN = 1;
    LEFT = 2;
    RIGHT = 3;
}

message MoveWindowRequest {
    string runtime_id = 1;
    int32 x = 2;
    int32 y = 3;
}

message ResizeWindowRequest {
    string runtime_id = 1;
    int32 width = 2;
    int32 height = 3;
}

message AttachProcessRequest {
    int32 process_id = 1;
    string process_name = 2;
}

message LaunchRequest {
    string executable_path = 1;
    string arguments = 2;
    string working_directory = 3;
    int32 timeout_ms = 4;
}

// Responses
message ElementResponse {
    cascade.Result result = 1;
    Element element = 2;
}

message ElementListResponse {
    cascade.Result result = 1;
    repeated Element elements = 2;
}

message Element {
    string runtime_id = 1;
    string automation_id = 2;
    string name = 3;
    string class_name = 4;
    string control_type = 5;
    cascade.Rectangle bounding_rectangle = 6;
    bool is_enabled = 7;
    bool is_offscreen = 8;
    bool has_keyboard_focus = 9;
    repeated string supported_patterns = 10;
    int32 process_id = 11;
}

message TreeSnapshotResponse {
    cascade.Result result = 1;
    TreeNode root = 2;
    int32 total_elements = 3;
    cascade.Timestamp captured_at = 4;
}

message TreeNode {
    Element element = 1;
    repeated TreeNode children = 2;
}

message ActionResponse {
    cascade.Result result = 1;
    int32 execution_time_ms = 2;
}

message PatternsResponse {
    cascade.Result result = 1;
    repeated string patterns = 2;
}

message ValueResponse {
    cascade.Result result = 1;
    string value = 2;
    bool is_readonly = 3;
}

message ToggleStateResponse {
    cascade.Result result = 1;
    ToggleState state = 2;
}

enum ToggleState {
    OFF = 0;
    ON = 1;
    INDETERMINATE = 2;
}
```

### vision.proto

> All capture/OCR requests embed `SessionContext session = 100;` so the service can attach to the correct hidden desktop.

```protobuf
syntax = "proto3";

package cascade.vision;

import "cascade.proto";

option csharp_namespace = "Cascade.Grpc.Vision";

service VisionService {
    // Screenshot capture
    rpc CaptureScreen(CaptureScreenRequest) returns (CaptureResponse);
    rpc CaptureWindow(CaptureWindowRequest) returns (CaptureResponse);
    rpc CaptureElement(CaptureElementRequest) returns (CaptureResponse);
    rpc CaptureRegion(CaptureRegionRequest) returns (CaptureResponse);
    
    // OCR
    rpc RecognizeText(RecognizeRequest) returns (OcrResponse);
    rpc FindText(FindTextRequest) returns (FindTextResponse);
    
    // Change detection
    rpc SetBaseline(SetBaselineRequest) returns (cascade.Result);
    rpc CompareWithBaseline(CompareRequest) returns (ChangeResponse);
    rpc CompareImages(CompareImagesRequest) returns (ChangeResponse);
    rpc WaitForChange(WaitForChangeRequest) returns (ChangeResponse);
    
    // Analysis
    rpc AnalyzeLayout(AnalyzeLayoutRequest) returns (LayoutResponse);
    rpc DetectElements(DetectElementsRequest) returns (VisualElementsResponse);
}

message CaptureScreenRequest {
    int32 screen_index = 1;
    CaptureOptions options = 2;
}

message CaptureWindowRequest {
    string window_handle = 1;
    string window_title = 2;
    CaptureOptions options = 3;
}

message CaptureElementRequest {
    string runtime_id = 1;
    CaptureOptions options = 2;
}

message CaptureRegionRequest {
    cascade.Rectangle region = 1;
    CaptureOptions options = 2;
}

message CaptureOptions {
    string format = 1;  // png, jpeg, bmp
    int32 jpeg_quality = 2;
    bool include_cursor = 3;
    double scale = 4;
}

message CaptureResponse {
    cascade.Result result = 1;
    bytes image_data = 2;
    int32 width = 3;
    int32 height = 4;
    string format = 5;
    cascade.Rectangle captured_region = 6;
}

message RecognizeRequest {
    bytes image_data = 1;
    OcrOptions options = 2;
}

message OcrOptions {
    string language = 1;
    bool preprocess = 2;
    double min_confidence = 3;
}

message OcrResponse {
    cascade.Result result = 1;
    string full_text = 2;
    double confidence = 3;
    repeated OcrLine lines = 4;
    int32 processing_time_ms = 5;
}

message OcrLine {
    string text = 1;
    cascade.Rectangle bounding_box = 2;
    double confidence = 3;
    repeated OcrWord words = 4;
}

message OcrWord {
    string text = 1;
    cascade.Rectangle bounding_box = 2;
    double confidence = 3;
}

message FindTextRequest {
    bytes image_data = 1;
    string search_text = 2;
    bool case_sensitive = 3;
}

message FindTextResponse {
    cascade.Result result = 1;
    bool found = 2;
    repeated TextMatch matches = 3;
}

message TextMatch {
    string text = 1;
    cascade.Rectangle bounding_box = 2;
    double confidence = 3;
}

message SetBaselineRequest {
    bytes image_data = 1;
    string baseline_id = 2;
}

message CompareRequest {
    bytes image_data = 1;
    string baseline_id = 2;
    CompareOptions options = 3;
}

message CompareImagesRequest {
    bytes baseline_image = 1;
    bytes current_image = 2;
    CompareOptions options = 3;
}

message CompareOptions {
    double change_threshold = 1;
    bool ignore_antialiasing = 2;
    int32 color_tolerance = 3;
    repeated cascade.Rectangle ignore_regions = 4;
}

message WaitForChangeRequest {
    cascade.Rectangle region = 1;
    int32 timeout_ms = 2;
    int32 polling_interval_ms = 3;
    double change_threshold = 4;
}

message ChangeResponse {
    cascade.Result result = 1;
    bool has_changes = 2;
    double difference_percentage = 3;
    repeated cascade.Rectangle changed_regions = 4;
    bytes difference_image = 5;
}

message AnalyzeLayoutRequest {
    bytes image_data = 1;
}

message LayoutResponse {
    cascade.Result result = 1;
    string layout_type = 2;
    cascade.Rectangle content_area = 3;
    repeated LayoutRegion regions = 4;
}

message LayoutRegion {
    string name = 1;
    string type = 2;
    cascade.Rectangle bounds = 3;
}

message DetectElementsRequest {
    bytes image_data = 1;
    repeated string element_types = 2;
}

message VisualElementsResponse {
    cascade.Result result = 1;
    repeated VisualElement elements = 2;
}

message VisualElement {
    string type = 1;
    cascade.Rectangle bounding_box = 2;
    double confidence = 3;
    string text = 4;
}
```

### codegen.proto

> Script execution requests include both `SessionContext` and `AutomationCallContext` metadata so the backend knows which hidden desktop to target.

```protobuf
syntax = "proto3";

package cascade.codegen;

import "cascade.proto";

option csharp_namespace = "Cascade.Grpc.CodeGen";

service CodeGenService {
    // Code generation
    rpc GenerateAction(GenerateActionRequest) returns (GeneratedCodeResponse);
    rpc GenerateWorkflow(GenerateWorkflowRequest) returns (GeneratedCodeResponse);
    rpc GenerateAgent(GenerateAgentRequest) returns (GeneratedCodeResponse);
    
    // Compilation
    rpc Compile(CompileRequest) returns (CompileResponse);
    rpc CheckSyntax(CheckSyntaxRequest) returns (SyntaxCheckResponse);
    
    // Execution
    rpc Execute(ExecuteRequest) returns (ExecuteResponse);
    rpc ExecuteScript(ExecuteScriptRequest) returns (ExecuteResponse);
    
    // Script management
    rpc SaveScript(SaveScriptRequest) returns (ScriptResponse);
    rpc GetScript(GetScriptRequest) returns (ScriptResponse);
    rpc ListScripts(ListScriptsRequest) returns (ScriptListResponse);
    rpc DeleteScript(DeleteScriptRequest) returns (cascade.Result);
}

message GenerateActionRequest {
    string name = 1;
    string action_type = 2;
    string element_locator = 3;
    map<string, string> parameters = 4;
}

message GenerateWorkflowRequest {
    string name = 1;
    string description = 2;
    repeated WorkflowStep steps = 3;
}

message WorkflowStep {
    int32 order = 1;
    string name = 2;
    string action_type = 3;
    string element_locator = 4;
    map<string, string> parameters = 5;
    int32 delay_after_ms = 6;
}

message GenerateAgentRequest {
    string name = 1;
    string description = 2;
    repeated string capabilities = 3;
    repeated GenerateActionRequest actions = 4;
}

message GeneratedCodeResponse {
    cascade.Result result = 1;
    string source_code = 2;
    string file_name = 3;
    string namespace = 4;
    repeated string required_usings = 5;
    repeated string required_references = 6;
}

message CompileRequest {
    string source_code = 1;
    CompileOptions options = 2;
}

message CompileOptions {
    string assembly_name = 1;
    repeated string references = 2;
    bool optimize = 3;
}

message CompileResponse {
    cascade.Result result = 1;
    bool compilation_success = 2;
    bytes assembly_bytes = 3;
    repeated CompileError errors = 4;
    repeated CompileError warnings = 5;
    int32 compilation_time_ms = 6;
}

message CompileError {
    string code = 1;
    string message = 2;
    int32 line = 3;
    int32 column = 4;
    string severity = 5;
}

message CheckSyntaxRequest {
    string source_code = 1;
}

message SyntaxCheckResponse {
    cascade.Result result = 1;
    bool is_valid = 2;
    repeated CompileError diagnostics = 3;
}

message ExecuteRequest {
    string script_id = 1;
    string type_name = 2;
    string method_name = 3;
    map<string, string> variables = 4;
    int32 timeout_ms = 5;
}

message ExecuteScriptRequest {
    string script_code = 1;
    map<string, string> variables = 2;
    int32 timeout_ms = 3;
}

message ExecuteResponse {
    cascade.Result result = 1;
    bool execution_success = 2;
    string return_value = 3;
    string exception_message = 4;
    string exception_stack_trace = 5;
    int32 execution_time_ms = 6;
    repeated string logs = 7;
}

message SaveScriptRequest {
    string id = 1;
    string name = 2;
    string description = 3;
    string source_code = 4;
    string type = 5;
    map<string, string> metadata = 6;
}

message GetScriptRequest {
    string id = 1;
    string name = 2;
    string version = 3;
}

message ListScriptsRequest {
    string type = 1;
    string name_contains = 2;
    int32 limit = 3;
    int32 offset = 4;
}

message DeleteScriptRequest {
    string id = 1;
}

message ScriptResponse {
    cascade.Result result = 1;
    Script script = 2;
}

message ScriptListResponse {
    cascade.Result result = 1;
    repeated Script scripts = 2;
    int32 total_count = 3;
}

message Script {
    string id = 1;
    string name = 2;
    string description = 3;
    string source_code = 4;
    string type = 5;
    string current_version = 6;
    string created_at = 7;
    string updated_at = 8;
    map<string, string> metadata = 9;
}
```

### agent.proto

> Agent execution and history mutations carry `SessionContext` values so that runs can be audited per hidden desktop.

```protobuf
syntax = "proto3";

package cascade.agent;

import "cascade.proto";

option csharp_namespace = "Cascade.Grpc.Agent";

service AgentService {
    // Agent CRUD
    rpc CreateAgent(CreateAgentRequest) returns (AgentResponse);
    rpc GetAgent(GetAgentRequest) returns (AgentResponse);
    rpc UpdateAgent(UpdateAgentRequest) returns (AgentResponse);
    rpc DeleteAgent(DeleteAgentRequest) returns (cascade.Result);
    rpc ListAgents(ListAgentsRequest) returns (AgentListResponse);
    
    // Agent versions
    rpc CreateVersion(CreateVersionRequest) returns (AgentVersionResponse);
    rpc GetVersions(GetVersionsRequest) returns (AgentVersionListResponse);
    rpc SetActiveVersion(SetActiveVersionRequest) returns (cascade.Result);
    
    // Agent execution
    rpc GetAgentDefinition(GetAgentRequest) returns (AgentDefinitionResponse);
    rpc RecordExecution(RecordExecutionRequest) returns (cascade.Result);
    rpc GetExecutionHistory(GetExecutionHistoryRequest) returns (ExecutionHistoryResponse);
}

message CreateAgentRequest {
    string name = 1;
    string description = 2;
    string target_application = 3;
    repeated string capabilities = 4;
    string instruction_list = 5;
    repeated string script_ids = 6;
    map<string, string> metadata = 7;
}

message GetAgentRequest {
    string id = 1;
    string name = 2;
}

message UpdateAgentRequest {
    string id = 1;
    string name = 2;
    string description = 3;
    repeated string capabilities = 4;
    string instruction_list = 5;
    repeated string script_ids = 6;
    map<string, string> metadata = 7;
}

message DeleteAgentRequest {
    string id = 1;
}

message ListAgentsRequest {
    string target_application = 1;
    string name_contains = 2;
    int32 limit = 3;
    int32 offset = 4;
}

message CreateVersionRequest {
    string agent_id = 1;
    string version_notes = 2;
}

message GetVersionsRequest {
    string agent_id = 1;
}

message SetActiveVersionRequest {
    string agent_id = 1;
    string version = 2;
}

message GetExecutionHistoryRequest {
    string agent_id = 1;
    int32 limit = 2;
    int32 offset = 3;
}

message RecordExecutionRequest {
    string agent_id = 1;
    string task_description = 2;
    bool success = 3;
    string error_message = 4;
    int32 duration_ms = 5;
    repeated ExecutionStep steps = 6;
}

message ExecutionStep {
    int32 order = 1;
    string action = 2;
    bool success = 3;
    string error = 4;
    int32 duration_ms = 5;
}

// Responses
message AgentResponse {
    cascade.Result result = 1;
    Agent agent = 2;
}

message AgentListResponse {
    cascade.Result result = 1;
    repeated Agent agents = 2;
    int32 total_count = 3;
}

message Agent {
    string id = 1;
    string name = 2;
    string description = 3;
    string target_application = 4;
    repeated string capabilities = 5;
    string active_version = 6;
    string created_at = 7;
    string updated_at = 8;
    map<string, string> metadata = 9;
}

message AgentVersionResponse {
    cascade.Result result = 1;
    AgentVersion version = 2;
}

message AgentVersionListResponse {
    cascade.Result result = 1;
    repeated AgentVersion versions = 2;
}

message AgentVersion {
    string version = 1;
    string created_at = 2;
    string notes = 3;
    bool is_active = 4;
}

message AgentDefinitionResponse {
    cascade.Result result = 1;
    string instruction_list = 2;
    repeated Script scripts = 3;
    repeated string capabilities = 4;
}

message Script {
    string id = 1;
    string name = 2;
    string source_code = 3;
}

message ExecutionHistoryResponse {
    cascade.Result result = 1;
    repeated ExecutionRecord records = 2;
    int32 total_count = 3;
}

message ExecutionRecord {
    string id = 1;
    string agent_id = 2;
    string task_description = 3;
    bool success = 4;
    string error_message = 5;
    int32 duration_ms = 6;
    string executed_at = 7;
}
```

## Server Configuration

```csharp
public class GrpcServerOptions
{
    public int Port { get; set; } = 50051;
    public bool EnableReflection { get; set; } = true;
    public bool EnableDetailedErrors { get; set; } = false;
    public int MaxReceiveMessageSize { get; set; } = 16 * 1024 * 1024; // 16MB
    public int MaxSendMessageSize { get; set; } = 16 * 1024 * 1024;
    public bool RequireAuthentication { get; set; } = false;
    public string? CertificatePath { get; set; }
    public string? CertificateKeyPath { get; set; }
}
```

## Error Handling

```csharp
public class ErrorHandlingInterceptor : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            return await continuation(request, context);
        }
        catch (UIAutomationException ex)
        {
            throw new RpcException(new Status(
                StatusCode.FailedPrecondition, 
                ex.Message));
        }
        catch (TimeoutException ex)
        {
            throw new RpcException(new Status(
                StatusCode.DeadlineExceeded, 
                ex.Message));
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(
                StatusCode.Internal, 
                "Internal server error"));
        }
    }
}
```

## Python Client Example

```python
import grpc
from cascade.grpc import ui_automation_pb2 as ua
from cascade.grpc import ui_automation_pb2_grpc as ua_grpc

class UIAutomationClient:
    def __init__(self, host: str = "localhost", port: int = 50051):
        self.channel = grpc.insecure_channel(f"{host}:{port}")
        self.stub = ua_grpc.UIAutomationServiceStub(self.channel)
    
    async def find_element(self, criteria: dict) -> ua.Element:
        request = ua.FindElementRequest(
            criteria=ua.SearchCriteria(**criteria)
        )
        response = await self.stub.FindElement(request)
        if not response.result.success:
            raise Exception(response.result.error_message)
        return response.element
    
    async def click(self, runtime_id: str) -> None:
        request = ua.ClickRequest(runtime_id=runtime_id)
        response = await self.stub.Click(request)
        if not response.result.success:
            raise Exception(response.result.error_message)
```



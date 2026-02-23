/**
 * Type definitions for OpenClaw Plugin
 */

// Configuration Types
export interface CascadePluginConfig {
  cascadeGrpcEndpoint: string;
  cascadePythonPath?: string;
  cascadePythonModulePath?: string;
  firestoreProjectId?: string;
  firestoreCredentialsPath?: string;
  headless?: boolean;
  actionTimeoutMs?: number;
  enableA2A?: boolean;
  allowedAgents?: AgentRole[];
  requireAgentConfirmation?: boolean;
  verbose?: boolean;
  screenshotMode?: 'embed' | 'disk' | 'auto';
  screenshotDir?: string;
}

export type AgentRole = 'explorer' | 'worker' | 'orchestrator';

// Tool Types
export interface ToolSchema {
  name: string;
  description: string;
  inputSchema: Record<string, any>;
}

export interface ToolRegistry {
  register(tool: ToolSchema & { handler: ToolHandler }): void;
  getAll(): Array<ToolSchema & { handler: ToolHandler }>;
  get(name: string): (ToolSchema & { handler: ToolHandler }) | undefined;
}

export type ToolHandler = (args: Record<string, any>) => Promise<ToolResponse>;

export interface ToolResponse {
  content: Array<{ type: string; [key: string]: any }>;
  isError?: boolean;
}

// MCP Types
export interface McpRequest {
  jsonrpc: '2.0';
  id: number;
  method: string;
  params?: Record<string, any>;
}

export interface McpResponse {
  jsonrpc: '2.0';
  id: number;
  result?: any;
  error?: {
    code: number;
    message: string;
  };
}

// A2A Types
export interface A2AMessage {
  type: string;
  source: 'openclaw' | AgentRole;
  timestamp: number;
  payload: any;
  runId?: string;
}

export interface AgentMessage {
  messageId: string;
  userId: string;
  appId: string;
  senderAgentId: string;
  senderRole: string;
  targetAgentId?: string;
  targetRole?: string;
  runId?: string;
  headers: Record<string, string>;
  jsonPayload: string;
  createdAtMs: number;
}

// OpenClaw API Types
export interface OpenClawApi {
  config: {
    plugins: {
      entries: {
        cascade?: {
          config?: CascadePluginConfig;
        };
      };
    };
  };
  registerTool(tool: ToolSchema & { handler: ToolHandler }): void;
  registerGatewayMethod(name: string, handler: (context: any) => void): void;
  registerCli(handler: (context: { program: any }) => void): void;
  notify(message: string): void;
}

// Error Types
export class CascadeError extends Error {
  constructor(
    message: string,
    public code: string,
    public suggestion?: string
  ) {
    super(message);
    this.name = 'CascadeError';
  }
}

export class A2ADisabledError extends CascadeError {
  constructor(message: string) {
    super(message, 'A2A_DISABLED');
    this.name = 'A2ADisabledError';
  }
}

export class AgentNotAllowedError extends CascadeError {
  constructor(message: string) {
    super(message, 'AGENT_NOT_ALLOWED');
    this.name = 'AgentNotAllowedError';
  }
}

export class UserCancelledError extends CascadeError {
  constructor(message: string) {
    super(message, 'USER_CANCELLED');
    this.name = 'UserCancelledError';
  }
}

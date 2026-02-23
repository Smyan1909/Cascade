/**
 * OpenClaw Plugin Entry Point
 * 
 * This is the main entry point for the @cascade/openclaw-plugin
 * It initializes the plugin and registers all tools with OpenClaw
 */

import { CascadeGrpcClient } from './grpc-client';
import { CascadeA2AClient } from './a2a-client';
import { loadConfig } from './config';
import { CascadeError } from './types';
import {
  registerDesktopTools,
  registerA2ATools
} from './tools';
import { ToolRegistry } from './tools/tool-registry';

// Placeholder for OpenClaw API type
interface OpenClawApi {
  config: {
    plugins: {
      entries: {
        cascade?: { config?: any };
        'openclaw-cascade-plugin'?: { config?: any };
        [key: string]: { config?: any } | undefined;
      };
    };
  };
  registerTool: (tool: any) => void;
  registerGatewayMethod: (name: string, handler: Function) => void;
  registerCli: (handler: Function) => void;
  notify: (message: string) => void;
}

export default async function register(api: OpenClawApi) {
  let grpcClient: CascadeGrpcClient | null = null;
  let a2aClient: CascadeA2AClient | null = null;

  try {
    // Load and validate configuration
    const entries = api.config.plugins.entries || {};
    const config = await loadConfig(
      entries['openclaw-cascade-plugin']?.config ||
      entries.cascade?.config ||
      {}
    );
    
    // Lazy loaded clients
    let initialized = false;

    const getGrpcClient = async (): Promise<CascadeGrpcClient> => {
      if (grpcClient && initialized) return grpcClient;

      grpcClient = new CascadeGrpcClient(config.cascadeGrpcEndpoint);
      await grpcClient.start();
      initialized = true;
      return grpcClient;
    };

    const getA2aClient = async (): Promise<CascadeA2AClient | null> => {
      if (!config.enableA2A) return null;
      if (a2aClient) return a2aClient;

      a2aClient = new CascadeA2AClient(
        config.cascadeGrpcEndpoint,
        'openclaw-user',
        config.firestoreProjectId || 'openclaw',
        '' // auth token would come from config
      );
      await a2aClient.initialize();
      return a2aClient;
    };
    
    // Create tool registry
    const toolRegistry = new ToolRegistry();
    
    // Register all tools using the getters
    registerDesktopTools(toolRegistry, getGrpcClient, config);
    registerA2ATools(toolRegistry, getA2aClient);
    
    // Register tools with OpenClaw
    const tools = toolRegistry.getAll();
    
    for (const tool of tools) {
      // Ensure the schema is perfectly formatted for OpenAI and OpenClaw's internal validator
      const schema = tool.inputSchema || { type: 'object', properties: {} };
      
      api.registerTool({
        name: tool.name,
        description: tool.description,
        schema: schema,
        parameters: schema,
        inputSchema: schema,
        // Match the exact signature expected by OpenClaw's pi-agent-core AgentTool
        execute: async (_toolCallId: string, params: any) => tool.handler(params), 
        handler: tool.handler  // Fallback for legacy
      });
    }
    
    // Register status check
    api.registerGatewayMethod('cascade.status', () => ({
      connected: grpcClient?.isConnected() || false,
      toolsRegistered: tools.length,
      grpcEndpoint: config.cascadeGrpcEndpoint,
      a2aEnabled: config.enableA2A
    }));
    
    // Register CLI command
    api.registerCli(({ program }: { program: any }) => {
      program
        .command('cascade:status')
        .description('Check Cascade plugin status')
        .action(async () => {
          let connected = false;
          try {
             // Only check if it's already initialized to avoid triggering init
             if (initialized && grpcClient) {
                connected = grpcClient.isConnected();
             }
          } catch(e) {}

          console.log('Cascade Plugin Status:');
          console.log('  Connected:', connected);
          console.log('  Tools:', tools.length);
          console.log('  gRPC:', config.cascadeGrpcEndpoint);
          console.log('  A2A:', config.enableA2A ? 'enabled' : 'disabled');
        });
      
      program
        .command('cascade:tools')
        .description('List all registered tools')
        .action(() => {
          console.log('Registered Tools:');
          tools.forEach((tool, index) => {
            console.log(`  ${index + 1}. ${tool.name}`);
          });
        });
    });
    
  } catch (error) {
    console.error('Failed to initialize Cascade plugin:', error);
    
    if (error instanceof CascadeError) {
      throw error;
    }
    
    throw new Error(
      `Cascade plugin initialization failed: ${error instanceof Error ? error.message : 'Unknown error'}`
    );
  }
}

// Export types for TypeScript users
export * from './types';
export { CascadeGrpcClient, CascadeA2AClient, loadConfig };

/**
 * A2A Client - Agent-to-Agent communication
 */

import { A2AMessage } from './types';

export class CascadeA2AClient {
  private agentId: string | null = null;
  private messageHandlers = new Map<string, (payload: any) => Promise<void>>();
  private grpcEndpoint: string;
  private userId: string;
  private appId: string;
  private authToken: string;

  constructor(
    grpcEndpoint: string,
    userId: string,
    appId: string,
    authToken: string
  ) {
    this.grpcEndpoint = grpcEndpoint;
    this.userId = userId;
    this.appId = appId;
    this.authToken = authToken;
  }

  async initialize(): Promise<void> {
    // Initialize gRPC connection and register agent
    // This is a placeholder - actual implementation would use gRPC
    this.agentId = `openclaw-${this.userId}-${Date.now()}`;
    console.debug(`A2A Client connecting to ${this.grpcEndpoint} for app ${this.appId}`);
  }

  async sendToAgent(
    targetRole: 'explorer' | 'worker' | 'orchestrator',
    payload: any,
    targetAgentId?: string
  ): Promise<void> {
    if (!this.agentId) {
      throw new Error('A2A client not initialized');
    }

    const message: A2AMessage = {
      type: payload.type,
      source: 'openclaw',
      timestamp: Date.now(),
      payload,
      runId: payload.runId
    };

    // Send via gRPC (placeholder)
    const target = targetAgentId || targetRole;
    console.log(`Sending message to ${target}:`, message);
    
    // In real implementation, would use this.grpcEndpoint and this.authToken
    console.debug(`Using endpoint: ${this.grpcEndpoint}, token: ${this.authToken ? '***' : 'none'}`);
  }

  onMessage(type: string, handler: (payload: any) => Promise<void>): void {
    this.messageHandlers.set(type, handler);
  }

  isConnected(): boolean {
    return this.agentId !== null;
  }
}

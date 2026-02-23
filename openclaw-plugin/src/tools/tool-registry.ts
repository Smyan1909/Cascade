/**
 * Tool Registry for managing OpenClaw tools
 */

import { ToolSchema, ToolHandler, ToolResponse } from '../types';

export class ToolRegistry {
  private tools = new Map<string, ToolSchema & { handler: ToolHandler }>();

  /**
   * Register a tool with the registry
   */
  register(tool: ToolSchema & { handler: ToolHandler }): void {
    if (this.tools.has(tool.name)) {
      throw new Error(`Tool ${tool.name} is already registered`);
    }
    this.tools.set(tool.name, tool);
  }

  /**
   * Get all registered tools
   */
  getAll(): Array<ToolSchema & { handler: ToolHandler }> {
    return Array.from(this.tools.values());
  }

  /**
   * Get a specific tool by name
   */
  get(name: string): (ToolSchema & { handler: ToolHandler }) | undefined {
    return this.tools.get(name);
  }

  /**
   * Check if a tool exists
   */
  has(name: string): boolean {
    return this.tools.has(name);
  }

  /**
   * Call a tool by name with arguments
   */
  async call(name: string, args: Record<string, any>): Promise<ToolResponse> {
    const tool = this.get(name);
    if (!tool) {
      throw new Error(`Tool ${name} not found`);
    }
    return tool.handler(args);
  }
}

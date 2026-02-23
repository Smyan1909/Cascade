/**
 * A2A Tools (Agent-to-Agent)
 * 
 * 3 tools for calling Cascade agents
 */

import { ToolRegistry } from './tool-registry';
import { CascadeA2AClient } from '../a2a-client';
import { ToolResponse } from '../types';
import { errorResponse, formatSuccess } from './response-helpers';

export function registerA2ATools(registry: ToolRegistry, getA2aClient: () => Promise<CascadeA2AClient | null>): void {
  // 1. cascade_run_explorer
  registry.register({
    name: 'cascade_run_explorer',
    description: 'Launch Cascade Explorer agent to learn an application',
    inputSchema: {
      type: 'object',
      properties: {
        app_name: {
          type: 'string',
          description: 'Application name to explore'
        },
        instructions: {
          type: 'string',
          description: 'Specific instructions for exploration'
        },
        max_steps: {
          type: 'integer',
          default: 50,
          description: 'Maximum exploration steps'
        }
      },
      required: ['app_name']
    },
    handler: async (args): Promise<ToolResponse> => {
      try {
        if (!args.app_name) {
          return errorResponse('app_name is required');
        }

        await (await getA2aClient())?.sendToAgent('explorer', {
          type: 'start_exploration',
          appName: args.app_name,
          instructions: args.instructions || `Learn how to use ${args.app_name}`,
          maxSteps: args.max_steps || 50
        });

        return formatSuccess({
          message: 'Explorer agent started',
          app_name: args.app_name,
          status: 'running'
        });
      } catch (error) {
        return errorResponse(
          error instanceof Error ? error.message : 'Failed to start explorer',
          'Ensure A2A is enabled in configuration (enableA2A: true)'
        );
      }
    }
  });

  // 2. cascade_run_worker
  registry.register({
    name: 'cascade_run_worker',
    description: 'Execute a task using Cascade Worker agent',
    inputSchema: {
      type: 'object',
      properties: {
        task: {
          type: 'string',
          description: 'Task description to execute'
        },
        skill_id: {
          type: 'string',
          description: 'Optional skill ID to use'
        },
        app_name: {
          type: 'string',
          description: 'Target application name'
        },
        inputs: {
          type: 'object',
          description: 'Optional inputs for the task',
          additionalProperties: true
        }
      },
      required: ['task']
    },
    handler: async (args): Promise<ToolResponse> => {
      try {
        if (!args.task) {
          return errorResponse('task is required');
        }

        await (await getA2aClient())?.sendToAgent('worker', {
          type: 'execute_task',
          task: args.task,
          skillId: args.skill_id,
          appName: args.app_name,
          inputs: args.inputs || {}
        });

        return formatSuccess({
          message: 'Worker agent started',
          task: args.task,
          status: 'running'
        });
      } catch (error) {
        return errorResponse(
          error instanceof Error ? error.message : 'Failed to start worker',
          'Ensure A2A is enabled in configuration (enableA2A: true)'
        );
      }
    }
  });

  // 3. cascade_run_orchestrator
  registry.register({
    name: 'cascade_run_orchestrator',
    description: 'Use Cascade Orchestrator to coordinate multi-step tasks',
    inputSchema: {
      type: 'object',
      properties: {
        goal: {
          type: 'string',
          description: 'High-level goal to achieve'
        },
        require_approval: {
          type: 'boolean',
          default: true,
          description: 'Require approval before executing steps'
        }
      },
      required: ['goal']
    },
    handler: async (args): Promise<ToolResponse> => {
      try {
        if (!args.goal) {
          return errorResponse('goal is required');
        }

        await (await getA2aClient())?.sendToAgent('orchestrator', {
          type: 'coordinate',
          goal: args.goal,
          requireApproval: args.require_approval !== false
        });

        return formatSuccess({
          message: 'Orchestrator started',
          goal: args.goal,
          status: 'running'
        });
      } catch (error) {
        return errorResponse(
          error instanceof Error ? error.message : 'Failed to start orchestrator',
          'Ensure A2A is enabled in configuration (enableA2A: true)'
        );
      }
    }
  });
}

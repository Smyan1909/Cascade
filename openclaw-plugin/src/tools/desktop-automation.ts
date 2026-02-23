import { ToolRegistry } from './tool-registry';
import { CascadeGrpcClient } from '../grpc-client';
import { ToolResponse, CascadePluginConfig } from '../types';
import { errorResponse, formatSuccess, imageResponse } from './response-helpers';
import { writeFile, mkdir } from 'fs/promises';
import { join } from 'path';
import { homedir } from 'os';

const MAX_EMBED_SIZE = 4 * 1024 * 1024;

export function registerDesktopTools(
  registry: ToolRegistry,
  getClient: () => Promise<CascadeGrpcClient>,
  config?: CascadePluginConfig
): void {
  const screenshotMode = config?.screenshotMode || 'auto';
  const screenshotDir = config?.screenshotDir || join(homedir(), '.openclaw', 'screenshots');

  // Helper to extract selector
  const extractSelector = (args: any) => {
    return {
      platform_source: args.platform_source,
      id: args.id,
      name: args.name,
      control_type: args.control_type,
      path: args.path,
      index: args.index,
      text_hint: args.text_hint,
      element_id: args.element_id,
      automation_id: args.automation_id,
      class_name: args.class_name,
      framework_id: args.framework_id,
      help_text: args.help_text
    };
  };

  const selectorProperties = {
    platform_source: { type: 'string', enum: ['WINDOWS', 'JAVA', 'WEB'], description: 'Platform type' },
    id: { type: 'string', description: 'Legacy automation ID' },
    name: { type: 'string' },
    control_type: { type: 'string' },
    path: { type: 'array', items: { type: 'string' } },
    index: { type: 'integer' },
    text_hint: { type: 'string' },
    element_id: { type: 'string', description: 'Runtime element ID' },
    automation_id: { type: 'string', description: 'Automation ID' },
    class_name: { type: 'string' },
    framework_id: { type: 'string' },
    help_text: { type: 'string' }
  };

  const selectorSchema = {
    type: 'object',
    properties: selectorProperties,
    required: ['platform_source']
  };

  registry.register({
    name: 'cascade_click_element',
    description: 'Click on a UI element on the desktop or in an application',
    inputSchema: {
      type: 'object',
      properties: selectorProperties,
      required: ['platform_source']
    },
    handler: async (args): Promise<ToolResponse> => {
      try {
        if (!args.platform_source) return errorResponse('platform_source is required');
        const result = await (await getClient()).performAction('CLICK', extractSelector(args));
        return formatSuccess(result);
      } catch (error) {
        return errorResponse(error instanceof Error ? error.message : 'Failed to click element');
      }
    }
  });

  registry.register({
    name: 'cascade_type_text',
    description: 'Type text into a UI element',
    inputSchema: {
      type: 'object',
      properties: {
        selector: selectorSchema,
        text: { type: 'string', description: 'Text to type' },
        text_entry_mode: { type: 'string', enum: ['APPEND', 'REPLACE'] }
      },
      required: ['selector', 'text']
    },
    handler: async (args): Promise<ToolResponse> => {
      try {
        if (!args.selector) return errorResponse('selector is required');
        if (!args.text) return errorResponse('text is required');
        const result = await (await getClient()).performAction('TYPE_TEXT', args.selector, {
          text: args.text,
          text_entry_mode: args.text_entry_mode || 'APPEND'
        });
        return formatSuccess(result);
      } catch (error) {
        return errorResponse(error instanceof Error ? error.message : 'Failed to type text');
      }
    }
  });

  registry.register({
    name: 'cascade_get_semantic_tree',
    description: 'Get the UI element structure',
    inputSchema: {
      type: 'object',
      properties: { _placeholder: { type: "boolean" } }
    },
    handler: async (): Promise<ToolResponse> => {
      try {
        const result = await (await getClient()).getSemanticTree();
        return formatSuccess(result);
      } catch (error) {
        return errorResponse(error instanceof Error ? error.message : 'Failed to get tree');
      }
    }
  });

  registry.register({
    name: 'cascade_get_screenshot',
    description: 'Capture a screenshot with element annotations',
    inputSchema: {
      type: 'object',
      properties: { _placeholder: { type: "boolean" } }
    },
    handler: async (): Promise<ToolResponse> => {
      try {
        const result = await (await getClient()).getScreenshot();
        if (!result.image) return errorResponse('No image data received');

        const imageSize = Buffer.from(result.image, 'base64').length;
        const shouldEmbed = screenshotMode === 'embed' || (screenshotMode === 'auto' && imageSize < MAX_EMBED_SIZE);

        if (shouldEmbed) {
          return imageResponse(result.image, 'jpeg', `Captured screenshot. Marks available:\n${JSON.stringify(result.marks, null, 2)}`);
        }

        await mkdir(screenshotDir, { recursive: true });
        const filename = `screenshot_${Date.now()}.jpg`;
        const filepath = join(screenshotDir, filename);
        await writeFile(filepath, Buffer.from(result.image, 'base64'));

        return formatSuccess({
          message: `Screenshot saved to ${filepath}`,
          marks: result.marks,
          file_path: filepath
        });
      } catch (error) {
        return errorResponse(error instanceof Error ? error.message : 'Failed to get screenshot');
      }
    }
  });

  registry.register({
    name: 'cascade_start_app',
    description: 'Start an application by name',
    inputSchema: {
      type: 'object',
      properties: {
        app_name: { type: 'string' }
      },
      required: ['app_name']
    },
    handler: async (args): Promise<ToolResponse> => {
      try {
        if (!args.app_name) return errorResponse('app_name is required');
        const result = await (await getClient()).startApp(args.app_name);
        return formatSuccess(result);
      } catch (error) {
        return errorResponse(error instanceof Error ? error.message : 'Failed to start app');
      }
    }
  });

  registry.register({
    name: 'cascade_hover_element',
    description: 'Hover over a UI element',
    inputSchema: {
      type: 'object',
      properties: {
        selector: selectorSchema
      },
      required: ['selector']
    },
    handler: async (args): Promise<ToolResponse> => {
      try {
        const result = await (await getClient()).performAction('HOVER', args.selector);
        return formatSuccess(result);
      } catch (error) {
        return errorResponse(error instanceof Error ? error.message : 'Failed to hover');
      }
    }
  });

  registry.register({
    name: 'cascade_focus_element',
    description: 'Focus on a UI element',
    inputSchema: {
      type: 'object',
      properties: {
        selector: selectorSchema
      },
      required: ['selector']
    },
    handler: async (args): Promise<ToolResponse> => {
      try {
        const result = await (await getClient()).performAction('FOCUS', args.selector);
        return formatSuccess(result);
      } catch (error) {
        return errorResponse(error instanceof Error ? error.message : 'Failed to focus');
      }
    }
  });

  registry.register({
    name: 'cascade_scroll_element',
    description: 'Scroll a UI element',
    inputSchema: {
      type: 'object',
      properties: {
        selector: selectorSchema,
        amount: { type: 'number', description: 'Scroll delta (positive=down, negative=up)' }
      },
      required: ['selector']
    },
    handler: async (args): Promise<ToolResponse> => {
      try {
        const result = await (await getClient()).performAction('SCROLL', args.selector, {
          number: args.amount
        });
        return formatSuccess(result);
      } catch (error) {
        return errorResponse(error instanceof Error ? error.message : 'Failed to scroll');
      }
    }
  });

  registry.register({
    name: 'cascade_wait_visible',
    description: 'Wait for a UI element to become visible',
    inputSchema: {
      type: 'object',
      properties: {
        selector: selectorSchema
      },
      required: ['selector']
    },
    handler: async (args): Promise<ToolResponse> => {
      try {
        const result = await (await getClient()).performAction('WAIT_VISIBLE', args.selector);
        return formatSuccess(result);
      } catch (error) {
        return errorResponse(error instanceof Error ? error.message : 'Failed to wait');
      }
    }
  });

  registry.register({
    name: 'cascade_toggle_element',
    description: 'Toggle a UI element (checkbox, switch, etc.)',
    inputSchema: {
      type: 'object',
      properties: {
        selector: selectorSchema
      },
      required: ['selector']
    },
    handler: async (args): Promise<ToolResponse> => {
      try {
        const result = await (await getClient()).performAction('TOGGLE', args.selector);
        return formatSuccess(result);
      } catch (error) {
        return errorResponse(error instanceof Error ? error.message : 'Failed to toggle');
      }
    }
  });

  registry.register({
    name: 'cascade_expand_element',
    description: 'Expand a UI element (tree item, accordion, etc.)',
    inputSchema: {
      type: 'object',
      properties: {
        selector: selectorSchema
      },
      required: ['selector']
    },
    handler: async (args): Promise<ToolResponse> => {
      try {
        const result = await (await getClient()).performAction('EXPAND', args.selector);
        return formatSuccess(result);
      } catch (error) {
        return errorResponse(error instanceof Error ? error.message : 'Failed to expand');
      }
    }
  });

  registry.register({
    name: 'cascade_collapse_element',
    description: 'Collapse a UI element (tree item, accordion, etc.)',
    inputSchema: {
      type: 'object',
      properties: {
        selector: selectorSchema
      },
      required: ['selector']
    },
    handler: async (args): Promise<ToolResponse> => {
      try {
        const result = await (await getClient()).performAction('COLLAPSE', args.selector);
        return formatSuccess(result);
      } catch (error) {
        return errorResponse(error instanceof Error ? error.message : 'Failed to collapse');
      }
    }
  });

  registry.register({
    name: 'cascade_select_element',
    description: 'Select a UI element (list item, tab, etc.)',
    inputSchema: {
      type: 'object',
      properties: {
        selector: selectorSchema
      },
      required: ['selector']
    },
    handler: async (args): Promise<ToolResponse> => {
      try {
        const result = await (await getClient()).performAction('SELECT', args.selector);
        return formatSuccess(result);
      } catch (error) {
        return errorResponse(error instanceof Error ? error.message : 'Failed to select');
      }
    }
  });

  registry.register({
    name: 'cascade_set_range_value',
    description: 'Set a range value (slider, spinner, etc.)',
    inputSchema: {
      type: 'object',
      properties: {
        selector: selectorSchema,
        value: { type: 'number', description: 'Target numeric value' }
      },
      required: ['selector', 'value']
    },
    handler: async (args): Promise<ToolResponse> => {
      try {
        const result = await (await getClient()).performAction('SET_RANGE_VALUE', args.selector, {
          number: args.value
        });
        return formatSuccess(result);
      } catch (error) {
        return errorResponse(error instanceof Error ? error.message : 'Failed to set range value');
      }
    }
  });

  registry.register({
    name: 'cascade_send_keys',
    description: 'Send a key chord or sequence to a focused element',
    inputSchema: {
      type: 'object',
      properties: {
        selector: selectorSchema,
        keys: { type: 'string', description: 'Keys like ENTER, CTRL+S, ALT+F4' }
      },
      required: ['selector', 'keys']
    },
    handler: async (args): Promise<ToolResponse> => {
      try {
        const result = await (await getClient()).performAction('SEND_KEYS', args.selector, {
          text: args.keys
        });
        return formatSuccess(result);
      } catch (error) {
        return errorResponse(error instanceof Error ? error.message : 'Failed to send keys');
      }
    }
  });

  registry.register({
    name: 'cascade_window_minimize',
    description: 'Minimize a window element',
    inputSchema: {
      type: 'object',
      properties: {
        selector: selectorSchema
      },
      required: ['selector']
    },
    handler: async (args): Promise<ToolResponse> => {
      try {
        const result = await (await getClient()).performAction('WINDOW_MINIMIZE', args.selector);
        return formatSuccess(result);
      } catch (error) {
        return errorResponse(error instanceof Error ? error.message : 'Failed to minimize window');
      }
    }
  });

  registry.register({
    name: 'cascade_window_maximize',
    description: 'Maximize a window element',
    inputSchema: {
      type: 'object',
      properties: {
        selector: selectorSchema
      },
      required: ['selector']
    },
    handler: async (args): Promise<ToolResponse> => {
      try {
        const result = await (await getClient()).performAction('WINDOW_MAXIMIZE', args.selector);
        return formatSuccess(result);
      } catch (error) {
        return errorResponse(error instanceof Error ? error.message : 'Failed to maximize window');
      }
    }
  });

  registry.register({
    name: 'cascade_window_restore',
    description: 'Restore a window element',
    inputSchema: {
      type: 'object',
      properties: {
        selector: selectorSchema
      },
      required: ['selector']
    },
    handler: async (args): Promise<ToolResponse> => {
      try {
        const result = await (await getClient()).performAction('WINDOW_RESTORE', args.selector);
        return formatSuccess(result);
      } catch (error) {
        return errorResponse(error instanceof Error ? error.message : 'Failed to restore window');
      }
    }
  });

  registry.register({
    name: 'cascade_window_close',
    description: 'Close a window element',
    inputSchema: {
      type: 'object',
      properties: {
        selector: selectorSchema
      },
      required: ['selector']
    },
    handler: async (args): Promise<ToolResponse> => {
      try {
        const result = await (await getClient()).performAction('WINDOW_CLOSE', args.selector);
        return formatSuccess(result);
      } catch (error) {
        return errorResponse(error instanceof Error ? error.message : 'Failed to close window');
      }
    }
  });

  registry.register({
    name: 'cascade_move_window',
    description: 'Move a window element by coordinates',
    inputSchema: {
      type: 'object',
      properties: {
        selector: selectorSchema,
        x: { type: 'number', description: 'Target X coordinate' },
        y: { type: 'number', description: 'Target Y coordinate' }
      },
      required: ['selector', 'x', 'y']
    },
    handler: async (args): Promise<ToolResponse> => {
      try {
        const result = await (await getClient()).performAction('MOVE', args.selector, {
          json_payload: JSON.stringify({ x: args.x, y: args.y })
        });
        return formatSuccess(result);
      } catch (error) {
        return errorResponse(error instanceof Error ? error.message : 'Failed to move window');
      }
    }
  });

  registry.register({
    name: 'cascade_resize_window',
    description: 'Resize a window element by width/height',
    inputSchema: {
      type: 'object',
      properties: {
        selector: selectorSchema,
        width: { type: 'number', description: 'Target width' },
        height: { type: 'number', description: 'Target height' }
      },
      required: ['selector', 'width', 'height']
    },
    handler: async (args): Promise<ToolResponse> => {
      try {
        const result = await (await getClient()).performAction('RESIZE', args.selector, {
          json_payload: JSON.stringify({ width: args.width, height: args.height })
        });
        return formatSuccess(result);
      } catch (error) {
        return errorResponse(error instanceof Error ? error.message : 'Failed to resize window');
      }
    }
  });
}

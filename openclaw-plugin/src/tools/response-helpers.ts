/**
 * Base tool response helpers
 */

import { ToolResponse } from '../types';

/**
 * Create a successful text response
 */
export function successResponse(text: string): ToolResponse {
  return {
    content: [{ type: 'text', text }]
  };
}

/**
 * Create a successful JSON response
 */
export function jsonResponse(data: any): ToolResponse {
  return {
    content: [{ type: 'text', text: JSON.stringify(data, null, 2) }]
  };
}

/**
 * Create an image response with optional text
 */
export function imageResponse(
  base64Data: string,
  format: 'png' | 'jpeg' = 'png',
  text?: string
): ToolResponse {
  const content: any[] = [
    {
      // Anthropic / Standard MCP Tool Response format
      type: 'image',
      source: {
        type: 'base64',
        media_type: `image/${format}`,
        data: base64Data
      },
      // In case OpenClaw is translating to OpenAI and looking for top-level fields:
      mimeType: `image/${format}`,
      data: base64Data,
      // In case OpenClaw allows direct OpenAI passthrough:
      image_url: {
        url: `data:image/${format};base64,${base64Data}`
      }
    }
  ];

  if (text) {
    content.push({ type: 'text', text });
  }

  return { content };
}

/**
 * Create an error response
 */
export function errorResponse(message: string, suggestion?: string): ToolResponse {
  const response: any = {
    success: false,
    error: message
  };

  if (suggestion) {
    response.suggestion = suggestion;
  }

  return {
    content: [{ type: 'text', text: JSON.stringify(response, null, 2) }],
    isError: true
  };
}

/**
 * Format a successful tool result
 */
export function formatSuccess(result: any): ToolResponse {
  return jsonResponse({
    success: true,
    ...result
  });
}

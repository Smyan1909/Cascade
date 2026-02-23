/**
 * Configuration management for OpenClaw Cascade Plugin
 */

import { CascadePluginConfig } from './types';
import { isWsl, getWslHostIp } from './wsl';

const VALID_AGENTS = ['explorer', 'worker', 'orchestrator'];
const VALID_SCREENSHOT_MODES = ['embed', 'disk', 'auto'];

/**
 * Get default configuration values
 */
export async function getDefaults(): Promise<Partial<CascadePluginConfig>> {
  let defaultEndpoint = 'localhost:50051';
  
  if (isWsl()) {
    const wslIp = await getWslHostIp();
    if (wslIp) {
      defaultEndpoint = `${wslIp}:50051`;
    }
  }

  return {
    cascadeGrpcEndpoint: defaultEndpoint,
    headless: false,
    actionTimeoutMs: 8000,
    enableA2A: true,
    verbose: false,
    screenshotMode: 'auto'
  };
}

/**
 * Validate configuration object
 */
export function validateConfig(config: CascadePluginConfig): void {
  // Required fields
  if (!config.cascadeGrpcEndpoint || config.cascadeGrpcEndpoint.trim() === '') {
    throw new Error('cascadeGrpcEndpoint is required');
  }

  // Validate gRPC endpoint format
  const endpointPattern = /^[\w.-]+:\d+$/;
  if (!endpointPattern.test(config.cascadeGrpcEndpoint)) {
    throw new Error(
      `Invalid cascadeGrpcEndpoint format: ${config.cascadeGrpcEndpoint}. ` +
      'Expected format: host:port (e.g., localhost:50051)'
    );
  }

  // Validate allowed agents if provided
  if (config.allowedAgents) {
    for (const agent of config.allowedAgents) {
      if (!VALID_AGENTS.includes(agent)) {
        throw new Error(`Invalid agent in allowedAgents: ${agent}. Valid agents: ${VALID_AGENTS.join(', ')}`);
      }
    }
  }

  // Validate screenshot mode if provided
  if (config.screenshotMode && !VALID_SCREENSHOT_MODES.includes(config.screenshotMode)) {
    throw new Error(
      `Invalid screenshotMode: ${config.screenshotMode}. ` +
      `Valid modes: ${VALID_SCREENSHOT_MODES.join(', ')}`
    );
  }

  // Validate action timeout
  if (config.actionTimeoutMs !== undefined && config.actionTimeoutMs < 1000) {
    throw new Error('actionTimeoutMs must be at least 1000ms');
  }
}

/**
 * Expand environment variables in a string
 * Supports both $VAR and %VAR% syntax
 */
export function expandEnvVars(value: string): string {
  if (!value) return value;

  // Unix-style: $VAR or ${VAR}
  let expanded = value.replace(/\$\{(\w+)\}/g, (match, varName) => {
    return process.env[varName] || match;
  });

  expanded = expanded.replace(/\$(\w+)/g, (match, varName) => {
    return process.env[varName] || match;
  });

  // Windows-style: %VAR%
  expanded = expanded.replace(/%(\w+)%/g, (match, varName) => {
    return process.env[varName] || match;
  });

  return expanded;
}

/**
 * Load and validate configuration
 */
export async function loadConfig(input?: Partial<CascadePluginConfig>): Promise<CascadePluginConfig> {
  const actualInput = input || {};

  // Expand environment variables in string fields
  const expanded: Partial<CascadePluginConfig> = {};
  
  for (const [key, value] of Object.entries(actualInput)) {
    if (typeof value === 'string') {
      (expanded as any)[key] = expandEnvVars(value);
    } else {
      (expanded as any)[key] = value;
    }
  }

  // Merge with defaults
  const defaults = await getDefaults();
  const config: CascadePluginConfig = {
    ...defaults,
    ...expanded
  } as CascadePluginConfig;

  // Validate
  validateConfig(config);

  return config;
}

/**
 * Load configuration from OpenClaw API
 */
export async function loadConfigFromOpenClaw(api: any): Promise<CascadePluginConfig> {
  const entries = api.config?.plugins?.entries || {};
  const pluginConfig = entries['openclaw-cascade-plugin']?.config || entries.cascade?.config || {};
  
  return loadConfig(pluginConfig);
}

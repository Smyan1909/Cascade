/**
 * Test helper utilities
 */

import { join } from 'path';
import { mkdir, rm } from 'fs/promises';

export interface TestConfig {
  cascadeGrpcEndpoint: string;
  cascadePythonPath?: string;
  firestoreProjectId?: string;
  firestoreCredentialsPath?: string;
  headless?: boolean;
  actionTimeoutMs?: number;
  enableA2A?: boolean;
  verbose?: boolean;
}

export function createMockConfig(overrides: Partial<TestConfig> = {}): TestConfig {
  return {
    cascadeGrpcEndpoint: 'localhost:50051',
    headless: false,
    actionTimeoutMs: 8000,
    enableA2A: true,
    verbose: false,
    ...overrides
  };
}

export async function withTempDir(fn: (dir: string) => Promise<void>): Promise<void> {
  const tmpDir = join(process.cwd(), '.test-tmp', Date.now().toString());
  await mkdir(tmpDir, { recursive: true });
  
  try {
    await fn(tmpDir);
  } finally {
    await rm(tmpDir, { recursive: true, force: true });
  }
}

export function waitForCondition(
  condition: () => boolean,
  timeout = 5000,
  interval = 100
): Promise<void> {
  return new Promise((resolve, reject) => {
    const start = Date.now();
    const check = () => {
      if (condition()) {
        resolve();
      } else if (Date.now() - start > timeout) {
        reject(new Error(`Timeout waiting for condition after ${timeout}ms`));
      } else {
        setTimeout(check, interval);
      }
    };
    check();
  });
}

export function generateBase64Image(size: number): string {
  // Generate fake base64 image data
  const chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/';
  let result = '';
  for (let i = 0; i < size; i++) {
    result += chars[Math.floor(Math.random() * chars.length)];
  }
  // Pad to multiple of 4 for valid base64
  while (result.length % 4 !== 0) {
    result += '=';
  }
  return result;
}

export function createMockSpawnImplementation(
  stdoutData: string | string[],
  exitCode = 0
): () => any {
  return () => {
    const { EventEmitter } = require('events');
    const stdout = new EventEmitter();
    const stderr = new EventEmitter();
    
    const proc = Object.assign(new EventEmitter(), {
      stdout,
      stderr,
      stdin: { write: jest.fn() },
      kill: jest.fn()
    });

    // Emit stdout data after a brief delay
    setTimeout(() => {
      const lines = Array.isArray(stdoutData) ? stdoutData : [stdoutData];
      lines.forEach(line => stdout.emit('data', line + '\n'));
      
      setTimeout(() => {
        proc.emit('close', exitCode);
      }, 10);
    }, 10);

    return proc;
  };
}

export function delay(ms: number): Promise<void> {
  return new Promise(resolve => setTimeout(resolve, ms));
}

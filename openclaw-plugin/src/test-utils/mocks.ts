/**
 * Mock implementations for testing
 */

import { EventEmitter } from 'events';

export interface MockChildProcess {
  stdout: EventEmitter;
  stderr: EventEmitter;
  stdin: {
    write: jest.Mock;
  };
  on: jest.Mock;
  once: jest.Mock;
  emit: jest.Mock;
  kill: jest.Mock;
  pid: number;
  connected: boolean;
  disconnect: jest.Mock;
  unref: jest.Mock;
  ref: jest.Mock;
  send: jest.Mock;
}

export function createMockChildProcess(): MockChildProcess {
  const stdout = new EventEmitter();
  const stderr = new EventEmitter();
  
  return {
    stdout,
    stderr,
    stdin: {
      write: jest.fn()
    },
    on: jest.fn(),
    once: jest.fn(),
    emit: jest.fn(),
    kill: jest.fn(),
    pid: 12345,
    connected: true,
    disconnect: jest.fn(),
    unref: jest.fn(),
    ref: jest.fn(),
    send: jest.fn()
  };
}

export class MockCascadeGrpcClient {
  public performAction = jest.fn();
  public getSemanticTree = jest.fn();
  public getScreenshot = jest.fn();
  public startApp = jest.fn();
  public resetState = jest.fn();
  public isConnected = jest.fn().mockReturnValue(true);
  
  registerMockTool(name: string, handler: Function) {
    if (name === 'click_element' || name === 'type_text' || name === 'hover_element' || name === 'focus_element' || name === 'scroll_element' || name === 'wait_visible') {
      this.performAction.mockImplementation(async (_type, selector, payload) => handler({ selector, payload }));
    } else if (name === 'get_semantic_tree') {
      this.getSemanticTree.mockImplementation(async () => handler());
    } else if (name === 'get_screenshot') {
      this.getScreenshot.mockImplementation(async () => handler());
    } else if (name === 'start_app') {
      this.startApp.mockImplementation(async (appName) => handler({ app_name: appName }));
    }
  }

  simulateError(error: Error) {
    this.performAction.mockRejectedValue(error);
    this.getSemanticTree.mockRejectedValue(error);
    this.getScreenshot.mockRejectedValue(error);
    this.startApp.mockRejectedValue(error);
  }

  simulateConnectionError() {
    this.isConnected.mockReturnValue(false);
  }
}

export class MockFirestore {
  private data = new Map<string, any>();

  setDocument(path: string, data: any) {
    this.data.set(path, data);
  }

  collection(path: string) {
    return {
      get: async () => ({
        docs: Array.from(this.data.entries())
          .filter(([key]) => key.startsWith(path))
          .map(([key, value]) => ({
            id: key.split('/').pop(),
            data: () => value
          }))
      }),
      doc: (id: string) => ({
        get: async () => ({
          exists: this.data.has(`${path}/${id}`),
          data: () => this.data.get(`${path}/${id}`)
        }),
        set: async (data: any) => {
          this.data.set(`${path}/${id}`, data);
        }
      })
    };
  }
}

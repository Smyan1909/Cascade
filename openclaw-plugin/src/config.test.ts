import { loadConfig, validateConfig, getDefaults } from './config';
import { CascadePluginConfig } from './types';

describe('Config', () => {
  const originalEnv = process.env;

  beforeEach(() => {
    process.env = { ...originalEnv };
  });

  afterEach(() => {
    process.env = originalEnv;
  });

  describe('getDefaults', () => {
    test('should return default configuration', async () => {
      const defaults = await getDefaults();
      expect(defaults).toEqual({
        cascadeGrpcEndpoint: 'localhost:50051',
        headless: false,
        actionTimeoutMs: 8000,
        enableA2A: true,
        verbose: false,
        screenshotMode: 'auto'
      });
    });
  });

  describe('validateConfig', () => {
    test('should pass with valid config', () => {
      const config: CascadePluginConfig = { cascadeGrpcEndpoint: 'localhost:50051' };
      expect(() => validateConfig(config)).not.toThrow();
    });

    test('should throw when cascadeGrpcEndpoint is missing', () => {
      const config: CascadePluginConfig = {} as any;
      expect(() => validateConfig(config)).toThrow('cascadeGrpcEndpoint is required');
    });
  });

  describe('loadConfig', () => {
    test('should load config with defaults when input is empty', async () => {
      const config = await loadConfig({});
      expect(config.cascadeGrpcEndpoint).toBe('localhost:50051');
    });

    test('should validate loaded config', async () => {
      const input = { cascadeGrpcEndpoint: '' };
      await expect(loadConfig(input as any)).rejects.toThrow('cascadeGrpcEndpoint is required');
    });
  });
});

const fs = require('fs');
const path = require('path');
const os = require('os');

const PLUGIN_ID = 'openclaw-cascade-plugin';
const DEFAULT_ENDPOINT = 'localhost:50051';
const defaultConfigPath = path.join(os.homedir(), '.openclaw', 'openclaw.json');
const legacyConfigPath = path.join(os.homedir(), '.openclaw', 'config.json');
const configPath = process.env.OPENCLAW_CONFIG_PATH ||
  (fs.existsSync(defaultConfigPath) ? defaultConfigPath : legacyConfigPath);

function readConfig(filePath) {
  if (!fs.existsSync(filePath)) {
    return { plugins: { entries: {} } };
  }

  const content = fs.readFileSync(filePath, 'utf8');
  try {
    return JSON.parse(content);
  } catch (error) {
    console.warn('[cascade] Could not parse OpenClaw config; skipping auto-update:', error.message);
    return null;
  }
}

function writeConfig(filePath, config) {
  const dir = path.dirname(filePath);
  if (!fs.existsSync(dir)) {
    fs.mkdirSync(dir, { recursive: true });
  }

  fs.writeFileSync(filePath, JSON.stringify(config, null, 2));
}

function ensurePluginEntry(config) {
  config.plugins = config.plugins || {};
  config.plugins.entries = config.plugins.entries || {};

  // Migrate legacy entry if present
  if (config.plugins.entries.cascade && !config.plugins.entries[PLUGIN_ID]) {
    config.plugins.entries[PLUGIN_ID] = config.plugins.entries.cascade;
    delete config.plugins.entries.cascade;
  }

  const entry = config.plugins.entries[PLUGIN_ID] || {};
  const existingConfig = entry.config || {};

  const cascadeRepoPath = process.env.CASCADE_REPO_PATH;
  const cascadePythonModulePath = process.env.CASCADE_PYTHON_MODULE_PATH ||
    (cascadeRepoPath ? path.join(cascadeRepoPath, 'python') : undefined);

  config.plugins.entries[PLUGIN_ID] = {
    enabled: entry.enabled !== false,
    config: {
      cascadeGrpcEndpoint: existingConfig.cascadeGrpcEndpoint || DEFAULT_ENDPOINT,
      cascadePythonModulePath: existingConfig.cascadePythonModulePath || cascadePythonModulePath,
      cascadePythonPath: existingConfig.cascadePythonPath,
      firestoreProjectId: existingConfig.firestoreProjectId,
      firestoreCredentialsPath: existingConfig.firestoreCredentialsPath,
      headless: existingConfig.headless,
      actionTimeoutMs: existingConfig.actionTimeoutMs,
      enableA2A: existingConfig.enableA2A,
      allowedAgents: existingConfig.allowedAgents,
      requireAgentConfirmation: existingConfig.requireAgentConfirmation,
      verbose: existingConfig.verbose,
      screenshotMode: existingConfig.screenshotMode,
      screenshotDir: existingConfig.screenshotDir
    }
  };
}

const config = readConfig(configPath);
if (!config) {
  process.exit(0);
}

ensurePluginEntry(config);
writeConfig(configPath, config);

console.log(`[cascade] OpenClaw config updated: ${configPath}`);
console.log('[cascade] Plugin entry ensured under plugins.entries.openclaw-cascade-plugin');
if (!process.env.CASCADE_REPO_PATH && !process.env.CASCADE_PYTHON_MODULE_PATH) {
  console.log('[cascade] Tip: set CASCADE_REPO_PATH to your repo to auto-configure PYTHONPATH.');
}

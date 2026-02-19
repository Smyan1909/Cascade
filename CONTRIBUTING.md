# Contributing to Cascade

Thank you for your interest in contributing to Cascade! This document provides guidelines and instructions for contributing.

## Table of Contents

1. [Getting Started](#getting-started)
2. [Development Setup](#development-setup)
3. [Project Structure](#project-structure)
4. [Making Changes](#making-changes)
5. [Testing](#testing)
6. [Submitting Changes](#submitting-changes)
7. [Code Style](#code-style)
8. [Areas for Contribution](#areas-for-contribution)

## Getting Started

### Prerequisites

- Node.js 18+
- Python 3.10+
- .NET 8 SDK (for C# Body)
- Git

### Fork and Clone

```bash
# Fork the repository on GitHub
git clone https://github.com/YOUR_USERNAME/cascade.git
cd cascade
```

## Development Setup

### Plugin Development

```bash
cd openclaw-plugin

# Install dependencies
npm install

# Run tests
npm test

# Build TypeScript
npm run build

# Watch mode for development
npm run watch
```

### Python Brain Development

```bash
cd python

# Create virtual environment
python3 -m venv .venv

# Activate
# Windows:
.venv\Scripts\activate
# macOS/Linux:
source .venv/bin/activate

# Install dependencies
pip install -r requirements.txt

# Run tests
pytest tests/
```

### C# Body Development

```bash
# Build
dotnet build src/Body/Body.csproj

# Run
dotnet run --project src/Body/Body.csproj

# Run tests
dotnet test src/Body.Tests/Body.Tests.csproj
```

## Project Structure

```
cascade/
├── openclaw-plugin/          # TypeScript OpenClaw plugin
│   ├── src/
│   │   ├── tools/           # Tool implementations
│   │   ├── types/           # TypeScript types
│   │   └── test-utils/      # Testing utilities
│   └── tests/
├── python/                   # Python Brain
│   ├── agents/              # Explorer, Worker, Orchestrator
│   ├── cascade_client/      # gRPC client
│   └── tests/
├── src/                      # C# Body
│   ├── Body/                # Main application
│   └── Body.Tests/          # Unit tests
└── docs/                     # Documentation
```

## Making Changes

### Adding a New Tool

1. **Create the tool file** in `openclaw-plugin/src/tools/`
2. **Add tests** in the corresponding `.test.ts` file
3. **Export the tool** from `openclaw-plugin/src/tools/index.ts`
4. **Register the tool** in `openclaw-plugin/src/index.ts`

Example tool structure:

```typescript
// src/tools/my-new-tool.ts
import { ToolRegistry } from './tool-registry';
import { CascadeMcpClient } from '../cascade-client';
import { errorResponse, formatSuccess } from './response-helpers';

export function registerMyNewTool(registry: ToolRegistry, client: CascadeMcpClient): void {
  registry.register({
    name: 'cascade_my_new_tool',
    description: 'Description of what the tool does',
    inputSchema: {
      type: 'object',
      properties: {
        param1: { type: 'string', description: 'Parameter description' }
      },
      required: ['param1']
    },
    handler: async (args) => {
      try {
        const result = await client.callTool('underlying_tool', args);
        return formatSuccess(result);
      } catch (error) {
        return errorResponse(
          error instanceof Error ? error.message : 'Failed'
        );
      }
    }
  });
}
```

### Adding Tests

We follow test-driven development. Tests should be written before or alongside implementation.

```typescript
// src/tools/my-new-tool.test.ts
import { registerMyNewTool } from './my-new-tool';
import { ToolRegistry } from './tool-registry';
import { MockCascadeMcpClient } from '../test-utils';

describe('My New Tool', () => {
  let registry: ToolRegistry;
  let mockClient: MockCascadeMcpClient;

  beforeEach(() => {
    registry = new ToolRegistry();
    mockClient = new MockCascadeMcpClient();
    registerMyNewTool(registry, mockClient as any);
  });

  test('should be registered', () => {
    expect(registry.has('cascade_my_new_tool')).toBe(true);
  });

  test('should handle success', async () => {
    mockClient.registerMockTool('underlying_tool', () => ({ success: true }));
    const result = await registry.call('cascade_my_new_tool', { param1: 'test' });
    expect(result.content[0].text).toContain('success');
  });
});
```

## Testing

### Running Tests

```bash
# Plugin tests
cd openclaw-plugin
npm test

# With coverage
npm run test:coverage

# Specific test
npm test -- --testNamePattern="Desktop Automation"

# Watch mode
npm run test:watch
```

### Test Coverage Requirements

- **Minimum coverage**: 80% for all metrics
- **Target coverage**: 90% for critical paths

### Writing Good Tests

1. **Test the happy path** - Normal usage should work
2. **Test error cases** - Invalid inputs, failures, edge cases
3. **Test async behavior** - Timeouts, race conditions
4. **Mock external dependencies** - Don't make real network calls
5. **Use descriptive names** - Tests should read like documentation

## Submitting Changes

### Before Submitting

1. **Run all tests**
   ```bash
   npm test
   ```

2. **Check code style**
   ```bash
   npm run lint
   npm run lint:fix  # Auto-fix issues
   ```

3. **Build the project**
   ```bash
   npm run build
   ```

4. **Update documentation** if needed

### Pull Request Process

1. **Create a feature branch**
   ```bash
   git checkout -b feature/my-feature
   ```

2. **Make your changes** with clear commit messages
   ```bash
   git commit -m "Add feature X for Y"
   ```

3. **Push to your fork**
   ```bash
   git push origin feature/my-feature
   ```

4. **Create a Pull Request** on GitHub

### PR Checklist

- [ ] Tests pass (`npm test`)
- [ ] Coverage maintained at 80%+
- [ ] Linting passes (`npm run lint`)
- [ ] TypeScript builds (`npm run build`)
- [ ] Documentation updated
- [ ] Commit messages are clear
- [ ] PR description explains the changes

## Code Style

### TypeScript

- Use strict TypeScript settings
- Prefer `const` over `let`
- Use async/await for async code
- Export types and interfaces explicitly
- Use descriptive variable names

Example:

```typescript
// Good
async function fetchUserData(userId: string): Promise<UserData> {
  const response = await apiClient.get(`/users/${userId}`);
  return response.data;
}

// Avoid
function getData(id) {
  return api.get(id).then(r => r.data);
}
```

### Naming Conventions

- **Tools**: `cascade_tool_name` (snake_case)
- **Functions**: `camelCase`
- **Classes**: `PascalCase`
- **Constants**: `UPPER_SNAKE_CASE`
- **Files**: `kebab-case.ts`

### Error Handling

Always provide helpful error messages:

```typescript
try {
  const result = await client.callTool('tool_name', args);
  return formatSuccess(result);
} catch (error) {
  return errorResponse(
    error instanceof Error ? error.message : 'Operation failed',
    'Try checking X or doing Y'  // Helpful suggestion
  );
}
```

## Areas for Contribution

### High Priority

- 🐛 Bug fixes
- 📚 Documentation improvements
- 🧪 Additional test coverage

### Medium Priority

- ✨ New tools for common operations
- 🌍 macOS and Linux support
- 🔧 Tool improvements

### Low Priority

- 🎨 UI improvements
- ⚡ Performance optimizations
- 🧹 Code refactoring

### Ideas for New Tools

- Window management tools (minimize, maximize, resize)
- File system tools (browse, open, save)
- Clipboard tools (copy, paste)
- Process management tools
- System information tools

## Getting Help

- 📖 Read the [documentation](./docs/)
- 🐛 Check [existing issues](https://github.com/yourusername/cascade/issues)
- 💬 Join our [Discord](https://discord.gg/cascade)
- 📧 Email: maintainers@cascade.dev

## Code of Conduct

This project follows a standard code of conduct. Be respectful, inclusive, and constructive in all interactions.

## License

By contributing, you agree that your contributions will be licensed under the MIT License.

---

Thank you for contributing to Cascade! 🎉

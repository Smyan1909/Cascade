# Contributing to Cascade

Thanks for your interest in contributing! This guide focuses on the minimal steps to build, test, and submit changes.

## Prerequisites
- Windows 10/11
- .NET 8 SDK
- Node.js 18+
- Python 3.12+
- Git

## Repo Setup
```bash
git clone https://github.com/Smyan1909/Cascade.git
cd cascade
```

## Development Workflows

### C# Body
```bash
dotnet build src/Body/Body.csproj
dotnet run --project src/Body/Body.csproj
dotnet test src/Body.Tests/Body.Tests.csproj
```

### OpenClaw Plugin
```bash
cd openclaw-plugin
npm install
npm test
npm run build
```

### Python Brain
```bash
cd python
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt
pytest tests\
```

## Protocol Changes
If you modify `proto/cascade.proto`, regenerate Python stubs:
```bash
powershell -File python\generate_proto.ps1
```

## Code Style
- Keep changes focused and minimal.
- Prefer pattern-first UIA actions over raw mouse/keyboard fallbacks.
- Don’t commit generated artifacts unless explicitly required.

## Submitting Changes
1. Create a focused PR with a clear description.
2. Include test results relevant to your changes.
3. Document any new tools or config options.

## Reporting Issues
Use GitHub Issues for bugs, feature requests, and documentation fixes.

## License
By contributing, you agree that your contributions will be licensed under the MIT License.

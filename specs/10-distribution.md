# Distribution and Deployment Specification

## Overview

This specification covers packaging, deployment, and scaling strategies for Cascade. It supports local single-user installations, containerized deployments, and distributed multi-instance setups.

## Deployment Modes

### Local Mode
- Single-user, single-machine deployment
- SQLite database
- All components run on one machine
- Minimal infrastructure requirements

### Distributed Mode
- Multi-user support
- PostgreSQL database
- Horizontal scaling
- Load balancing
- Containerized deployment

## Package Structure

### Windows Installer (MSI/MSIX)

```
CascadeSetup/
├── Cascade.CLI.exe              # Command-line interface
├── Cascade.Server.exe           # gRPC server
├── cascade_agent/               # Python agent package
│   ├── __init__.py
│   └── ...
├── data/
│   ├── cascade.db              # SQLite database (local mode)
│   └── tesseract/              # OCR data files
├── config/
│   ├── appsettings.json        # Default configuration
│   └── cascade_config.yaml     # Python configuration
└── README.txt
```

### Directory Structure (Installed)

```
%PROGRAMFILES%\Cascade\
├── bin/
│   ├── Cascade.CLI.exe
│   ├── Cascade.Server.exe
│   └── *.dll
├── python/
│   ├── venv/                   # Python virtual environment
│   └── cascade_agent/
├── data/
│   ├── cascade.db
│   ├── logs/
│   └── cache/
├── config/
│   ├── appsettings.json
│   └── cascade_config.yaml
└── tessdata/
```

## Docker Configuration

### Dockerfile.server (C# Backend)

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY src/*.sln ./
COPY src/Cascade.Core/*.csproj Cascade.Core/
COPY src/Cascade.UIAutomation/*.csproj Cascade.UIAutomation/
COPY src/Cascade.Vision/*.csproj Cascade.Vision/
COPY src/Cascade.CodeGen/*.csproj Cascade.CodeGen/
COPY src/Cascade.Database/*.csproj Cascade.Database/
COPY src/Cascade.Grpc.Server/*.csproj Cascade.Grpc.Server/

# Restore dependencies
RUN dotnet restore

# Copy source code
COPY src/ ./

# Build
RUN dotnet publish Cascade.Grpc.Server/Cascade.Grpc.Server.csproj \
    -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install dependencies for UI Automation (limited in container)
RUN apt-get update && apt-get install -y \
    tesseract-ocr \
    tesseract-ocr-eng \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Configuration
ENV ASPNETCORE_URLS=http://+:50051
ENV CASCADE_DATABASE__PROVIDER=PostgreSQL
ENV CASCADE_DATABASE__CONNECTIONSTRING=Host=db;Database=cascade;Username=cascade;Password=cascade

EXPOSE 50051

ENTRYPOINT ["dotnet", "Cascade.Grpc.Server.dll"]
```

### Dockerfile.agent (Python)

```dockerfile
FROM python:3.11-slim

WORKDIR /app

# Install system dependencies
RUN apt-get update && apt-get install -y \
    build-essential \
    && rm -rf /var/lib/apt/lists/*

# Copy requirements first for caching
COPY python/requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copy source code
COPY python/cascade_agent/ ./cascade_agent/
COPY python/pyproject.toml .

# Install package
RUN pip install -e .

# Configuration
ENV CASCADE_SERVER_HOST=server
ENV CASCADE_SERVER_PORT=50051
ENV CASCADE_LLM_PROVIDER=openai

ENTRYPOINT ["python", "-m", "cascade_agent"]
```

### docker-compose.yml

```yaml
version: '3.8'

services:
  server:
    build:
      context: .
      dockerfile: docker/Dockerfile.server
    ports:
      - "50051:50051"
    environment:
      - CASCADE_DATABASE__PROVIDER=PostgreSQL
      - CASCADE_DATABASE__CONNECTIONSTRING=Host=db;Database=cascade;Username=cascade;Password=cascade
    depends_on:
      db:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "grpc_health_probe", "-addr=:50051"]
      interval: 10s
      timeout: 5s
      retries: 3
    networks:
      - cascade-network

  agent:
    build:
      context: .
      dockerfile: docker/Dockerfile.agent
    environment:
      - CASCADE_SERVER_HOST=server
      - CASCADE_SERVER_PORT=50051
      - OPENAI_API_KEY=${OPENAI_API_KEY}
      - ANTHROPIC_API_KEY=${ANTHROPIC_API_KEY}
    depends_on:
      server:
        condition: service_healthy
    networks:
      - cascade-network

  db:
    image: postgres:16-alpine
    environment:
      - POSTGRES_USER=cascade
      - POSTGRES_PASSWORD=cascade
      - POSTGRES_DB=cascade
    volumes:
      - cascade-db:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U cascade"]
      interval: 5s
      timeout: 5s
      retries: 5
    networks:
      - cascade-network

volumes:
  cascade-db:

networks:
  cascade-network:
    driver: bridge
```

### docker-compose.prod.yml

```yaml
version: '3.8'

services:
  server:
    image: cascade/server:latest
    deploy:
      replicas: 3
      resources:
        limits:
          cpus: '2'
          memory: 4G
    environment:
      - CASCADE_DATABASE__PROVIDER=PostgreSQL
      - CASCADE_DATABASE__CONNECTIONSTRING=${DATABASE_URL}
    healthcheck:
      test: ["CMD", "grpc_health_probe", "-addr=:50051"]
      interval: 10s
      timeout: 5s
      retries: 3
    networks:
      - cascade-network

  agent:
    image: cascade/agent:latest
    deploy:
      replicas: 2
      resources:
        limits:
          cpus: '1'
          memory: 2G
    environment:
      - CASCADE_SERVER_HOST=server
      - CASCADE_SERVER_PORT=50051
      - OPENAI_API_KEY=${OPENAI_API_KEY}
    depends_on:
      - server
    networks:
      - cascade-network

  load-balancer:
    image: envoyproxy/envoy:v1.28-latest
    ports:
      - "50051:50051"
      - "9901:9901"  # Admin
    volumes:
      - ./config/envoy.yaml:/etc/envoy/envoy.yaml:ro
    networks:
      - cascade-network

  prometheus:
    image: prom/prometheus:v2.48.0
    volumes:
      - ./config/prometheus.yml:/etc/prometheus/prometheus.yml
      - prometheus-data:/prometheus
    ports:
      - "9090:9090"
    networks:
      - cascade-network

  grafana:
    image: grafana/grafana:10.2.0
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=${GRAFANA_PASSWORD}
    volumes:
      - grafana-data:/var/lib/grafana
      - ./config/grafana/dashboards:/etc/grafana/provisioning/dashboards
    ports:
      - "3000:3000"
    networks:
      - cascade-network

volumes:
  prometheus-data:
  grafana-data:

networks:
  cascade-network:
    driver: overlay
```

## Configuration Management

### appsettings.json (C# Server)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Grpc": "Information"
    }
  },
  "Database": {
    "Provider": "SQLite",
    "ConnectionString": "Data Source=cascade.db",
    "AutoMigrate": true,
    "MaxPoolSize": 100
  },
  "GrpcServer": {
    "Port": 50051,
    "EnableReflection": true,
    "EnableDetailedErrors": false,
    "MaxReceiveMessageSize": 16777216,
    "MaxSendMessageSize": 16777216
  },
  "UIAutomation": {
    "DefaultTimeout": "00:00:30",
    "EnableCaching": true,
    "CacheDuration": "00:00:05"
  },
  "Vision": {
    "TesseractDataPath": "./tessdata",
    "DefaultOcrLanguage": "eng"
  },
  "CodeGen": {
    "DefaultNamespace": "Cascade.Generated",
    "OptimizeCode": true,
    "EnableSandbox": true
  },
  "Telemetry": {
    "Enabled": true,
    "ServiceName": "cascade-server",
    "OtlpEndpoint": "http://localhost:4317"
  }
}
```

### cascade_config.yaml (Python)

```yaml
# Server connection
server:
  host: localhost
  port: 50051
  use_ssl: false
  ssl_cert_path: null

# LLM configuration
llm:
  openai:
    api_key: ${OPENAI_API_KEY}
    model: gpt-4o
    temperature: 0.7
    max_tokens: 4096
  
  anthropic:
    api_key: ${ANTHROPIC_API_KEY}
    model: claude-3-5-sonnet-20241022
    temperature: 0.7
  
  default_provider: openai
  fallback_enabled: true

# Agent configuration
agent:
  max_retries: 3
  timeout_seconds: 300
  enable_logging: true
  log_level: INFO

# Exploration settings
exploration:
  max_depth: 10
  screenshot_interval: 1000
  ocr_enabled: true

# Logging
logging:
  level: INFO
  format: "%(asctime)s - %(name)s - %(levelname)s - %(message)s"
  file: logs/cascade_agent.log
```

### Environment Variables

```bash
# Database
CASCADE_DATABASE__PROVIDER=PostgreSQL
CASCADE_DATABASE__CONNECTIONSTRING=Host=localhost;Database=cascade;Username=cascade;Password=password

# gRPC Server
CASCADE_GRPCSERVER__PORT=50051
CASCADE_GRPCSERVER__ENABLEREFLECTION=true

# LLM API Keys
OPENAI_API_KEY=sk-...
ANTHROPIC_API_KEY=sk-ant-...
AZURE_OPENAI_KEY=...
AZURE_OPENAI_ENDPOINT=https://....openai.azure.com/

# Telemetry
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317
OTEL_SERVICE_NAME=cascade
```

## Telemetry and Monitoring

### OpenTelemetry Integration

```csharp
// Program.cs
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddGrpcClientInstrumentation()
            .AddSource("Cascade.*")
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(config["Telemetry:OtlpEndpoint"]);
            });
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddMeter("Cascade.*")
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(config["Telemetry:OtlpEndpoint"]);
            });
    });
```

### Custom Metrics

```csharp
public class CascadeMetrics
{
    private readonly Counter<long> _agentExecutions;
    private readonly Counter<long> _agentErrors;
    private readonly Histogram<double> _executionDuration;
    private readonly Counter<long> _uiActions;
    
    public CascadeMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("Cascade.Core");
        
        _agentExecutions = meter.CreateCounter<long>(
            "cascade.agent.executions",
            description: "Number of agent executions");
        
        _agentErrors = meter.CreateCounter<long>(
            "cascade.agent.errors",
            description: "Number of agent errors");
        
        _executionDuration = meter.CreateHistogram<double>(
            "cascade.agent.execution_duration",
            unit: "ms",
            description: "Duration of agent executions");
        
        _uiActions = meter.CreateCounter<long>(
            "cascade.ui.actions",
            description: "Number of UI automation actions");
    }
    
    public void RecordExecution(string agentName, bool success, double durationMs)
    {
        var tags = new TagList
        {
            { "agent", agentName },
            { "success", success.ToString() }
        };
        
        _agentExecutions.Add(1, tags);
        _executionDuration.Record(durationMs, tags);
        
        if (!success)
            _agentErrors.Add(1, tags);
    }
}
```

### Health Checks

```csharp
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database")
    .AddCheck<GrpcHealthCheck>("grpc")
    .AddCheck<UIAutomationHealthCheck>("ui_automation");

public class DatabaseHealthCheck : IHealthCheck
{
    private readonly CascadeDbContext _context;
    
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            await _context.Database.CanConnectAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(ex.Message);
        }
    }
}
```

## Installation Scripts

### install.ps1 (Windows)

```powershell
#Requires -RunAsAdministrator

param(
    [string]$InstallPath = "$env:ProgramFiles\Cascade",
    [switch]$SkipPython
)

Write-Host "Installing Cascade..."

# Create directories
New-Item -ItemType Directory -Force -Path $InstallPath
New-Item -ItemType Directory -Force -Path "$InstallPath\bin"
New-Item -ItemType Directory -Force -Path "$InstallPath\data"
New-Item -ItemType Directory -Force -Path "$InstallPath\config"
New-Item -ItemType Directory -Force -Path "$InstallPath\logs"

# Copy binaries
Copy-Item -Recurse ".\bin\*" "$InstallPath\bin\"

# Copy configuration
Copy-Item ".\config\appsettings.json" "$InstallPath\config\"
Copy-Item ".\config\cascade_config.yaml" "$InstallPath\config\"

# Setup Python environment
if (-not $SkipPython) {
    Write-Host "Setting up Python environment..."
    python -m venv "$InstallPath\python\venv"
    & "$InstallPath\python\venv\Scripts\pip" install -r ".\python\requirements.txt"
    Copy-Item -Recurse ".\python\cascade_agent" "$InstallPath\python\"
}

# Add to PATH
$currentPath = [Environment]::GetEnvironmentVariable("Path", "Machine")
if ($currentPath -notlike "*$InstallPath\bin*") {
    [Environment]::SetEnvironmentVariable(
        "Path", 
        "$currentPath;$InstallPath\bin", 
        "Machine"
    )
}

# Create Windows service
New-Service -Name "CascadeServer" `
    -BinaryPathName "$InstallPath\bin\Cascade.Server.exe" `
    -DisplayName "Cascade Server" `
    -Description "Cascade AI Agent Builder Server" `
    -StartupType Automatic

Write-Host "Installation complete!"
Write-Host "Start the server with: Start-Service CascadeServer"
Write-Host "Or run manually: cascade-server"
```

### uninstall.ps1

```powershell
#Requires -RunAsAdministrator

param(
    [string]$InstallPath = "$env:ProgramFiles\Cascade"
)

Write-Host "Uninstalling Cascade..."

# Stop and remove service
Stop-Service -Name "CascadeServer" -ErrorAction SilentlyContinue
sc.exe delete "CascadeServer"

# Remove from PATH
$currentPath = [Environment]::GetEnvironmentVariable("Path", "Machine")
$newPath = ($currentPath.Split(';') | Where-Object { $_ -notlike "*Cascade*" }) -join ';'
[Environment]::SetEnvironmentVariable("Path", $newPath, "Machine")

# Remove files
Remove-Item -Recurse -Force $InstallPath

Write-Host "Uninstallation complete!"
```

## CI/CD Pipeline

### GitHub Actions (.github/workflows/build.yml)

```yaml
name: Build and Release

on:
  push:
    branches: [main]
    tags: ['v*']
  pull_request:
    branches: [main]

jobs:
  build-dotnet:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      
      - name: Restore
        run: dotnet restore src/Cascade.sln
      
      - name: Build
        run: dotnet build src/Cascade.sln -c Release --no-restore
      
      - name: Test
        run: dotnet test src/Cascade.sln -c Release --no-build
      
      - name: Publish
        run: dotnet publish src/Cascade.Grpc.Server/Cascade.Grpc.Server.csproj -c Release -o publish
      
      - name: Upload Artifact
        uses: actions/upload-artifact@v4
        with:
          name: cascade-server
          path: publish/

  build-python:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup Python
        uses: actions/setup-python@v5
        with:
          python-version: '3.11'
      
      - name: Install dependencies
        run: |
          pip install -r python/requirements.txt
          pip install pytest pytest-asyncio
      
      - name: Run tests
        run: pytest python/tests/
      
      - name: Build package
        run: pip wheel -w dist python/

  build-docker:
    needs: [build-dotnet, build-python]
    runs-on: ubuntu-latest
    if: startsWith(github.ref, 'refs/tags/')
    steps:
      - uses: actions/checkout@v4
      
      - name: Set up Docker Buildx
        uses: docker/setup-buildx-action@v3
      
      - name: Login to Docker Hub
        uses: docker/login-action@v3
        with:
          username: ${{ secrets.DOCKER_USERNAME }}
          password: ${{ secrets.DOCKER_TOKEN }}
      
      - name: Build and push server
        uses: docker/build-push-action@v5
        with:
          context: .
          file: docker/Dockerfile.server
          push: true
          tags: cascade/server:${{ github.ref_name }},cascade/server:latest
      
      - name: Build and push agent
        uses: docker/build-push-action@v5
        with:
          context: .
          file: docker/Dockerfile.agent
          push: true
          tags: cascade/agent:${{ github.ref_name }},cascade/agent:latest

  release:
    needs: [build-docker]
    runs-on: windows-latest
    if: startsWith(github.ref, 'refs/tags/')
    steps:
      - uses: actions/checkout@v4
      
      - name: Download artifacts
        uses: actions/download-artifact@v4
      
      - name: Create installer
        run: |
          # Build MSI installer using WiX
          dotnet tool install --global wix
          wix build installer/cascade.wxs -o cascade-setup.msi
      
      - name: Create Release
        uses: softprops/action-gh-release@v1
        with:
          files: cascade-setup.msi
```

## Scaling Considerations

### Horizontal Scaling
- Multiple gRPC server instances behind load balancer
- Shared PostgreSQL database
- Redis for distributed caching (optional)
- Message queue for async operations (optional)

### Resource Requirements

| Component | Min CPU | Min RAM | Recommended CPU | Recommended RAM |
|-----------|---------|---------|-----------------|-----------------|
| Server (per instance) | 1 core | 1 GB | 2 cores | 4 GB |
| Agent (per instance) | 0.5 core | 512 MB | 1 core | 2 GB |
| PostgreSQL | 1 core | 2 GB | 4 cores | 8 GB |
| Load Balancer | 0.5 core | 256 MB | 1 core | 512 MB |



# Cascade Explorer - Autonomous Mode Startup Script
# Runs the MCP-driven autonomous Explorer that lets the LLM decide actions

# Get project root (where this script is located)
$projectRoot = $PSScriptRoot
if (-not $projectRoot) {
    $projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
}
$pythonDir = Join-Path $projectRoot "python"

Write-Host "Cascade Explorer - AUTONOMOUS Mode" -ForegroundColor Magenta
Write-Host "===================================" -ForegroundColor Magenta
Write-Host "LLM will decide what actions to take" -ForegroundColor Yellow
Write-Host ""

# Check if venv exists
$venvPath = Join-Path $pythonDir ".venv"
if (-not (Test-Path $venvPath)) {
    Write-Host "Virtual environment not found!" -ForegroundColor Red
    Write-Host "Run setup_venv.ps1 first from the python directory" -ForegroundColor Yellow
    exit 1
}

# Activate venv
$activateScript = Join-Path $venvPath "Scripts\Activate.ps1"
Write-Host "[OK] Activating virtual environment..." -ForegroundColor Green
. $activateScript

# Load environment variables from .env
$envFile = Join-Path $projectRoot ".env"
if (Test-Path $envFile) {
    Write-Host "[OK] Loading environment variables from .env..." -ForegroundColor Green
    Get-Content $envFile | ForEach-Object {
        if ($_ -match '^([^#][^=]+)=(.*)$') {
            $varKey = $matches[1].Trim()
            $varValue = $matches[2].Trim()
            [Environment]::SetEnvironmentVariable($varKey, $varValue, 'Process')
        }
    }
}

# Verify critical environment variables
$required = @('CASCADE_USER_ID', 'CASCADE_GRPC_ENDPOINT', 'CASCADE_MODEL_API_KEY')
$missing = @()
foreach ($var in $required) {
    if (-not (Test-Path "env:$var")) {
        $missing += $var
    }
}

if ($missing.Count -gt 0) {
    Write-Host "Missing required environment variables:" -ForegroundColor Red
    $missing | ForEach-Object { Write-Host "  - $_" -ForegroundColor Yellow }
    exit 1
}

Write-Host "[OK] Environment configured:" -ForegroundColor Green
Write-Host "  CASCADE_USER_ID: $env:CASCADE_USER_ID" -ForegroundColor Gray
Write-Host "  CASCADE_GRPC_ENDPOINT: $env:CASCADE_GRPC_ENDPOINT" -ForegroundColor Gray
Write-Host "  CASCADE_MODEL_PROVIDER: $env:CASCADE_MODEL_PROVIDER" -ForegroundColor Gray
Write-Host ""

# Check if prerequisites are running
Write-Host "Checking prerequisites..." -ForegroundColor Cyan

# Check Body gRPC server
try {
    $endpoint = $env:CASCADE_GRPC_ENDPOINT
    $endpointParts = $endpoint -split ':'
    $grpcHost = $endpointParts[0]
    $grpcPort = [int]$endpointParts[1]
    
    $connection = Test-NetConnection -ComputerName $grpcHost -Port $grpcPort -WarningAction SilentlyContinue -ErrorAction SilentlyContinue
    if ($connection.TcpTestSucceeded) {
        Write-Host "[OK] Body gRPC server is running on $endpoint" -ForegroundColor Green
    } else {
        Write-Host "[FAIL] Body gRPC server not reachable on $endpoint" -ForegroundColor Red
        Write-Host "  Start it with: cd src\Body; dotnet run" -ForegroundColor Yellow
        Read-Host "Press Enter to exit"
        exit 1
    }
} catch {
    Write-Host "[WARN] Could not check Body server status: $_" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Starting Autonomous Explorer..." -ForegroundColor Magenta
Write-Host "===================================" -ForegroundColor Magenta
Write-Host ""

# Run Explorer in autonomous mode
Set-Location $pythonDir
python (Join-Path $pythonDir "run_explorer_autonomous.py")

$exitCode = $LASTEXITCODE

Write-Host ""
if ($exitCode -eq 0) {
    Write-Host "Autonomous Explorer completed successfully!" -ForegroundColor Green
} else {
    Write-Host "Autonomous Explorer failed with exit code: $exitCode" -ForegroundColor Red
}

Write-Host ""
Read-Host "Press Enter to exit"

exit $exitCode

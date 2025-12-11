# Cascade Explorer - Complete Startup Script
# Automatically checks prerequisites, loads environment, and runs Explorer

# Get project root (where this script is located)
$projectRoot = $PSScriptRoot
if (-not $projectRoot) {
    $projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
}
$pythonDir = Join-Path $projectRoot "python"

Write-Host "Cascade Explorer - Complete Runner" -ForegroundColor Cyan
Write-Host "===================================" -ForegroundColor Cyan
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
$required = @('CASCADE_USER_ID', 'CASCADE_GRPC_ENDPOINT')
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

# Check Firestore emulator
if ($env:FIRESTORE_EMULATOR_HOST) {
    $firestoreEndpointParts = $env:FIRESTORE_EMULATOR_HOST -split ':'
    $firestoreHost = $firestoreEndpointParts[0]
    $firestorePort = [int]$firestoreEndpointParts[1]
    
    try {
        $firestoreConnection = Test-NetConnection -ComputerName $firestoreHost -Port $firestorePort -WarningAction SilentlyContinue -ErrorAction SilentlyContinue
        if ($firestoreConnection.TcpTestSucceeded) {
            Write-Host "[OK] Firestore emulator is running on $env:FIRESTORE_EMULATOR_HOST" -ForegroundColor Green
        } else {
            Write-Host "[FAIL] Firestore emulator not reachable on $env:FIRESTORE_EMULATOR_HOST" -ForegroundColor Red
            Write-Host "  Start it with: firebase emulators:start --only firestore --project cascade-prototype" -ForegroundColor Yellow
            Read-Host "Press Enter to exit"
            exit 1
        }
    } catch {
        Write-Host "[WARN] Could not check Firestore emulator status" -ForegroundColor Yellow
    }
}

Write-Host ""

# Load calculator instructions
$instrFile = Join-Path $projectRoot "instr_calculator.json"
if (-not (Test-Path $instrFile)) {
    Write-Host "Calculator instructions not found: $instrFile" -ForegroundColor Red
    exit 1
}

Write-Host "[OK] Loaded calculator instructions" -ForegroundColor Green
Write-Host ""

Write-Host "Starting Explorer Agent..." -ForegroundColor Cyan
Write-Host "===================================" -ForegroundColor Cyan
Write-Host ""

# Run Explorer - use the helper script which handles JSON properly
Set-Location $pythonDir
python (Join-Path $pythonDir "run_explorer_calc.py")

$exitCode = $LASTEXITCODE

Write-Host ""
if ($exitCode -eq 0) {
    Write-Host "Explorer completed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "  - Check Firestore Emulator UI: http://localhost:4000/firestore" -ForegroundColor Gray
    Write-Host "  - Look for skill maps under: /artifacts/cascade-prototype/users/test-user/skill_maps/" -ForegroundColor Gray
} else {
    Write-Host "Explorer failed with exit code: $exitCode" -ForegroundColor Red
}

Write-Host ""
Read-Host "Press Enter to exit"

exit $exitCode

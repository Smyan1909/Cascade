# PowerShell script to generate Python gRPC stubs from proto files
# Usage: .\generate_proto.ps1

$ErrorActionPreference = "Stop"

Write-Host "Generating Python gRPC stubs..." -ForegroundColor Green

$scriptDir = $PSScriptRoot
$protoDir = Join-Path (Split-Path $scriptDir -Parent) "proto"
$outputDir = Join-Path $scriptDir "cascade_client\proto"

# Create output directory if it doesn't exist
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    Write-Host "Created directory: $outputDir" -ForegroundColor Cyan
}

$protoFile = Join-Path $protoDir "cascade.proto"
if (-not (Test-Path $protoFile)) {
    Write-Host "Error: Proto file not found at $protoFile" -ForegroundColor Red
    exit 1
}

Write-Host "Proto file: $protoFile" -ForegroundColor Cyan
Write-Host "Output directory: $outputDir" -ForegroundColor Cyan

# Generate Python stubs
python -m grpc_tools.protoc `
    -I "$protoDir" `
    --python_out="$outputDir" `
    --grpc_python_out="$outputDir" `
    "$protoFile"

if ($LASTEXITCODE -eq 0) {
    Write-Host "Successfully generated gRPC stubs!" -ForegroundColor Green
    Write-Host "Generated files:" -ForegroundColor Cyan
    Get-ChildItem $outputDir | ForEach-Object { Write-Host "  - $($_.Name)" -ForegroundColor White }
} else {
    Write-Host "Error: Failed to generate gRPC stubs" -ForegroundColor Red
    exit 1
}


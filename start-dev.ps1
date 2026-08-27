<#
.SYNOPSIS
    Starts the full Playr dev environment: Postgres (Docker), backend API, and frontend.

.DESCRIPTION
    - Ensures Docker Desktop is running, then starts the postgres container via docker compose.
    - Starts the .NET API (src/Playr.Api) in the background, logging to api_stdout.log / api_stderr.log.
    - Starts the frontend (npm run dev) in the background, logging to dev_stdout.log / dev_stderr.log.
    - Writes PID files (api.pid, frontend.pid) so services can be checked/stopped later.
    - Skips starting a service if it's already running (based on the PID file).

.PARAMETER FrontendPath
    Path to the Playr-Frontend repo. Defaults to sibling folder "..\Playr-Frontend".

.EXAMPLE
    .\start-dev.ps1
#>

param(
    [string]$FrontendPath = (Join-Path (Split-Path $PSScriptRoot -Parent) "Playr-Frontend")
)

$ErrorActionPreference = "Stop"
$repoRoot = $PSScriptRoot

function Test-ProcessAlive($pidFile) {
    if (Test-Path $pidFile) {
        $procId = Get-Content $pidFile -ErrorAction SilentlyContinue
        if ($procId) {
            $proc = Get-Process -Id $procId -ErrorAction SilentlyContinue
            if ($proc) { return $proc }
        }
    }
    return $null
}

Write-Host "=== 1/3 Docker Desktop & Postgres ===" -ForegroundColor Cyan
$dockerRunning = $false
try {
    docker ps *> $null
    if ($LASTEXITCODE -eq 0) { $dockerRunning = $true }
} catch { $dockerRunning = $false }

if (-not $dockerRunning) {
    Write-Host "Starting Docker Desktop..."
    $dockerExe = "C:\Program Files\Docker\Docker\Docker Desktop.exe"
    if (Test-Path $dockerExe) {
        Start-Process $dockerExe | Out-Null
    } else {
        Write-Warning "Docker Desktop executable not found at $dockerExe. Start it manually."
    }

    $maxWait = 90
    $elapsed = 0
    while ($elapsed -lt $maxWait) {
        docker ps *> $null
        if ($LASTEXITCODE -eq 0) { break }
        Start-Sleep -Seconds 5
        $elapsed += 5
    }
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Docker did not become ready after $maxWait s. Continuing anyway."
    } else {
        Write-Host "Docker ready after ~$elapsed s"
    }
}

Push-Location $repoRoot
try {
    docker compose up -d
} finally {
    Pop-Location
}

Write-Host "=== 2/3 Backend API ===" -ForegroundColor Cyan
$apiPidFile = Join-Path $repoRoot "api.pid"
$existingApi = Test-ProcessAlive $apiPidFile
if ($existingApi) {
    Write-Host "API already running (PID $($existingApi.Id)), skipping."
} else {
    $proc = Start-Process -FilePath "dotnet" `
        -ArgumentList "run", "--project", "src\Playr.Api\Playr.Api.csproj" `
        -WorkingDirectory $repoRoot `
        -RedirectStandardOutput (Join-Path $repoRoot "api_stdout.log") `
        -RedirectStandardError (Join-Path $repoRoot "api_stderr.log") `
        -PassThru -WindowStyle Hidden
    $proc.Id | Set-Content $apiPidFile
    Write-Host "API starting (PID $($proc.Id)). Logs: api_stdout.log / api_stderr.log"
}

Write-Host "=== 3/3 Frontend ===" -ForegroundColor Cyan
$frontendPidFile = Join-Path $FrontendPath "frontend.pid"
$existingFrontend = Test-ProcessAlive $frontendPidFile
if ($existingFrontend) {
    Write-Host "Frontend already running (PID $($existingFrontend.Id)), skipping."
} elseif (-not (Test-Path $FrontendPath)) {
    Write-Warning "Frontend path not found: $FrontendPath"
} else {
    $npmCmd = (Get-Command npm.cmd -ErrorAction SilentlyContinue)
    $npmExe = if ($npmCmd) { $npmCmd.Source } else { "npm" }
    $proc = Start-Process -FilePath $npmExe `
        -ArgumentList "run", "dev" `
        -WorkingDirectory $FrontendPath `
        -RedirectStandardOutput (Join-Path $FrontendPath "dev_stdout.log") `
        -RedirectStandardError (Join-Path $FrontendPath "dev_stderr.log") `
        -PassThru -WindowStyle Hidden
    $proc.Id | Set-Content $frontendPidFile
    Write-Host "Frontend starting (PID $($proc.Id)). Logs: dev_stdout.log / dev_stderr.log"
}

Write-Host ""
Write-Host "=== Done ===" -ForegroundColor Green
Write-Host "API:      http://localhost:5258"
Write-Host "Frontend: http://localhost:5173"
Write-Host "Postgres: localhost:5432"

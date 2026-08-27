<#
.SYNOPSIS
    Stops the Playr dev environment started by start-dev.ps1 (API, frontend, and optionally Postgres container).

.PARAMETER FrontendPath
    Path to the Playr-Frontend repo. Defaults to sibling folder "..\Playr-Frontend".

.PARAMETER StopPostgres
    Also stop the postgres docker container.

.EXAMPLE
    .\stop-dev.ps1
    .\stop-dev.ps1 -StopPostgres
#>

param(
    [string]$FrontendPath = (Join-Path (Split-Path $PSScriptRoot -Parent) "Playr-Frontend"),
    [switch]$StopPostgres
)

$repoRoot = $PSScriptRoot

function Stop-ByPidFile($pidFile, $label) {
    if (Test-Path $pidFile) {
        $procId = Get-Content $pidFile -ErrorAction SilentlyContinue
        if ($procId) {
            $proc = Get-Process -Id $procId -ErrorAction SilentlyContinue
            if ($proc) {
                Write-Host "Stopping $label (PID $procId) and child processes..."
                # dotnet/npm spawn child processes; kill the whole tree
                & taskkill /PID $procId /T /F *> $null
            } else {
                Write-Host "$label not running (stale PID $procId)."
            }
        }
        Remove-Item $pidFile -ErrorAction SilentlyContinue
    } else {
        Write-Host "$label pid file not found, skipping."
    }
}

Stop-ByPidFile (Join-Path $repoRoot "api.pid") "API"
Stop-ByPidFile (Join-Path $FrontendPath "frontend.pid") "Frontend"

if ($StopPostgres) {
    Push-Location $repoRoot
    try {
        docker compose down
    } finally {
        Pop-Location
    }
}

Write-Host "Done."

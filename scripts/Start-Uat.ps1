<#
.SYNOPSIS
    Starts the whole application for manual UAT with an emulated Hetzner backend.

.DESCRIPTION
    Runs the Aspire AppHost (PostgreSQL, API, WebApp, React app) in the Development environment with the UAT profile:
      * development login bypass (you are signed in as the seeded debug administrator),
      * the Hetzner runtime provider replaced by an in-memory emulation (no cloud calls, no cost),
      * one seeded fleet server per card state (off, running, starting, stopping, attention, unconfirmed, ...),
      * "UAT 9 Costing (accrued cost)", a running session that has already accrued cost (about 1.20 EUR, rising by
        one cent per minute): open it to see the accrued cost and to set a cost alert,
      * a funded UAT cost owner: it is credited once with 1000 EUR (ledger reference UAT-SEED-CREDIT), so the real
        cost reconciler keeps charging the seeded sessions instead of stopping them for missing credit,
      * a Mailpit inbox at http://localhost:8025 that receives cost alert emails (SMTP on localhost:1025),
      * server automation scenarios UAT 11 to 16 with fast timers (see below).

    The canary React UI (/next/) is the only UI that is opened. The classic Blazor WebApp still runs headless
    because the React dev server proxies /appsettings.json, /media and the logo images to it.

    The emulation never executes a game process. Emulated workers (UAT 10 to 13) acknowledge stops, so a shutdown
    completes and the server turns Off; a freshly provisioned deployment registers no worker, so a started server stays
    in "waiting for worker". Failure and unconfirmed-stop states are covered by the seeded servers instead.

    Automation scenarios UAT 11 to 16 (automation timers run fast: a check every 5 seconds, a warning one minute
    before an automatic stop, idle shutdown from one minute):
      * UAT 11 Idle shutdown: nobody is on the server. Settings > Automation: switch on idle shutdown at 1 minute.
        The warning appears, and one minute later the server shuts down by itself.
      * UAT 12 Maximum runtime: the session is 70 minutes old and players are online. Set a maximum runtime of
        1 hour: it warns and then stops although players are online.
      * UAT 13 Scheduled stop: running server. Add a Stop rule two to three minutes ahead (UTC): it warns one
        minute before and stops at that time.
      * UAT 14 Scheduled start: off server. Add a Start rule two minutes ahead and allow scheduled starts: a
        "Start server" job is queued by the schedule.
      * UAT 15 Scheduled start without credit: the start rule was skipped because the cost owner cannot pay.
      * UAT 16 Scheduled starts paused: the consent belongs to another cost owner, so starts need new consent.
    Alert emails (including the automatic shutdown warning) arrive in Mailpit once the email channel is on.

.PARAMETER Fresh
    Deletes the PostgreSQL data volume first so the database is rebuilt and reseeded from scratch.

.PARAMETER NoBrowser
    Does not open the fleet page in the browser.

.EXAMPLE
    ./scripts/Start-Uat.ps1
.EXAMPLE
    ./scripts/Start-Uat.ps1 -Fresh
#>
[CmdletBinding()]
param(
    [switch] $Fresh,
    [switch] $NoBrowser
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$appHostProject = Join-Path $repositoryRoot 'sources/AppHost/AppHost.csproj'
$fleetUrl = 'http://localhost:5173/next/command/servers'

# Podman is the project's container runtime; Docker is only a fallback.
$runtime = @('podman', 'docker') | Where-Object { Get-Command $_ -ErrorAction SilentlyContinue } | Select-Object -First 1
if (-not $runtime) {
    throw 'Podman (or Docker) is required: Aspire runs PostgreSQL in a container.'
}
$previousPreference = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
& $runtime info 2>&1 | Out-Null
$runtimeReady = $LASTEXITCODE -eq 0
$ErrorActionPreference = $previousPreference
if (-not $runtimeReady) {
    $hint = if ($runtime -eq 'podman') { 'Start the machine with: podman machine start' } else { 'Start Docker Desktop.' }
    throw "The $runtime service is not reachable. $hint"
}
$env:ASPIRE_CONTAINER_RUNTIME = $runtime
Write-Host "Container runtime: $runtime" -ForegroundColor Cyan

if ($Fresh) {
    Write-Host 'Removing PostgreSQL data volumes (-Fresh)...' -ForegroundColor Yellow
    $volumes = & $runtime volume ls --format '{{.Name}}' | Where-Object { $_ -like '*pg-database*' }
    foreach ($volume in $volumes) {
        & $runtime volume rm $volume | Out-Null
        Write-Host "  removed $volume"
    }
}

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'
$env:Uat__Enabled = 'true'
# Launched without a launch profile so no browser tab opens on its own (the old launch profile
# opened the Aspire dashboard); these are the settings the dashboard needs in that case.
$env:ASPIRE_ALLOW_UNSECURED_TRANSPORT = 'true'
$env:ASPNETCORE_URLS = 'http://localhost:15012'
$env:DOTNET_DASHBOARD_OTLP_ENDPOINT_URL = 'http://localhost:19046'
$env:DOTNET_RESOURCE_SERVICE_ENDPOINT_URL = 'http://localhost:20125'

Write-Host ''
Write-Host 'UAT profile: login bypass + emulated Hetzner + seeded fleet servers' -ForegroundColor Cyan
Write-Host "Fleet page (once the React app is up): $fleetUrl" -ForegroundColor Cyan
Write-Host 'Stop everything with Ctrl+C.' -ForegroundColor Cyan
Write-Host ''

if (-not $NoBrowser) {
    # Open the browser in the background once the Vite dev server answers.
    Start-Job -ArgumentList $fleetUrl -ScriptBlock {
        param($url)
        for ($i = 0; $i -lt 180; $i++) {
            try {
                Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 2 | Out-Null
                Start-Process $url
                return
            } catch {
                Start-Sleep -Seconds 2
            }
        }
    } | Out-Null
}

try {
    dotnet run --project $appHostProject --configuration Debug --no-launch-profile
} finally {
    Get-Job | Remove-Job -Force -ErrorAction SilentlyContinue
}

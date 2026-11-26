<#
.SYNOPSIS
    Generates or compares the Playwright visual baselines (@visual tests) inside one pinned Linux container.

.DESCRIPTION
    Pixel output depends on the operating system, the browser build and the installed fonts. To keep the committed
    baselines comparable, they are generated and compared in exactly one environment: the official Playwright image
    pinned below by version and digest. This script is the only place where baselines are produced or checked
    (UTF_VISUAL_BASELINES=1). It never runs in CI and does not touch any pipeline file; the mocked suite that CI runs
    skips every pixel comparison and keeps the layout metrics.

    What it does:
      1. Picks a container engine (Podman first, then Docker, or the one named with -Engine) and fails with a clear
         message when none of them is running.
      2. Mounts the repository's `sources` directory read-only, copies it into the container without node_modules,
         build output and test results (the Windows or macOS node_modules never reaches Linux), and runs `npm ci`
         from the committed lockfile. The npm download cache lives in a named volume, so repeated runs are quick.
      3. Mounts `sources/ReactApp/tests/e2e/__screenshots__` read-write, so generated baselines land in the working
         tree, ready to be reviewed and committed.
      4. Runs `npx playwright test --grep @visual` with UTF_VISUAL_BASELINES=1; with -Update it adds
         `--update-snapshots`.

    The page fonts are vendored test fonts served by tests/e2e/support/visual.ts, so the image's system fonts do not
    influence the result.

.PARAMETER Update
    Writes new baselines instead of comparing against the committed ones (passes --update-snapshots).

.PARAMETER Engine
    Forces the container engine, `docker` or `podman`. By default Podman is used when it is running, otherwise Docker.

.EXAMPLE
    ./scripts/Update-VisualBaselines.ps1
    Compares the current rendering with the committed baselines.
.EXAMPLE
    ./scripts/Update-VisualBaselines.ps1 -Update
    Regenerates the baselines, then review `git status` and commit the changed images.
.EXAMPLE
    ./scripts/Update-VisualBaselines.ps1 -Update -Engine docker
#>
[CmdletBinding()]
param(
    [switch] $Update,
    [ValidateSet('docker', 'podman')]
    [string] $Engine
)

$ErrorActionPreference = 'Stop'

# Playwright 1.62.1 on Ubuntu 24.04 (noble), pinned by the multi-architecture index digest resolved from
# mcr.microsoft.com on 2026-11-25. Used only to render test pages, never to build or deploy anything. Bumping
# @playwright/test means bumping this tag and digest together and regenerating every baseline.
$image = 'mcr.microsoft.com/playwright:v1.62.1-noble@sha256:dcc5531e97840b9b5e794f2814476b21571c5124a3fca2267d73041f56e7580e'
$npmCacheVolume = 'utf-visual-baselines-npm-cache'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$sourcesPath = Join-Path $repositoryRoot 'sources'
$screenshotsPath = Join-Path $sourcesPath 'ReactApp/tests/e2e/__screenshots__'

function Test-EngineRunning([string] $name) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) { return $false }
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    & $name info 2>&1 | Out-Null
    $running = $LASTEXITCODE -eq 0
    $ErrorActionPreference = $previousPreference
    return $running
}

$candidates = if ($Engine) { @($Engine) } else { @('podman', 'docker') }
$runtime = $candidates | Where-Object { Test-EngineRunning $_ } | Select-Object -First 1
if (-not $runtime) {
    $names = $candidates -join ' or '
    throw "No running container engine found ($names). Start the Podman machine ('podman machine start') or Docker Desktop and run the script again."
}
Write-Host "Container engine: $runtime" -ForegroundColor Cyan
Write-Host "Image: $image" -ForegroundColor Cyan

New-Item -ItemType Directory -Force -Path $screenshotsPath | Out-Null

$playwrightArguments = '--grep @visual'
if ($Update) { $playwrightArguments += ' --update-snapshots' }

$containerScript = @(
    'set -eu',
    'mkdir -p /work/sources',
    'tar -C /host/sources --exclude=node_modules --exclude=dist --exclude=test-results --exclude=playwright-report --exclude=__screenshots__ -cf - . | tar -C /work/sources -xf -',
    'cd /work/sources/ReactApp',
    'npm ci --no-audit --no-fund',
    "npx playwright test $playwrightArguments"
) -join "`n"

$mode = if ($Update) { 'Generating baselines' } else { 'Comparing against committed baselines' }
Write-Host "$mode (UTF_VISUAL_BASELINES=1)..." -ForegroundColor Cyan

& $runtime run --rm --init --ipc=host `
    -e UTF_VISUAL_BASELINES=1 `
    -v "${sourcesPath}:/host/sources:ro" `
    -v "${screenshotsPath}:/work/sources/ReactApp/tests/e2e/__screenshots__" `
    -v "${npmCacheVolume}:/root/.npm" `
    $image `
    bash -c $containerScript

if ($LASTEXITCODE -ne 0) {
    throw "Visual baselines run failed with exit code $LASTEXITCODE."
}
if ($Update) {
    Write-Host 'Baselines written to sources/ReactApp/tests/e2e/__screenshots__. Review and commit them.' -ForegroundColor Green
} else {
    Write-Host 'All visual baselines match.' -ForegroundColor Green
}

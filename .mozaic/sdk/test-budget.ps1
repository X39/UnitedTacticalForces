# Exercise the real callback and subprocess wrapper, without an engine or build.
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'safety.ps1')
$tokens = $null
$parseErrors = $null
$syntax = [Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'prepare.ps1'), [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count) { throw 'Preparation source does not parse.' }
$budget = @($syntax.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq 'Assert-Budget' }, $true))
if ($budget.Count -ne 1) { throw 'Expected one real preparation budget function.' }
Invoke-Expression $budget[0].Extent.Text
$script:output = New-ExclusiveOutput (Join-Path ([IO.Path]::GetTempPath()) ('utf-budget-contract-' + [Guid]::NewGuid().ToString('N'))) ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')))
$testRoot = $script:output
$pwsh = (Get-Process -Id $PID).Path
$script:baseline = [pscustomobject]@{graphRootUsed=1GB}
$script:storageReads = 0
function Get-Storage {
    $script:storageReads += 1
    return $script:testStorage
}
function Invoke-BudgetCase([string]$Name, [string]$Expected, [DateTime]$PreparationStart, [double]$Seconds = 8) {
    $script:preparationStartedAt = $PreparationStart
    $prefix = Join-Path $testRoot $Name
    $caught = $null
    try {
        Invoke-DeadlineProcess -Executable $pwsh -Arguments @('-NoProfile','-Command','[Console]::WriteLine($PID); Start-Sleep -Seconds 4') -Deadline ([DateTime]::UtcNow.AddSeconds(12)) -LogPrefix $prefix -CallSeconds $Seconds -OnPoll { Assert-Budget }
    } catch { $caught = $_.Exception.Message }
    if ($Expected) {
        if (-not $caught -or -not $caught.Contains($Expected)) { throw "${Name}: expected fence '$Expected'; actual '$caught'." }
    } elseif ($caught) { throw "${Name}: unexpected failure '$caught'." }
    # The child writes its actual PID before sleeping. Every failure must finish
    # cleanup before returning; a Boolean/string/source-name assertion is not enough.
    $childText = [IO.File]::ReadAllText($prefix + '.stdout').Trim()
    if ($childText) {
        $childId = [int]$childText
        if (Get-Process -Id $childId -ErrorAction SilentlyContinue) { throw "${Name}: owned child remains after wrapper returned." }
    }
    Write-Output "PASS $Name"
}
try {
    [IO.File]::WriteAllText((Join-Path $testRoot 'owned-fixture'), 'bounded storage fixture')
    $script:testStorage = [pscustomobject]@{graphRootUsed=2GB;graphRootAllocated=128GB}
    Invoke-BudgetCase 'poll-with-real-wrapper-local-started-true' '' ([DateTime]::UtcNow)
    if ($script:storageReads -lt 1) { throw 'Real polling callback never executed.' }
    Invoke-BudgetCase 'preparation-deadline' '120-minute operator deadline' ([DateTime]::UtcNow.AddMinutes(-121))
    $script:testStorage = [pscustomobject]@{graphRootUsed=66GB;graphRootAllocated=128GB}
    Invoke-BudgetCase 'combined-storage-budget' '64 GiB incremental storage budget' ([DateTime]::UtcNow)
    $script:testStorage = [pscustomobject]@{graphRootUsed=2GB;graphRootAllocated=33GB}
    Invoke-BudgetCase 'vm-free-floor' 'free-space floor' ([DateTime]::UtcNow)
    $script:testStorage = [pscustomobject]@{graphRootUsed=2GB;graphRootAllocated=128GB}
    Invoke-BudgetCase 'subprocess-deadline' 'subprocess exceeded its deadline' ([DateTime]::UtcNow) 0.5
    Write-Output 'PASS: 5 actual PowerShell callback/time/storage/child-cleanup contracts; storage values only are mocked.'
} finally {
    $resolved = [IO.Path]::GetFullPath($testRoot)
    $temporary = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if (-not $resolved.StartsWith($temporary, [StringComparison]::OrdinalIgnoreCase) -or -not ([IO.Path]::GetFileName($resolved)).StartsWith('utf-budget-contract-')) { throw 'Refusing unexpected test cleanup path.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}

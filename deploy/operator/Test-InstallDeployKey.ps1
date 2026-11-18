<#
.SYNOPSIS
  Tests Install-DeployKey.ps1 against a stub ssh, under Windows PowerShell 5.1 and PowerShell 7.
  Run: powershell -NoProfile -File deploy\operator\Test-InstallDeployKey.ps1
#>
param([string[]]$Shells = @('powershell', 'pwsh'))
$ErrorActionPreference = 'Stop'
$script = Join-Path $PSScriptRoot 'Install-DeployKey.ps1'
$work = Join-Path ([IO.Path]::GetTempPath()) ('utf-install-test-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
$calls = Join-Path $work 'calls'
New-Item -ItemType Directory -Path $calls | Out-Null

Set-Content -Path (Join-Path $work 'ssh.cmd') -Encoding ASCII -Value '@powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0stub.ps1" %*'
Set-Content -Path (Join-Path $work 'stub.ps1') -Encoding UTF8 -Value @'
$command = $args[$args.Count - 1]
$memory = New-Object IO.MemoryStream
[Console]::OpenStandardInput().CopyTo($memory)
$bytes = $memory.ToArray()
$count = @(Get-ChildItem $env:STUB_DIR -Filter 'call-*.json').Count
$record = @{ argv = @($args); command = $command; stdin = (($bytes | ForEach-Object { $_.ToString('x2') }) -join '') }
Set-Content -Path (Join-Path $env:STUB_DIR ('call-{0:000}.json' -f $count)) -Value ($record | ConvertTo-Json -Compress) -Encoding UTF8
$identity = @($args) -contains '-i'
if ($identity) {
    if ($env:STUB_UNFORCED) { 'uid=1000(utf-deploy) gid=1000'; exit 0 }
    [Console]::Error.WriteLine('Error: payload does not match any of the supported image formats')
    exit 125
}
if ($command -eq 'id -u') { if ($env:STUB_UID) { $env:STUB_UID } else { '0' }; exit 0 }
if ($command -like "printf '%s|%s*") { '/usr/bin/podman|/home/utf-deploy'; exit 0 }
if ($command -like 'ssh-keygen -lf*') { '256 SHA256:abc utf-deploy-ci (ED25519)'; exit 0 }
if ($command -like 'grep -cF*') { if ($env:STUB_PRESENT) { $env:STUB_PRESENT } else { '0' }; exit 0 }
exit 0
'@

$public = Join-Path $work 'utf-deploy.pub'
$privateKey = Join-Path $work 'utf_deploy_ci'
$knownHosts = Join-Path $work 'known_hosts'
$body = 'AAAAC3NzaC1lZDI1NTE5AAAAIBYr47ii5I9QN+uGEpDEmRGZkdCWUMHAUK6vSwTtsBqd'
[IO.File]::WriteAllText($public, "ssh-ed25519 $body utf-deploy-ci`n")
[IO.File]::WriteAllText($privateKey, "fake private key`n")
[IO.File]::WriteAllText($knownHosts, "2a01:4f8:c0c:e2e::1 ssh-ed25519 AAAA`n")

$failures = 0
function Check([bool]$Condition, [string]$Label) {
    if ($Condition) { Write-Host "  ok   $Label" } else { Write-Host "  FAIL $Label" -ForegroundColor Red; $script:failures++ }
}
function Get-Calls { @(Get-ChildItem $calls -Filter 'call-*.json' | Sort-Object Name | ForEach-Object { Get-Content $_.FullName -Raw | ConvertFrom-Json }) }
function Find-Call([string]$Pattern) { @(@(Get-Calls) | ForEach-Object { $_ } | Where-Object { $_.command -like $Pattern }) }

function Invoke-Script([string]$Shell, [string[]]$Answers, [hashtable]$Environment = @{}, [string[]]$Arguments = @()) {
    Get-ChildItem $calls -Filter 'call-*.json' | Remove-Item
    $answerFile = Join-Path $work 'answers.txt'
    Set-Content -Path $answerFile -Value $Answers -Encoding UTF8
    $settings = @{ STUB_DIR = $calls; UTF_SSH = (Join-Path $work 'ssh.cmd'); UTF_OPERATOR_SSH = 'root@2a01:4f8:c0c:e2e::1';
        UTF_VARS_TEST_ANSWERS = $answerFile; STUB_UID = ''; STUB_PRESENT = ''; STUB_UNFORCED = '' }
    foreach ($pair in $Environment.GetEnumerator()) { $settings[$pair.Key] = $pair.Value }
    $saved = @{}
    foreach ($pair in $settings.GetEnumerator()) {
        $saved[$pair.Key] = [Environment]::GetEnvironmentVariable($pair.Key)
        [Environment]::SetEnvironmentVariable($pair.Key, $(if ($pair.Value) { [string]$pair.Value } else { $null }))
    }
    try {
        $output = & $Shell -NoProfile -ExecutionPolicy Bypass -File $script -PublicKeyFile $public -PrivateKey $privateKey -KnownHosts $knownHosts @Arguments 2>&1 | Out-String
        return [pscustomobject]@{ Code = $LASTEXITCODE; Output = $output }
    }
    finally { foreach ($pair in $saved.GetEnumerator()) { [Environment]::SetEnvironmentVariable($pair.Key, $pair.Value) } }
}

$expectedLine = "restrict,command=`"/usr/bin/podman load`" ssh-ed25519 $body utf-deploy-ci`n"
$expectedHex = ([Text.Encoding]::UTF8.GetBytes($expectedLine) | ForEach-Object { $_.ToString('x2') }) -join ''

try {
    foreach ($shell in $Shells) {
        if (-not (Get-Command $shell -ErrorAction SilentlyContinue)) { Write-Host "== $shell not installed, skipped"; continue }
        Write-Host "== $shell"

        $r = Invoke-Script $shell @() @{} @('-CheckOnly')
        Check ($r.Code -eq 0 -and @(Find-Call 'install -d*').Count -eq 0 -and $r.Output -like '*Check only*' -and $r.Output -like '*SHA256:abc*') '-CheckOnly lists existing keys and changes nothing'

        $r = Invoke-Script $shell @('y')
        $install = @(Find-Call 'install -d*')
        if ($r.Code -ne 0 -or $install.Count -ne 1) { Write-Host "    exit $($r.Code), install calls $($install.Count), output: $($r.Output)" }
        Check ($r.Code -eq 0 -and $install.Count -eq 1) 'confirming appends the key'
        Check ($install.Count -eq 1 -and $install[0].stdin -ceq $expectedHex) 'the appended line is exactly restrict,command="<podman> load" <key>, byte-exact and without a BOM'
        Check ($install.Count -eq 1 -and $install[0].command -like '*chmod 600 /home/utf-deploy/.ssh/authorized_keys*' -and $install[0].command -like '*install -d -m 700 -o utf-deploy*') 'directory and file get private modes and the deploy user as owner'
        Check ($r.Output -like '*Proof passed*' -and $r.Output -like '*supported image formats*') 'the post-install proof ran and passed'

        $r = Invoke-Script $shell @('y') @{ STUB_PRESENT = '1' }
        Check ($r.Code -eq 0 -and @(Find-Call 'install -d*').Count -eq 0 -and $r.Output -like '*already authorized*') 'an already authorized key is not added twice'

        $r = Invoke-Script $shell @('n')
        Check ($r.Code -eq 0 -and @(Find-Call 'install -d*').Count -eq 0) 'declining changes nothing'

        $r = Invoke-Script $shell @('y') @{ STUB_UID = '1000' }
        Check ($r.Code -eq 1 -and @(Find-Call 'install -d*').Count -eq 0) 'a non-root login stops before changing anything'

        $r = Invoke-Script $shell @('y') @{ STUB_UNFORCED = '1' }
        Check ($r.Code -eq 1 -and $r.Output -like '*PROOF FAILED*') 'a key that ran its own command is reported as a failed proof'

        $r = Invoke-Script $shell @('y') @{ UTF_OPERATOR_SSH = '' }
        Check ($r.Code -eq 1 -and @(Get-Calls).Count -eq 0) 'a missing target stops before connecting'

        $all = @(Get-Calls)
        $r = Invoke-Script $shell @('y')
        $all = @(Get-Calls)
        $pinned = @($all | Where-Object { ($_.argv -join ' ') -like '*StrictHostKeyChecking=yes*' -and ($_.argv -join ' ') -like '*known_hosts*' -and ($_.argv -join ' ') -like '*BatchMode=yes*' })
        Check ($pinned.Count -eq $all.Count -and $all.Count -gt 0) 'every connection pins the host key from known_hosts and runs in batch mode'
        $proofCall = @($all | Where-Object { $_.argv -contains '-i' })
        Check ($proofCall.Count -eq 1 -and ($proofCall[0].argv -join ' ') -like '*IdentitiesOnly=yes*' -and ($proofCall[0].argv -join ' ') -like '*utf-deploy@2a01:4f8:c0c:e2e::1*') 'the proof logs in as the deploy user with only that key'
    }
}
finally { Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue }
if ($failures -gt 0) { Write-Host "`n$failures check(s) FAILED" -ForegroundColor Red; exit 1 }
Write-Host "`nAll checks passed."

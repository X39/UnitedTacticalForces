<#
.SYNOPSIS
  Tests Set-PipelineVariables.ps1 against a stub ssh, under Windows PowerShell 5.1 and PowerShell 7.
  Run: powershell -NoProfile -File deploy\operator\Test-SetPipelineVariables.ps1
#>
param([string[]]$Shells = @('powershell', 'pwsh'))
$ErrorActionPreference = 'Stop'
$script = Join-Path $PSScriptRoot 'Set-PipelineVariables.ps1'
$work = Join-Path ([IO.Path]::GetTempPath()) ('utf-vars-test-' + [guid]::NewGuid().ToString('N'))
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
if ($command -eq 'id -u') { if ($env:STUB_UID) { $env:STUB_UID } else { '0' }; exit 0 }
if ($command -eq 'wc -c') { $bytes.Length + [int]$env:STUB_EXTRA; exit 0 }
if ($command -eq 'id -u utf-deploy') { '1001'; exit 0 }
if ($command -like '*podman ps -a --format*') {
    if ($command -like 'runuser*') { 'utf-api localhost/utf-api@sha256:1'; 'utf-database docker.io/library/postgres:18'; 'unrelated-app docker.io/library/nginx' }
    else { 'mozaic-db docker.io/library/postgres:18'; 'mozaic-server localhost/mozaic-runtime@sha256:9' }
    exit 0
}
if ($command -like '*podman inspect utf-api *') { 'STEAM_API_KEY=steamkey-from-container'; 'Discord__Bot__BotToken=bottoken-from-container-1'; 'ConnectionStrings__database=Host=database;Port=5432;Database=utf;Username=utf;Password=pgpass-conn-12345'; exit 0 }
if ($command -like '*podman inspect utf-database *') { 'POSTGRES_PASSWORD=pgpass-db-container'; exit 0 }
if ($command -like '*podman inspect mozaic*') { 'POSTGRES_PASSWORD=mozaic-secret-never'; exit 0 }
if ($command -like 'find /etc*') {
    '/etc/utf/utf.env:STEAM_API_KEY=steamkey-from-file'
    '/etc/utf/utf.env:HETZNER_TOKEN="hetznertoken-from-file"'
    '/home/utf-deploy/.config/containers/systemd/utf-api.container:Environment=TEAMSPEAK_PASSWORD=ts-pass-from-unit'
    '/etc/utf/utf.env:GHCR_PULL_TOKEN=oldghcrtoken-not-mapped'
    '/etc/utf/utf.env:Hosting__WorkerProvisioning__RegistryToken=oldghcrtoken2-not-mapped'
    '/etc/utf/utf.env:DISCORD_CLIENT_ID=short'
    exit 0
}
if ($command -like 'podman network exists*') { exit [int]($env:STUB_NET) }
if ($command -like 'podman network ls*') { 'bridge'; 'other-net'; exit 0 }
if ($command -like 'podman ps*') { $env:STUB_IMAGES; exit 0 }
if ($command -like 'test -f*') { exit [int]($env:STUB_ENVFILES) }
if ($command -like 'test -r*') { exit [int]($env:STUB_CA) }
if ($command -like '*mktemp -d*') { '/var/tmp/mozaic-varset.abc123'; exit 0 }
if ($command -like '*pipeline-variable-set*') { if ([int]$env:STUB_EXIT -ne 0) { [Console]::Error.WriteLine('Error: stub tool refused the request') }; exit [int]($env:STUB_EXIT) }
exit 0
'@

$key = Join-Path $work 'key'
# The script only checks the OpenSSH header; a fake key keeps the test independent of ssh-keygen quoting.
[IO.File]::WriteAllText($key, "-----BEGIN OPENSSH PRIVATE KEY-----`nb3BlbnNzaC1rZXktdjEAAAAABG5vbmUAAAAEbm9uZQAAAAAAAAABAAAAMwAAAAtzc2gtZWQyNTUxOQ==`n-----END OPENSSH PRIVATE KEY-----`n")
[IO.File]::WriteAllText("$key.pub", "ssh-ed25519 AAAA test`n")
$order = 'UTF_DEPLOY_SSH_KEY', 'POSTGRES_PASSWORD', 'STEAM_API_KEY', 'DISCORD_CLIENT_ID', 'DISCORD_CLIENT_SECRET',
    'DISCORD_APPLICATION_ID', 'DISCORD_PUBLIC_KEY', 'DISCORD_BOT_TOKEN', 'TEAMSPEAK_PASSWORD',
    'TEAMSPEAK_CHANNEL_PASSWORD', 'HETZNER_TOKEN', 'WORKER_REGISTRY_TOKEN'

$failures = 0
function Check([bool]$Condition, [string]$Label) {
    if ($Condition) { Write-Host "  ok   $Label" } else { Write-Host "  FAIL $Label" -ForegroundColor Red; $script:failures++ }
}
# The comma keeps a single result an array, so .Count is always the number of calls.
function Get-Calls { , @(Get-ChildItem $calls -Filter 'call-*.json' | Sort-Object Name | ForEach-Object { Get-Content $_.FullName -Raw | ConvertFrom-Json }) }
function Get-Writes { , @(@(Get-Calls) | ForEach-Object { $_ } | Where-Object { $_.command -like '*pipeline-variable-set*' }) }
function Get-Flag([string]$Command, [string]$Name) { $words = $Command -split ' '; $words[[array]::IndexOf($words, $Name) + 1] }
function ConvertFrom-Hex([string]$Hex) { if (-not $Hex) { return ,([byte[]]@()) }; ,([byte[]](0..($Hex.Length / 2 - 1) | ForEach-Object { [Convert]::ToByte($Hex.Substring(2 * $_, 2), 16) })) }
function Get-Text([string]$Hex) { [Text.Encoding]::UTF8.GetString((ConvertFrom-Hex $Hex)) }

function Invoke-Script([string]$Shell, [string[]]$Answers, [hashtable]$Environment = @{}, [string[]]$Arguments = @()) {
    Get-ChildItem $calls -Filter 'call-*.json' | Remove-Item
    $answerFile = Join-Path $work 'answers.txt'
    Set-Content -Path $answerFile -Value $Answers -Encoding UTF8
    $saved = @{}
    $settings = @{ STUB_DIR = $calls; UTF_SSH = (Join-Path $work 'ssh.cmd'); UTF_OPERATOR_SSH = 'root@amsel';
        MOZAIC_ADMIN_CMD = 'mozaic-admin'; UTF_VARS_TEST_ANSWERS = $answerFile; STUB_UID = ''; STUB_EXIT = '0'; STUB_CA = '1'; STUB_EXTRA = ''; STUB_ENVFILES = ''; STUB_NET = '';
        STUB_IMAGES = ''; MOZAIC_IMAGE = ''; MOZAIC_ADMIN_BIN = '' }
    foreach ($pair in $Environment.GetEnumerator()) { $settings[$pair.Key] = $pair.Value }
    foreach ($pair in $settings.GetEnumerator()) {
        $saved[$pair.Key] = [Environment]::GetEnvironmentVariable($pair.Key)
        [Environment]::SetEnvironmentVariable($pair.Key, $(if ($pair.Value) { [string]$pair.Value } else { $null }))
    }
    try {
        $output = & $Shell -NoProfile -ExecutionPolicy Bypass -File $script @Arguments 2>&1 | Out-String
        return [pscustomobject]@{ Code = $LASTEXITCODE; Output = $output }
    }
    finally { foreach ($pair in $saved.GetEnumerator()) { [Environment]::SetEnvironmentVariable($pair.Key, $pair.Value) } }
}
function New-Answers([hashtable]$Given) { @($order | ForEach-Object { if ($Given.ContainsKey($_)) { $Given[$_] } else { '' } }) }

try {
    foreach ($shell in $Shells) {
        if (-not (Get-Command $shell -ErrorAction SilentlyContinue)) { Write-Host "== $shell not installed, skipped"; continue }
        Write-Host "== $shell"
        $secret = 'pgpassword-abc123'; $token = 'mzoci_client_abcdefghij'

        $r = Invoke-Script $shell @() @{} @('-List')
        Check ($r.Code -eq 0 -and $order.Count -eq @($order | Where-Object { $r.Output -match $_ }).Count -and (Get-Calls).Count -eq 0) 'list names every variable and never connects'

        $answers = (New-Answers @{ UTF_DEPLOY_SSH_KEY = $key; POSTGRES_PASSWORD = $secret; WORKER_REGISTRY_TOKEN = $token }) + 'y'
        $r = Invoke-Script $shell $answers
        $writes = Get-Writes
        Check ($r.Code -eq 0 -and $writes.Count -eq 3) 'three variables are written'
        $byName = @{}; foreach ($w in $writes) { $byName[(Get-Flag $w.command '--name')] = $w }
        # Compare raw bytes as hex with an ordinal operator: culture-aware string comparison ignores a leading BOM.
        $secretHex = ([Text.Encoding]::UTF8.GetBytes($secret) | ForEach-Object { $_.ToString('x2') }) -join ''
        $tokenHex = ([Text.Encoding]::UTF8.GetBytes($token) | ForEach-Object { $_.ToString('x2') }) -join ''
        Check ($byName['POSTGRES_PASSWORD'].stdin -ceq $secretHex) 'value arrives byte-exact on stdin (no BOM, no trailing newline)'
        Check ($byName['WORKER_REGISTRY_TOKEN'].stdin -ceq $tokenHex) 'second value byte-exact'
        Check (@($writes | Where-Object { $_.stdin.StartsWith('efbbbf') }).Count -eq 0 -and @(Get-Calls | Where-Object { $_.command -eq 'wc -c' }).Count -ge 1) 'no byte-order mark, and the byte-exactness probe ran'
        $sentKey = Get-Text $byName['UTF_DEPLOY_SSH_KEY'].stdin
        $keyText = [Text.Encoding]::ASCII.GetString([Convert]::FromBase64String($sentKey))
        Check ($keyText.StartsWith('-----BEGIN OPENSSH PRIVATE KEY-----')) 'key file is sent as base64 of the PEM'
        $leak = @($writes | Where-Object { $_.command -like "*$secret*" -or $_.command -like "*$token*" -or ($_.argv -join ' ') -like "*$secret*" })
        Check ($leak.Count -eq 0 -and $r.Output -notlike "*$secret*" -and $r.Output -notlike "*$token*") 'no value in any command line or in the output'
        $restricted = @($writes | Where-Object { (Get-Flag $_.command '--branch') -eq 'refs/heads/master' -and (Get-Flag $_.command '--pipeline') -eq '.mozaic/release.yml' -and $_.command -like '*--value-stdin*' -and (Get-Flag $_.command '--repository-id') -eq '01a166ad-93f3-7fa2-a4f0-e57014752726' })
        Check ($restricted.Count -eq 3) 'every write carries the branch, pipeline and repository restrictions'
        Check (@($writes | ForEach-Object { Get-Flag $_.command '--operation-id' } | Sort-Object -Unique).Count -eq 3) 'a fresh operation id per value'
        $everyCall = @(Get-Calls) | ForEach-Object { $_ }
        Check (@($everyCall | Where-Object { ($_.argv -join ' ') -like '*StrictHostKeyChecking=yes*' -and ($_.argv -join ' ') -like '*UserKnownHostsFile=*known_hosts*' -and ($_.argv -join ' ') -like '*BatchMode=yes*' }).Count -eq $everyCall.Count) 'every connection pins the host key from deploy/known_hosts'

        $r = Invoke-Script $shell ((New-Answers @{ POSTGRES_PASSWORD = $secret }) + 'n')
        Check ($r.Code -eq 0 -and (Get-Calls).Count -eq 0) 'cancelling writes nothing and never connects'
        $r = Invoke-Script $shell ((New-Answers @{ POSTGRES_PASSWORD = 'true' }) + 'y')
        Check ($r.Code -eq 1 -and (Get-Calls).Count -eq 0) 'short values are refused before connecting'
        $r = Invoke-Script $shell ((New-Answers @{ UTF_DEPLOY_SSH_KEY = "$key.pub" }) + 'y')
        Check ($r.Code -eq 1 -and (Get-Calls).Count -eq 0) 'a non-key file is refused before connecting'
        $r = Invoke-Script $shell ((New-Answers @{ POSTGRES_PASSWORD = $secret }) + 'y') @{ STUB_EXIT = '1' }
        Check ($r.Code -eq 1 -and $r.Output -like '*FAILED: POSTGRES_PASSWORD*' -and $r.Output -like '*host said: Error: stub tool refused*' -and $r.Output -notlike "*$secret*") 'a failing tool is reported by name'
        $r = Invoke-Script $shell ((New-Answers @{ POSTGRES_PASSWORD = $secret }) + 'y') @{ STUB_UID = '1000' }
        Check ($r.Code -eq 1 -and (Get-Writes).Count -eq 0) 'a non-root login stops before writing'
        $r = Invoke-Script $shell ((New-Answers @{ POSTGRES_PASSWORD = $secret }) + 'y') @{ STUB_EXTRA = '3' }
        Check ($r.Code -eq 1 -and (Get-Writes).Count -eq 0 -and $r.Output -like '*does not pass raw bytes*') 'a connection that alters bytes is refused before any secret is sent'
        $r = Invoke-Script $shell ((New-Answers @{ POSTGRES_PASSWORD = $secret }) + 'y') @{ UTF_OPERATOR_SSH = '' }
        Check ($r.Code -eq 1 -and (Get-Calls).Count -eq 0) 'a missing target stops before connecting'
        $r = Invoke-Script $shell @('hetznertoken-xyz987', 'y') @{} @('-Only', 'HETZNER_TOKEN')
        if ($r.Code -ne 0 -or (Get-Writes).Count -ne 1) { Write-Host "    exit $($r.Code), writes $((Get-Writes).Count), output: $($r.Output)" }
        Check ($r.Code -eq 0 -and (Get-Writes).Count -eq 1) '-Only prompts for one variable'

        # -FromHost: values the app already has on the host are imported in memory and never printed.
        $r = Invoke-Script $shell (@($key) + @('') * 6 + 'y') @{} @('-FromHost')
        $writes = Get-Writes
        $byName = @{}; foreach ($w in $writes) { $byName[(Get-Flag $w.command '--name')] = $w }
        if ($writes.Count -ne 6) { Write-Host "    exit $($r.Code); output: $($r.Output)"; @(Get-Calls) | ForEach-Object { $_ } | ForEach-Object { Write-Host ('    call: ' + $_.command.Substring(0, [Math]::Min(110, $_.command.Length))) } }
        Check ($r.Code -eq 0 -and $writes.Count -eq 6 -and (($byName.Keys | Sort-Object) -join ',') -ceq 'DISCORD_BOT_TOKEN,HETZNER_TOKEN,POSTGRES_PASSWORD,STEAM_API_KEY,TEAMSPEAK_PASSWORD,UTF_DEPLOY_SSH_KEY') '-FromHost writes the key plus the five values found on the host'
        $expected = @{ STEAM_API_KEY = 'steamkey-from-container'; HETZNER_TOKEN = 'hetznertoken-from-file'; TEAMSPEAK_PASSWORD = 'ts-pass-from-unit'; POSTGRES_PASSWORD = 'pgpass-conn-12345'; DISCORD_BOT_TOKEN = 'bottoken-from-container-1' }
        $exact = @($expected.Keys | Where-Object { $byName[$_] -and $byName[$_].stdin -ceq (([Text.Encoding]::UTF8.GetBytes($expected[$_]) | ForEach-Object { $_.ToString('x2') }) -join '') })
        Check ($exact.Count -eq 5) 'imported values are exact: containers win over files, quotes are stripped, the password is parsed from the connection string'
        $oldHex = ([Text.Encoding]::UTF8.GetBytes('oldghcr') | ForEach-Object { $_.ToString('x2') }) -join ''
        Check (@($writes | Where-Object { $_.command -like '*WORKER_REGISTRY_TOKEN*' -or $_.stdin -like "*$oldHex*" }).Count -eq 0) 'old GHCR tokens are never imported as the worker registry key'
        Check (@(@(Get-Calls) | Where-Object { $_.command -like '*podman inspect mozaic*' -or $_.command -like '*unrelated*' }).Count -eq 0) "Mozaic's own and unrelated containers are never read"
        $inspect = @(@(Get-Calls) | Where-Object { $_.command -like '*podman inspect utf-api*' })
        $search = @(@(Get-Calls) | Where-Object { $_.command -like 'find /etc*' })
        Check ($inspect.Count -eq 1 -and $inspect[0].command -like "*| grep -E '^(*Steam__ApiKey*)='" -and $search.Count -eq 1 -and $search[0].command -like '*grep -viE ''mozaic*' -and $search[0].command -like '*xargs -r grep -HsE*') 'only lines for the known names are selected on the host, and mozaic paths are excluded'
        # Full values as needles: a short fragment can collide with a key name in the search pattern.
        $needles = @($expected.Values) + @('steamkey-from-file', 'pgpass-db-container', 'oldghcrtoken-not-mapped')
        $inOutput = @($needles | Where-Object { $r.Output -clike "*$_*" })
        $inCommands = @($needles | Where-Object { $needle = $_; @(@(Get-Calls) | Where-Object { $_.command -clike "*$needle*" }).Count -gt 0 })
        Check ($inOutput.Count -eq 0 -and $inCommands.Count -eq 0) 'no imported value appears in the output or in any command line'
        Check ($r.Output -like '*found STEAM_API_KEY*container utf-api (utf-deploy)*' -and $r.Output -like '*note: POSTGRES_PASSWORD has different values*' -and $r.Output -like '*note: STEAM_API_KEY has different values*') 'sources and conflicts are shown by name'
        Check ($r.Output -like '*DISCORD_CLIENT_ID was found but is shorter*') 'a found value that is too short is left for the user instead of being stored'

        $binary = Join-Path $work 'mozaic-admin'
        [IO.File]::WriteAllBytes($binary, [byte[]](0x7f, 0x45, 0x4c, 0x46, 1, 2, 3))
        $r = Invoke-Script $shell ((New-Answers @{ POSTGRES_PASSWORD = $secret }) + 'y') @{ MOZAIC_ADMIN_CMD = ''; MOZAIC_ADMIN_BIN = $binary; MOZAIC_IMAGE = 'localhost/mozaic-runtime@sha256:abc' }
        $all = Get-Calls
        $upload = $all | Where-Object { $_.command -like 'cat >*' } | Select-Object -First 1
        $command = (Get-Writes)[0].command
        Check ($r.Code -eq 0 -and $upload -and (Get-Text $upload.stdin).Length -gt 0 -and $upload.stdin -eq '7f454c46010203') 'the binary is uploaded byte-exact for the run'
        Check ($command -like '*--volume /var/tmp/mozaic-varset.abc123/mozaic-admin:/opt/mozaic-varset/mozaic-admin:ro*' -and $command -like '*--entrypoint /opt/mozaic-varset/mozaic-admin localhost/mozaic-runtime@sha256:abc*') 'it is mounted and used as the entrypoint'
        Check ($command -like '*--cap-drop all*' -and $command -like '*--read-only*' -and $command -like '*no-new-privileges*') 'the container is locked down'
        Check ($all[$all.Count - 1].command -like 'rm -f /var/tmp/mozaic-varset.abc123/mozaic-admin*') 'the upload is removed again'

        $r = Invoke-Script $shell ((New-Answers @{ POSTGRES_PASSWORD = $secret }) + 'y') @{ MOZAIC_ADMIN_CMD = ''; STUB_CA = '0'; STUB_IMAGES = "localhost/mozaic-runtime@sha256:abc`nregistry/other:1" }
        $command = (Get-Writes)[0].command
        Check ($r.Code -eq 0 -and $command -like '*--entrypoint /opt/mozaic/bin/mozaic-admin localhost/mozaic-runtime@sha256:abc*' -and $command -like '*postgres-tls/ca.crt*') 'the runtime image is detected and the CA is mounted'
        $r = Invoke-Script $shell ((New-Answers @{ POSTGRES_PASSWORD = $secret }) + 'y') @{ MOZAIC_ADMIN_CMD = ''; STUB_IMAGES = "localhost/mozaic-runtime@sha256:aaa`nlocalhost/mozaic-runtime@sha256:bbb" }
        Check ($r.Code -eq 1 -and (Get-Writes).Count -eq 0) 'an ambiguous runtime image is refused'
        $r = Invoke-Script $shell ((New-Answers @{ POSTGRES_PASSWORD = $secret }) + 'y') @{ MOZAIC_ADMIN_CMD = ''; STUB_ENVFILES = '1' }
        Check ($r.Code -eq 1 -and (Get-Writes).Count -eq 0 -and $r.Output -like '*does not look like the Mozaic host*') 'a host without the Mozaic env files is refused before writing'
        $r = Invoke-Script $shell ((New-Answers @{ POSTGRES_PASSWORD = $secret }) + 'y') @{ MOZAIC_ADMIN_CMD = ''; STUB_NET = '1' }
        Check ($r.Code -eq 1 -and (Get-Writes).Count -eq 0 -and $r.Output -like '*podman network ''mozaic'' does not exist*' -and $r.Output -like '*other-net*') 'a missing podman network is named, with the networks that exist, before writing'
    }
}
finally { Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue }
if ($failures -gt 0) { Write-Host "`n$failures check(s) FAILED" -ForegroundColor Red; exit 1 }
Write-Host "`nAll checks passed."

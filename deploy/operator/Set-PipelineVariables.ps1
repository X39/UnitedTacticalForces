<#
.SYNOPSIS
  Enter all UTF pipeline variables once, from this computer's PowerShell. Nothing is run by hand on the server.

.DESCRIPTION
  Prompts here (input hidden), then reaches the Mozaic host over SSH and writes each value through
  `mozaic-admin pipeline-variable-set`, which can only create or override a secret variable and can never read
  one back. Every variable is a secret, trusted-only and restricted to the trusted branch and the release
  pipeline. A value travels only as raw bytes on the standard input of the SSH connection: never in an argument,
  a file, a process list or the terminal. Press Enter on a prompt to skip it. Running again overrides values.

.PARAMETER Target
  SSH login that is root on the Mozaic host, for example root@2a01:4f8:c0c:e2e::1.

.PARAMETER AdminBin
  Path of a Linux mozaic-admin binary that has pipeline-variable-set. It is uploaded to a private temporary
  directory on the host for this run and removed afterwards. Without it the installed runtime image's own
  /opt/mozaic/bin/mozaic-admin is used.

.PARAMETER Image
  Installed runtime image; detected from the running containers when omitted.

.PARAMETER Only
  Prompt for one variable only.

.PARAMETER List
  Show the variable names and purposes, then exit.
#>
param(
    [string]$Target = $env:UTF_OPERATOR_SSH,
    [string]$AdminBin = $env:MOZAIC_ADMIN_BIN,
    [string]$Image = $env:MOZAIC_IMAGE,
    [string]$Only,
    [switch]$List,
    [string]$KnownHosts = (Join-Path $PSScriptRoot '..\known_hosts'),
    [switch]$FromHost,
    [string]$HostUser = 'utf-deploy'
)
$ErrorActionPreference = 'Stop'

$Org = '01a16590-011f-7d72-91fb-61d57c148fe7'
$Project = '01a166ad-9024-7b30-a9bc-cd03ac37f2e8'
$Repository = '01a166ad-93f3-7fa2-a4f0-e57014752726'
$RunAs = '01a1658e-c4ec-74a1-ac91-89bf2f8ce666'
$Branch = 'refs/heads/master'
$Pipeline = '.mozaic/release.yml'
$RemoteTmp = '/var/tmp'
$MinimumLength = 8
$Ssh = if ($env:UTF_SSH) { $env:UTF_SSH } else { Join-Path $env:SystemRoot 'System32\OpenSSH\ssh.exe' }
$AdminCmd = $env:MOZAIC_ADMIN_CMD   # tests and native installs: run this command on the host instead of podman

$Variables = @(
    @{ Name = 'UTF_DEPLOY_SSH_KEY'; Kind = 'key'; Text = 'SSH key the release pipeline logs in to Amsel with (path of the private key file on this computer, or its single-line base64)' }
    @{ Name = 'POSTGRES_PASSWORD'; Kind = 'secret'; Text = 'PostgreSQL password of the UTF database (not used by the release pipeline today; skip it unless you want it stored for the later start-up step)' }
    @{ Name = 'STEAM_API_KEY'; Kind = 'secret'; Text = 'Steam Web API key' }
    @{ Name = 'DISCORD_CLIENT_ID'; Kind = 'secret'; Text = 'Discord OAuth client id' }
    @{ Name = 'DISCORD_CLIENT_SECRET'; Kind = 'secret'; Text = 'Discord OAuth client secret' }
    @{ Name = 'DISCORD_APPLICATION_ID'; Kind = 'secret'; Text = 'Discord bot application id' }
    @{ Name = 'DISCORD_PUBLIC_KEY'; Kind = 'secret'; Text = 'Discord bot public key' }
    @{ Name = 'DISCORD_BOT_TOKEN'; Kind = 'secret'; Text = 'Discord bot token' }
    @{ Name = 'TEAMSPEAK_PASSWORD'; Kind = 'secret'; Text = 'TeamSpeak server password' }
    @{ Name = 'TEAMSPEAK_CHANNEL_PASSWORD'; Kind = 'secret'; Text = 'TeamSpeak channel password' }
    @{ Name = 'HETZNER_TOKEN'; Kind = 'secret'; Text = 'Hetzner Cloud API token used to provision game servers' }
    @{ Name = 'WORKER_REGISTRY_TOKEN'; Kind = 'secret'; Text = 'Read-only registry key that lets game-server VMs pull the worker image' }
)

function Stop-Script([string]$Message) {
    Write-Host "Set-PipelineVariables: $Message" -ForegroundColor Red
    exit 1
}

# Test seam: UTF_VARS_TEST_ANSWERS names a file with one answer per prompt, used instead of the keyboard.
$script:TestAnswers = $null
if ($env:UTF_VARS_TEST_ANSWERS) {
    $script:TestAnswers = New-Object System.Collections.Generic.Queue[string] (, [string[]][IO.File]::ReadAllLines($env:UTF_VARS_TEST_ANSWERS))
}

function Read-Answer([string]$Prompt, [switch]$Hidden) {
    if ($null -ne $script:TestAnswers) {
        if ($script:TestAnswers.Count -eq 0) { return '' }
        return $script:TestAnswers.Dequeue()
    }
    if (-not $Hidden) { return (Read-Host $Prompt) }
    $secure = Read-Host $Prompt -AsSecureString
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
}

function Read-KeyValue {
    $answer = Read-Answer '  Path of the key file, or paste its base64 (Enter to skip)' -Hidden
    if (-not $answer) { return '' }
    if (Test-Path -LiteralPath $answer -PathType Leaf) {
        $answer = [Convert]::ToBase64String([IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $answer).Path))
    }
    try { $decoded = [Text.Encoding]::ASCII.GetString([Convert]::FromBase64String($answer)) }
    catch { Stop-Script 'that is neither a readable key file nor valid base64' }
    if (-not $decoded.StartsWith('-----BEGIN OPENSSH PRIVATE KEY-----')) { Stop-Script 'that does not decode to an OpenSSH private key' }
    return $answer
}

# Runs one command on the host over SSH. Standard input is sent as raw bytes. Returns exit code and output.
function Invoke-Remote([string]$Command, [byte[]]$Stdin) {
    $info = New-Object System.Diagnostics.ProcessStartInfo
    $info.FileName = $Ssh
    # The host key is pinned from the committed deploy/known_hosts: no trust-on-first-use, no prompt.
    $pinned = (Resolve-Path -LiteralPath $KnownHosts).Path.Replace('\', '/')
    $quoted = $Command.Replace('"', '\"')
    $info.Arguments = '-T -o BatchMode=yes -o StrictHostKeyChecking=yes -o "UserKnownHostsFile=' + $pinned + '" -o GlobalKnownHostsFile=NUL ' + $Target + ' "' + $quoted + '"'
    $info.UseShellExecute = $false
    $info.RedirectStandardInput = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    # Windows PowerShell 5.1 (.NET Framework) builds the stdin writer from the console code page and, for UTF-8,
    # writes a byte-order mark the moment the process starts. That would corrupt every secret, so start the
    # process under a code page without a preamble and restore the console afterwards.
    $previousEncoding = $null
    try { $previousEncoding = [Console]::InputEncoding; [Console]::InputEncoding = [Text.Encoding]::GetEncoding(1252) } catch { $previousEncoding = $null }
    try { $process = [System.Diagnostics.Process]::Start($info) }
    finally { if ($previousEncoding) { try { [Console]::InputEncoding = $previousEncoding } catch { } } }
    $outputTask = $process.StandardOutput.ReadToEndAsync()
    $errorTask = $process.StandardError.ReadToEndAsync()
    if ($Stdin -and $Stdin.Length -gt 0) { $process.StandardInput.BaseStream.Write($Stdin, 0, $Stdin.Length) }
    $process.StandardInput.Close()
    $process.WaitForExit()
    return [pscustomobject]@{ Code = $process.ExitCode; Output = $outputTask.Result.Trim(); Error = $errorTask.Result.Trim() }
}

$script:RemoteDir = ''
$script:CaMount = ''
$script:Entrypoint = '/opt/mozaic/bin/mozaic-admin'

function Remove-RemoteUpload {
    if ($script:RemoteDir) {
        $result = Invoke-Remote "rm -f $($script:RemoteDir)/mozaic-admin; rmdir $($script:RemoteDir)"
        if ($result.Code -ne 0) { Write-Warning "could not remove $($script:RemoteDir) on the host; remove it by hand" }
        $script:RemoteDir = ''
    }
}

function Initialize-Remote([switch]$ReadOnly) {
    if (-not $Target) { Stop-Script 'give -Target (the ssh login that is root on the Mozaic host), for example root@2a01:4f8:c0c:e2e::1' }
    $id = Invoke-Remote 'id -u'
    if ($id.Code -ne 0) { Stop-Script "could not log in to the host: $($id.Error)" }
    if ($id.Output -ne '0') { Stop-Script 'that login is not root on the host' }
    # Byte-exactness probe: three bytes in must be exactly three bytes out, or no secret may be sent
    # (a byte-order mark or a newline added in transit would silently change every value).
    $probe = Invoke-Remote 'wc -c' ([Text.Encoding]::ASCII.GetBytes('abc'))
    if ($probe.Code -ne 0 -or $probe.Output -ne '3') { Stop-Script "the connection does not pass raw bytes unchanged (sent 3, host counted '$($probe.Output)'); nothing was written" }
    if ($ReadOnly -or $AdminCmd) { return }
    if ((Invoke-Remote 'test -f /etc/mozaic/app.env && test -f /etc/mozaic/migrator.env').Code -ne 0) {
        Stop-Script 'this host does not look like the Mozaic host: /etc/mozaic/app.env and /etc/mozaic/migrator.env are missing; nothing was written'
    }
    if ((Invoke-Remote "podman network exists $($script:Network)").Code -ne 0) {
        $known = (Invoke-Remote "podman network ls --format '{{.Name}}'").Output -replace '\s+', ', '
        Stop-Script "the podman network '$($script:Network)' does not exist on the host (networks there: $known); set `$env:MOZAIC_NETWORK to the Mozaic network; nothing was written"
    }
    if (-not $script:ImageName) {
        $seen = Invoke-Remote "podman ps --format '{{.Image}}'"
        $candidates = @($seen.Output -split "`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ -match 'mozaic-runtime' } | Sort-Object -Unique)
        if ($candidates.Count -ne 1) { Stop-Script "could not detect the installed runtime image; give -Image. Candidates seen: $($candidates -join ', ')" }
        $script:ImageName = $candidates[0]
    }
    if ((Invoke-Remote 'test -r /etc/mozaic/postgres-tls/ca.crt').Code -eq 0) {
        $script:CaMount = '--volume /etc/mozaic/postgres-tls/ca.crt:/run/mozaic-postgres-ca/ca.crt:ro'
    }
    if ($AdminBin) {
        if (-not (Test-Path -LiteralPath $AdminBin -PathType Leaf)) { Stop-Script "-AdminBin is not a file: $AdminBin" }
        $made = Invoke-Remote "umask 077; mktemp -d $RemoteTmp/mozaic-varset.XXXXXX"
        if ($made.Output -notmatch '^/var/tmp/mozaic-varset\.[A-Za-z0-9]{6}$') { Stop-Script 'unexpected temporary directory from the host' }
        $script:RemoteDir = $made.Output
        $bytes = [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $AdminBin).Path)
        $upload = Invoke-Remote "cat > $($script:RemoteDir)/mozaic-admin && chmod 700 $($script:RemoteDir)/mozaic-admin" $bytes
        if ($upload.Code -ne 0) { Stop-Script "uploading the admin binary failed: $($upload.Error)" }
        $script:Entrypoint = '/opt/mozaic-varset/mozaic-admin'
    }
}

# The remote command line holds only fixed names and identifiers, never a value.
function Get-RemoteCommand([string]$Name, [string]$Operation) {
    $tail = "pipeline-variable-set --organization-id $Org --project-id $Project --repository-id $Repository " +
        "--name $Name --run-as $RunAs --operation-id $Operation " +
        "--reason 'UTF release pipeline variable $Name entered by the host operator' " +
        "--branch $Branch --pipeline $Pipeline --value-stdin"
    if ($AdminCmd) { return "$AdminCmd $tail" }
    $mounts = $script:CaMount
    if ($script:RemoteDir) { $mounts = "$mounts --volume $($script:RemoteDir)/mozaic-admin:$($script:Entrypoint):ro" }
    $network = $script:Network
    return "podman run --rm -i --network $network --env-file /etc/mozaic/migrator.env --env-file /etc/mozaic/app.env $mounts " +
        "--read-only --tmpfs /tmp:rw,noexec,nosuid,nodev,size=67108864 --cap-drop all --security-opt no-new-privileges " +
        "--entrypoint $($script:Entrypoint) $($script:ImageName) $tail"
}

# Names under which the app already has these values on the host: compose/.env style and .NET style.
# WORKER_REGISTRY_TOKEN is deliberately absent: the host may still hold an old GHCR token there, and this
# variable must be a new read-only key of the Mozaic registry.
$HostKeyMap = [ordered]@{
    POSTGRES_PASSWORD          = @('POSTGRES_PASSWORD')
    STEAM_API_KEY              = @('STEAM_API_KEY', 'Steam__ApiKey')
    DISCORD_CLIENT_ID          = @('DISCORD_CLIENT_ID', 'Discord__OAuth__ClientId')
    DISCORD_CLIENT_SECRET      = @('DISCORD_CLIENT_SECRET', 'Discord__OAuth__ClientSecret')
    DISCORD_APPLICATION_ID     = @('DISCORD_APPLICATION_ID', 'Discord__Bot__ApplicationId')
    DISCORD_PUBLIC_KEY         = @('DISCORD_PUBLIC_KEY', 'Discord__Bot__PublicKey')
    DISCORD_BOT_TOKEN          = @('DISCORD_BOT_TOKEN', 'Discord__Bot__BotToken')
    TEAMSPEAK_PASSWORD         = @('TEAMSPEAK_PASSWORD', 'TeamSpeak__Password')
    TEAMSPEAK_CHANNEL_PASSWORD = @('TEAMSPEAK_CHANNEL_PASSWORD', 'TeamSpeak__ChannelPassword')
    HETZNER_TOKEN              = @('HETZNER_TOKEN', 'Hosting__Providers__Hetzner__Token')
}

# Reads values the UTF app already has on the host, in memory only. It looks at the environment of the UTF
# containers (root's and the deploy user's) and at env/unit files whose path names utf or united. Anything
# whose container, image or path mentions mozaic is never read, and on the host only lines for the known
# names above are selected, so nothing else leaves the host.
function Get-HostValues {
    Initialize-Remote -ReadOnly
    $reverse = @{}
    foreach ($entry in $HostKeyMap.GetEnumerator()) { foreach ($key in $entry.Value) { $reverse[$key] = $entry.Key } }
    $connection = 'ConnectionStrings__database'
    $alternation = (@($reverse.Keys) + $connection | Sort-Object -Unique) -join '|'
    $result = @{ Values = @{}; Sources = @{}; Conflicts = New-Object System.Collections.Generic.List[string]; TooShort = New-Object System.Collections.Generic.List[string] }

    $accept = {
        param([string]$Key, [string]$Raw, [string]$Source)
        $value = $Raw.Trim()
        if ($value.Length -ge 2 -and (($value[0] -eq '"' -and $value[$value.Length - 1] -eq '"') -or ($value[0] -eq "'" -and $value[$value.Length - 1] -eq "'"))) { $value = $value.Substring(1, $value.Length - 2) }
        $variable = $reverse[$Key]
        if ($Key -eq $connection) {
            if ($value -notmatch '(?i)(?:^|;)\s*Password=([^;]+)') { return }
            $variable = 'POSTGRES_PASSWORD'; $value = $Matches[1]
        }
        if (-not $variable -or -not $value) { return }
        if ($value.Length -lt $MinimumLength) { if (-not $result.TooShort.Contains($variable)) { $result.TooShort.Add($variable) }; return }
        if (-not $result.Values.ContainsKey($variable)) { $result.Values[$variable] = $value; $result.Sources[$variable] = $Source }
        elseif ($result.Values[$variable] -cne $value -and -not $result.Conflicts.Contains($variable)) { $result.Conflicts.Add($variable) }
    }

    # Running containers first: they hold what the app actually uses. Owners: root and the deploy user.
    $owners = @(@{ Label = 'root'; Prefix = '' })
    $uid = (Invoke-Remote "id -u $HostUser").Output
    if ($uid -match '^\d+$') { $owners += @{ Label = $HostUser; Prefix = "runuser -u $HostUser -- env XDG_RUNTIME_DIR=/run/user/$uid " } }
    foreach ($owner in $owners) {
        $listing = Invoke-Remote "$($owner.Prefix)podman ps -a --format '{{.Names}} {{.Image}}'"
        foreach ($row in ($listing.Output -split "`n")) {
            $parts = $row.Trim() -split ' ', 2
            if ($parts.Count -lt 2 -or $parts[0] -notmatch '^[A-Za-z0-9_.-]+$') { continue }
            if ("$($parts[0]) $($parts[1])" -notmatch '(?i)utf|united' -or "$($parts[0]) $($parts[1])" -match '(?i)mozaic') { continue }
            $environment = Invoke-Remote "$($owner.Prefix)podman inspect $($parts[0]) --format '{{range .Config.Env}}{{println .}}{{end}}' | grep -E '^($alternation)='"
            foreach ($line in ($environment.Output -split "`n")) {
                if ($line -match '^(?<key>[A-Za-z0-9_]+)=(?<value>.*)$') { & $accept $Matches['key'] $Matches['value'] "container $($parts[0]) ($($owner.Label))" }
            }
        }
    }
    # Then env and unit files of the UTF deployment.
    $files = Invoke-Remote "find /etc /opt /srv /home /root -maxdepth 5 -type f \( -name '*.env' -o -name '.env' -o -name '*.service' -o -name '*.container' \) 2>/dev/null | grep -iE 'utf|united' | grep -viE 'mozaic|node_modules|/[.]cache/' | xargs -r grep -HsE '^[[:space:]]*(Environment=)?($alternation)='"
    foreach ($line in ($files.Output -split "`n")) {
        if ($line -match '^(?<path>/[^:]+):\s*(?:Environment=)?(?<key>[A-Za-z0-9_]+)=(?<value>.*)$') { & $accept $Matches['key'] $Matches['value'] "file $($Matches['path'])" }
    }
    return $result
}

$script:ImageName = $Image
$script:Network = if ($env:MOZAIC_NETWORK) { $env:MOZAIC_NETWORK } else { 'mozaic' }

if ($List) {
    foreach ($variable in $Variables) { '{0,-28} {1}' -f $variable.Name, $variable.Text }
    return
}

$values = @{}
$sources = @{}
$found = $false
$hostData = $null
if ($FromHost) {
    Write-Host "Looking on the host for values the app already has (names and sources only are shown)...`n"
    $hostData = Get-HostValues
    if ($hostData.Values.Count -eq 0) { Write-Host "  nothing found: no UTF container or env file on the host holds these names.`n" }
    foreach ($variable in $Variables) {
        if ($hostData.Values.ContainsKey($variable.Name)) {
            Write-Host ('  found {0,-28} {1} ({2} characters)' -f $variable.Name, $hostData.Sources[$variable.Name], $hostData.Values[$variable.Name].Length)
        }
    }
    foreach ($name in $hostData.Conflicts) { Write-Host "  note: $name has different values in different places; the first source above is used" -ForegroundColor Yellow }
    foreach ($name in $hostData.TooShort) { Write-Host "  note: $name was found but is shorter than $MinimumLength characters, so it is left for you to enter" -ForegroundColor Yellow }
    Write-Host "  not imported by design: WORKER_REGISTRY_TOKEN (needs a new read-only Mozaic registry key, not an old GHCR token) and UTF_DEPLOY_SSH_KEY`n"
}
Write-Host "Only UTF_DEPLOY_SSH_KEY is needed by the release pipeline today; the other variables are stored for the later start-up step and can be skipped with Enter.`n"
Write-Host "Press Enter to skip a variable. Input is not shown.`n"
foreach ($variable in $Variables) {
    if ($Only -and $Only -ne $variable.Name) { continue }
    $found = $true
    Write-Host $variable.Name
    if ($hostData -and $hostData.Values.ContainsKey($variable.Name)) {
        $values[$variable.Name] = $hostData.Values[$variable.Name]
        $sources[$variable.Name] = $hostData.Sources[$variable.Name]
        Write-Host "  taken from the host: $($hostData.Sources[$variable.Name]) ($($values[$variable.Name].Length) characters)`n"
        continue
    }
    Write-Host "  $($variable.Text)"
    if ($variable.Kind -eq 'key') { $value = Read-KeyValue }
    else { $value = Read-Answer '  Value (Enter to skip)' -Hidden }
    if (-not $value) { Write-Host "  skipped`n"; continue }
    if ($value.Length -lt $MinimumLength) {
        Stop-Script "$($variable.Name) is shorter than $MinimumLength characters; short values would garble pipeline logs when masked"
    }
    $values[$variable.Name] = $value
    Write-Host "  accepted ($($value.Length) characters)`n"
}
if ($Only -and -not $found) { Stop-Script "unknown variable name: $Only" }
if ($values.Count -eq 0) { Write-Host 'Nothing entered; nothing written.'; return }

Write-Host "About to write these secret variables (names only) for branch $Branch and pipeline $Pipeline via ${Target}:"
$values.Keys | Sort-Object | ForEach-Object { if ($sources.ContainsKey($_)) { Write-Host "  $_   (from the host: $($sources[$_]))" } else { Write-Host "  $_" } }
$confirm = Read-Answer 'Write them now? [y/N]'
if ($confirm -ne 'y' -and $confirm -ne 'Y') { Write-Host 'Cancelled; nothing written.'; return }

$failed = $false
try {
    Initialize-Remote
    foreach ($name in ($values.Keys | Sort-Object)) {
        $bytes = (New-Object System.Text.UTF8Encoding($false)).GetBytes($values[$name])
        $result = Invoke-Remote (Get-RemoteCommand $name ([guid]::NewGuid().ToString())) $bytes
        if ($result.Code -eq 0) { Write-Host "written: $name" }
        else {
            Write-Host "FAILED: $name (exit code $($result.Code))" -ForegroundColor Red
            # The tool's errors are fixed text and never contain a value, so they are safe to show.
            $detail = ($result.Error + ' ' + $result.Output).Trim()
            if ($detail.Length -gt 1500) { $detail = $detail.Substring(0, 1500) + ' ...' }
            if ($detail) { Write-Host "  host said: $detail" -ForegroundColor Red }
            $failed = $true
        }
    }
}
finally {
    $values.Clear()
    Remove-RemoteUpload
}
if ($failed) { Stop-Script 'at least one variable was not written; rerun for the failed names with -Only NAME' }
Write-Host "`nDone. The tool printed metadata only; no value can be read back."

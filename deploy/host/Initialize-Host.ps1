<#
.SYNOPSIS
  One-time setup of Amsel for the UTF release pipeline. Run from this computer's PowerShell; it connects itself.

.DESCRIPTION
  Does exactly two things, both idempotent, and shows each before it changes anything:
    1. authorizes the organization-wide CI key (deploy/amsel-ci.pub) for the deploy user as `restrict <key>`,
       in the file sshd really reads for that user (it asks sshd; on Amsel a Match User block moves it out of ~/.ssh);
    2. installs deploy/host/utf-api.service and utf-web.service into the deploy user's systemd user directory
       and runs daemon-reload. Nothing is enabled or started: the first pipeline release does the switch-over.
  It never stops, starts or replaces a running service and never touches a backup. All deployment happens in the pipeline.
  Afterwards it proves from here that the private key logs in as the deploy user.

.PARAMETER Target
  SSH login that is root on the host, for example root@2a01:4f8:c0c:e2e::1.

.PARAMETER PrivateKey
  The matching private key on this computer (default %USERPROFILE%\.ssh\amsel_ci), used only for the final proof.

.PARAMETER CheckOnly
  Only read and report what would change.
#>
param(
    [string]$Target = $env:UTF_OPERATOR_SSH,
    [string]$PublicKeyFile = (Join-Path $PSScriptRoot '..\amsel-ci.pub'),
    [string]$PrivateKey = (Join-Path $env:USERPROFILE '.ssh\amsel_ci'),
    [string]$User = 'utf-deploy',
    [string]$KnownHosts = (Join-Path $PSScriptRoot '..\known_hosts'),
    [string[]]$Units = @('utf-api.service', 'utf-web.service'),
    [switch]$CheckOnly
)
$ErrorActionPreference = 'Stop'
if (-not $Target) { $Target = 'root@2a01:4f8:c0c:e2e::1' }
$Ssh = if ($env:UTF_SSH) { $env:UTF_SSH } else { Join-Path $env:SystemRoot 'System32\OpenSSH\ssh.exe' }

function Stop-Script([string]$Message) {
    Write-Host "Initialize-Host: $Message" -ForegroundColor Red
    exit 1
}

# One SSH command with raw-byte stdin and the host key pinned from deploy/known_hosts (no trust on first use).
function Invoke-Ssh([string]$Login, [string]$Command, [byte[]]$Stdin, [string]$Identity) {
    $pinned = (Resolve-Path -LiteralPath $KnownHosts).Path.Replace('\', '/')
    $info = New-Object System.Diagnostics.ProcessStartInfo
    $info.FileName = $Ssh
    $identityArguments = ''
    if ($Identity) { $identityArguments = '-i "' + $Identity.Replace('\', '/') + '" -o IdentitiesOnly=yes ' }
    $info.Arguments = '-T -o BatchMode=yes -o StrictHostKeyChecking=yes -o "UserKnownHostsFile=' + $pinned + '" -o GlobalKnownHostsFile=NUL ' + $identityArguments + $Login + ' "' + $Command.Replace('"', '\"') + '"'
    $info.UseShellExecute = $false
    $info.RedirectStandardInput = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    # Windows PowerShell 5.1 would prepend a byte-order mark to stdin from the console code page.
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

if ($Target -notmatch '^[A-Za-z0-9_.-]+@[A-Za-z0-9:.\[\]-]+$') { Stop-Script 'the target must look like user@host' }
if (-not (Test-Path -LiteralPath $PublicKeyFile -PathType Leaf)) { Stop-Script "public key file not found: $PublicKeyFile" }
$publicKey = ([IO.File]::ReadAllText((Resolve-Path -LiteralPath $PublicKeyFile).Path)).Trim()
if ($publicKey -match '^(ssh-ed25519) ([A-Za-z0-9+/=]+)( [^\r\n"]*)?$') { $keyType = $Matches[1]; $keyBody = $Matches[2] }
else { Stop-Script 'the public key file must hold exactly one ssh-ed25519 public key' }
foreach ($unit in $Units) {
    if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot $unit) -PathType Leaf)) { Stop-Script "unit file not found next to this script: $unit" }
}

$id = Invoke-Ssh $Target 'id -u'
if ($id.Code -ne 0) { Stop-Script "could not log in to the host: $($id.Error)" }
if ($id.Output -ne '0') { Stop-Script 'that login is not root on the host' }

$facts = Invoke-Ssh $Target "printf '%s|%s|%s\n' `$(getent passwd $User | cut -d: -f6) `$(id -u $User) `$(sshd -T -C user=$User,host=localhost,addr=127.0.0.1 2>/dev/null | grep -i '^authorizedkeysfile ' | cut -d' ' -f2)"
if ($facts.Output -match '^(/[A-Za-z0-9_./-]+)\|([0-9]+)\|([A-Za-z0-9_./%-]*)$') { $userHome = $Matches[1]; $userId = $Matches[2]; $keysFile = $Matches[3] }
else { Stop-Script "could not read the home directory, uid or key file of '$User' on the host (got '$($facts.Output)')" }
if (-not $keysFile -or $keysFile -eq 'none') { $keysFile = '.ssh/authorized_keys' }
$keysFile = $keysFile.Replace('%h', $userHome).Replace('%u', $User)
if (-not $keysFile.StartsWith('/')) { $keysFile = "$userHome/$keysFile" }
$inHome = $keysFile.StartsWith("$userHome/")
$unitDirectory = "$userHome/.config/systemd/user"

$keyPresent = (Invoke-Ssh $Target "grep -cF $keyBody $keysFile 2>/dev/null || true").Output -match '^[1-9]'
$changes = @()
foreach ($unit in $Units) {
    $bytes = [IO.File]::ReadAllBytes((Join-Path $PSScriptRoot $unit))
    $local = ([BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash($bytes)).Replace('-', '')).ToLowerInvariant()
    $remote = (Invoke-Ssh $Target "sha256sum $unitDirectory/$unit 2>/dev/null | cut -d' ' -f1").Output
    $changes += [pscustomobject]@{ Unit = $unit; Bytes = $bytes; Same = ($remote -eq $local) }
}

Write-Host "Host login:   $Target"
Write-Host "Deploy user:  $User (uid $userId, home $userHome)"
Write-Host "Key file:     $keysFile (the file sshd reads for this user)"
Write-Host ("CI key:       " + $(if ($keyPresent) { 'already authorized' } else { "will be added as: restrict $keyType <key $($keyBody.Substring(0, 12))...> amsel-ci" }))
foreach ($change in $changes) { Write-Host ("Unit file:    {0} {1}" -f $change.Unit, $(if ($change.Same) { 'already up to date' } else { "will be written to $unitDirectory" })) }
if ($keyPresent -and -not (@($changes | Where-Object { -not $_.Same }).Count)) { Write-Host "`nNothing to change." }
if ($CheckOnly) { Write-Host "`nCheck only: nothing was changed."; return }

$answer = if ($env:UTF_VARS_TEST_ANSWERS) { ([IO.File]::ReadAllLines($env:UTF_VARS_TEST_ANSWERS) | Select-Object -First 1) } else { Read-Host "`nApply this now? It changes SSH access and installs two unit files; no service is started or stopped. [y/N]" }
if ($answer -ne 'y' -and $answer -ne 'Y') { Write-Host 'Cancelled; nothing changed.'; return }

if (-not $keyPresent) {
    $line = "restrict $keyType $keyBody amsel-ci"
    $stamp = Get-Date -Format 'yyyyMMddHHmmss'
    $command = "cp -p $keysFile $keysFile.bak-$stamp 2>/dev/null; cat >> $keysFile"
    if ($inHome) { $command = "install -d -m 700 -o $User -g $User $userHome/.ssh && " + $command + " && chown $User`:$User $keysFile && chmod 600 $keysFile" }
    else { $command = $command + " && chown root:root $keysFile && chmod 644 $keysFile" }
    $add = Invoke-Ssh $Target $command ((New-Object System.Text.UTF8Encoding($false)).GetBytes($line + "`n"))
    if ($add.Code -ne 0) { Stop-Script "adding the key failed: $($add.Error)" }
    Write-Host 'Authorized the CI key.'
}
foreach ($change in $changes) {
    if ($change.Same) { continue }
    $write = Invoke-Ssh $Target "install -d -m 755 -o $User -g $User $unitDirectory && install -m 644 -o $User -g $User /dev/stdin $unitDirectory/$($change.Unit)" $change.Bytes
    if ($write.Code -ne 0) { Stop-Script "writing $($change.Unit) failed: $($write.Error)" }
    Write-Host "Installed $($change.Unit)."
}
$reload = Invoke-Ssh $Target "runuser -u $User -- env XDG_RUNTIME_DIR=/run/user/$userId systemctl --user daemon-reload"
if ($reload.Code -ne 0) { Stop-Script "daemon-reload failed: $($reload.Error)" }

if (-not (Test-Path -LiteralPath $PrivateKey -PathType Leaf)) { Write-Warning "private key not found at $PrivateKey; skipped the proof"; return }
$hostPart = $Target.Substring($Target.IndexOf('@') + 1)
$proof = Invoke-Ssh "$User@$hostPart" 'id -un' $null $PrivateKey
if ($proof.Code -ne 0 -or $proof.Output -ne $User) { Stop-Script "PROOF FAILED: the key did not log in as $User ($($proof.Error))" }
Write-Host "`nProof passed: the CI key logs in as $User. The next push to the trusted branch releases."

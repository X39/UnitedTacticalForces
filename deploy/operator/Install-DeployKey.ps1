<#
.SYNOPSIS
  Authorize the release pipeline's SSH key on Amsel so it can do exactly one thing: run `podman load`.
  Run from this computer's PowerShell; it connects to the host itself.

.DESCRIPTION
  Appends one line to the deploy user's authorized_keys:
      restrict,command="<podman> load" <public key from deploy/utf-deploy.pub>
  `restrict` switches off forwarding, PTY and agent use; `command=` makes `podman load` the only possible
  command whatever the client asks for. Nothing else on the host is touched: no script, no daemon, no registry
  credential, and existing keys stay as they are (their fingerprints are listed so you can retire old ones).
  Afterwards the script proves, from here, that the key really is limited: asking for `id` must not run `id`.

.PARAMETER Target
  SSH login that is root on the host, for example root@2a01:4f8:c0c:e2e::1.

.PARAMETER PrivateKey
  The matching private key on this computer, used only for the final proof.

.PARAMETER CheckOnly
  Only read: show the podman path, the deploy user's home and the existing key fingerprints. Changes nothing.
#>
param(
    [string]$Target = $env:UTF_OPERATOR_SSH,
    [string]$PublicKeyFile = (Join-Path $PSScriptRoot '..\utf-deploy.pub'),
    [string]$PrivateKey = (Join-Path $env:USERPROFILE '.ssh\utf_deploy_ci'),
    [string]$User = 'utf-deploy',
    [string]$KnownHosts = (Join-Path $PSScriptRoot '..\known_hosts'),
    [switch]$CheckOnly
)
$ErrorActionPreference = 'Stop'
$Ssh = if ($env:UTF_SSH) { $env:UTF_SSH } else { Join-Path $env:SystemRoot 'System32\OpenSSH\ssh.exe' }

function Stop-Script([string]$Message) {
    Write-Host "Install-DeployKey: $Message" -ForegroundColor Red
    exit 1
}

# One SSH command with raw-byte stdin, the host key pinned from deploy/known_hosts (no trust-on-first-use).
function Invoke-Ssh([string]$Login, [string]$Command, [byte[]]$Stdin, [string]$Identity) {
    $pinned = (Resolve-Path -LiteralPath $KnownHosts).Path.Replace('\', '/')
    $info = New-Object System.Diagnostics.ProcessStartInfo
    $info.FileName = $Ssh
    $identityArguments = ''
    if ($Identity) { $identityArguments = '-i "' + $Identity.Replace('\', '/') + '" -o IdentitiesOnly=yes ' }
    $info.Arguments = '-T -o BatchMode=yes -o StrictHostKeyChecking=yes -o "UserKnownHostsFile=' + $pinned + '" -o GlobalKnownHostsFile=NUL ' + $identityArguments + $Login + ' "' + $Command + '"'
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

if (-not $Target) { Stop-Script 'give -Target (the ssh login that is root on the host), for example root@2a01:4f8:c0c:e2e::1' }
if ($Target -notmatch '^[A-Za-z0-9_.-]+@[A-Za-z0-9:.\[\]-]+$') { Stop-Script 'the target must look like user@host' }
if (-not (Test-Path -LiteralPath $PublicKeyFile -PathType Leaf)) { Stop-Script "public key file not found: $PublicKeyFile" }
$publicKey = ([IO.File]::ReadAllText((Resolve-Path -LiteralPath $PublicKeyFile).Path)).Trim()
if ($publicKey -match '^(ssh-ed25519) ([A-Za-z0-9+/=]+)( [^\r\n"]*)?$') { $keyType = $Matches[1]; $keyBody = $Matches[2] }
else { Stop-Script 'the public key file must hold exactly one ssh-ed25519 public key' }

$id = Invoke-Ssh $Target 'id -u'
if ($id.Code -ne 0) { Stop-Script "could not log in to the host: $($id.Error)" }
if ($id.Output -ne '0') { Stop-Script 'that login is not root on the host' }

$facts = Invoke-Ssh $Target "printf '%s|%s\n' `$(command -v podman) `$(getent passwd $User | cut -d: -f6)"
if ($facts.Output -match '^(/[A-Za-z0-9_./-]+)\|(/[A-Za-z0-9_./-]+)$') { $podman = $Matches[1]; $userHome = $Matches[2] }
else { Stop-Script "could not find podman and the home directory of '$User' on the host (got '$($facts.Output)')" }
$authorized = "$userHome/.ssh/authorized_keys"
$inHome = $true
# sshd may read this user's keys from somewhere else (a Match User block); ask it instead of assuming the home.
$effective = Invoke-Ssh $Target "sshd -T -C user=$User,host=localhost,addr=127.0.0.1 2>/dev/null | grep -i '^authorizedkeysfile ' | cut -d' ' -f2"
if ($effective.Output -match '^[A-Za-z0-9_./%-]+$' -and $effective.Output -ne 'none') {
    $path = $effective.Output.Replace('%h', $userHome).Replace('%u', $User)
    if (-not $path.StartsWith('/')) { $path = "$userHome/$path" }
    if ($path -ne $authorized) { $authorized = $path; $inHome = $false }
}
$line = "restrict,command=`"$podman load`" $keyType $keyBody utf-deploy-ci"

Write-Host "Host login:        $Target"
Write-Host "Deploy user:       $User (home $userHome)"
Write-Host "Line to authorize: restrict,command=`"$podman load`" $keyType <key $($keyBody.Substring(0, 12))...> utf-deploy-ci"
$existing = Invoke-Ssh $Target "ssh-keygen -lf $authorized 2>/dev/null; true"
Write-Host "Existing keys of $($User):"
if ($existing.Output) { $existing.Output -split "`n" | ForEach-Object { Write-Host "  $_" } } else { Write-Host '  (none)' }
if ($CheckOnly) { Write-Host "`nCheck only: nothing was changed."; return }

$answer = if ($env:UTF_VARS_TEST_ANSWERS) { ([IO.File]::ReadAllLines($env:UTF_VARS_TEST_ANSWERS) | Select-Object -First 1) } else { Read-Host "`nAdd this line now? This changes the host's SSH access. [y/N]" }
if ($answer -ne 'y' -and $answer -ne 'Y') { Write-Host 'Cancelled; nothing changed.'; return }

$present = Invoke-Ssh $Target "grep -cF $keyBody $authorized 2>/dev/null || true"
if ($present.Output -match '^[1-9]') {
    Write-Host 'The key is already authorized; leaving the file as it is.'
}
else {
    $bytes = (New-Object System.Text.UTF8Encoding($false)).GetBytes($line + "`n")
    $addCommand = "install -d -m 700 -o $User -g $User $userHome/.ssh && cat >> $authorized && chown $User`:$User $authorized && chmod 600 $authorized"
    # A file outside the home stays root-owned and world-readable, as sshd requires for such paths.
    if (-not $inHome) { $addCommand = "cat >> $authorized && chown root:root $authorized && chmod 644 $authorized" }
    $add = Invoke-Ssh $Target $addCommand $bytes
    if ($add.Code -ne 0) { Stop-Script "adding the key failed: $($add.Error)" }
    Write-Host 'Added the key.'
}
$listed = Invoke-Ssh $Target "ssh-keygen -lf $authorized"
Write-Host "authorized_keys now lists:"
$listed.Output -split "`n" | ForEach-Object { Write-Host "  $_" }

# Proof from this computer: the key may run only `podman load`. Asking for `id` must not run `id`.
if (-not (Test-Path -LiteralPath $PrivateKey -PathType Leaf)) { Write-Warning "private key not found at $PrivateKey; skipped the proof"; return }
$hostPart = $Target.Substring($Target.IndexOf('@') + 1)
$proof = Invoke-Ssh "$User@$hostPart" 'id' ([Text.Encoding]::ASCII.GetBytes('not an image')) $PrivateKey
if ($proof.Output -match 'uid=' -or $proof.Error -match 'uid=') { Stop-Script 'PROOF FAILED: the key ran a command of its own choosing; remove that authorized_keys line now' }
if ($proof.Code -eq 0) { Stop-Script 'PROOF INCONCLUSIVE: expected podman load to reject the dummy input, but it succeeded' }
if ($proof.Error -match 'Permission denied|publickey') { Stop-Script "the key was not accepted: $($proof.Error)" }
Write-Host "`nProof passed: the key was accepted, asking for 'id' did not run it, and podman load answered:"
Write-Host "  $($proof.Error.Split("`n")[0])"
Write-Host "`nThe release pipeline key is now authorized for 'podman load' only."

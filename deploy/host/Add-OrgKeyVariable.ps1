<#
.SYNOPSIS
  Prepares everything for entering the organization secret AMSEL_DEPLOY_SSH_KEY in Mozaic, from this computer.

.DESCRIPTION
  Mozaic only accepts organization pipeline variables from a signed-in session that has just passed MFA
  (it asks "Verify with MFA to continue"), so no script can create the variable for you. This script does every
  other step: it checks that the private key matches the public key authorized on Amsel (deploy/amsel-ci.pub),
  puts the whole multi-line key on the clipboard, opens the organization's Pipeline variables page, prints the exact
  field values, waits until you have saved the variable, then empties the clipboard.
  The key is never printed and never written anywhere.
#>
param(
    [string]$PrivateKey = (Join-Path $env:USERPROFILE '.ssh\amsel_ci'),
    [string]$PublicKeyFile = (Join-Path $PSScriptRoot '..\amsel-ci.pub'),
    [string]$Url = 'https://x39.mozaic.space/settings/pipelines/variables'
)
$ErrorActionPreference = 'Stop'
function Stop-Script([string]$Message) { Write-Host "Add-OrgKeyVariable: $Message" -ForegroundColor Red; exit 1 }

if (-not (Test-Path -LiteralPath $PrivateKey -PathType Leaf)) { Stop-Script "private key not found: $PrivateKey" }
if (-not (Test-Path -LiteralPath $PublicKeyFile -PathType Leaf)) { Stop-Script "public key file not found: $PublicKeyFile" }
$keygen = Join-Path $env:SystemRoot 'System32\OpenSSH\ssh-keygen.exe'
$derived = (& $keygen -y -f $PrivateKey 2>$null | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or -not $derived) { Stop-Script 'the private key cannot be read (wrong format or passphrase)' }
$committed = ([IO.File]::ReadAllText((Resolve-Path -LiteralPath $PublicKeyFile).Path)).Trim()
if (($derived -split ' ')[1] -ne ($committed -split ' ')[1]) { Stop-Script 'this private key does not match deploy/amsel-ci.pub' }

Get-Content -Raw -LiteralPath $PrivateKey | Set-Clipboard
Start-Process $Url
Write-Host @"

The private key is on your clipboard (it matches the key authorized on Amsel). Mozaic opened at:
  $Url

Create the variable there (you will be asked to verify with MFA first):
  Scope        Organization
  Name         AMSEL_DEPLOY_SSH_KEY
  Value        paste (Ctrl+V)
  Secret       yes
  Delivery     environment variable
  Trust        trusted only
  Branches     refs/heads/master   (add other trusted branches of projects that deploy to Amsel)
  Pipelines    leave unrestricted if offered; otherwise add .mozaic/release.yml of United Tactical Forces

"@
Read-Host 'Press Enter after you saved the variable (this empties the clipboard)' | Out-Null
Set-Clipboard -Value ' '
Write-Host 'Clipboard cleared. The next push to the trusted branch releases.'

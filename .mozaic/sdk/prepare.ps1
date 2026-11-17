# Dependency preparation only, on the explicitly authorized disposable release VM.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('mozaic-release-2a599af')][string]$Connection,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][ValidatePattern('^[a-z0-9-]+\.slice$')][string]$CgroupParent,
    [Parameter(Mandatory)][ValidatePattern('^/user\.slice/[A-Za-z0-9_./@:-]+$')][string]$ParentCgroupPath,
    [Parameter(Mandatory)][string]$PythonPath,
    [Parameter(Mandatory)][string]$ProtocolValidator,
    [switch]$AllowAnonymousFetch
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$source = $PSScriptRoot
. (Join-Path $source 'safety.ps1')
$repository = [IO.Path]::GetFullPath((Join-Path $source '../..'))
$script:output = New-ExclusiveOutput $OutputDirectory $repository
if ($ParentCgroupPath.Contains('/../') -or $ParentCgroupPath.Contains('/./') -or -not $ParentCgroupPath.EndsWith('/' + $CgroupParent)) {
    throw 'The verified parent cgroup path must end in the selected slice.'
}
$marker = [IO.File]::Open((Join-Path $output 'PREPARATION-INCOMPLETE'), 'CreateNew', 'Write', 'None')
$marker.Dispose()
$identity = [Guid]::NewGuid().ToString('N')
$context = Join-Path $output 'context'
$null = [IO.Directory]::CreateDirectory($context)
$null = [IO.Directory]::CreateDirectory((Join-Path $context 'archives'))
$null = [IO.Directory]::CreateDirectory((Join-Path $output 'logs'))
$null = [IO.Directory]::CreateDirectory((Join-Path $output 'receipts'))
$podman = (Get-Command podman -CommandType Application).Source
$script:preparationStartedAt = [DateTime]::UtcNow
$script:deadline = $script:preparationStartedAt.AddMinutes(120)
$sequence = 0
$inputs = Get-Content -Raw -LiteralPath (Join-Path $source 'inputs.json') | ConvertFrom-Json
if ((Get-FileHash -Algorithm SHA256 -LiteralPath $ProtocolValidator).Hash.ToLowerInvariant() -cne $inputs.protocol_validator_sha256) {
    throw 'The mandatory independent Mozaic protocol validator does not match its reviewed SHA256.'
}
$python = (Get-Command $PythonPath -CommandType Application).Source
$validator = (Get-Command $ProtocolValidator -CommandType Application).Source
if ($inputs.schema_version -ne 1 -or $inputs.platform -ne 'linux/amd64') { throw 'Unsupported preparation input contract.' }
$allowedReferences = @($inputs.images.PSObject.Properties.Value)
foreach ($recipe in @('api','worker','aska','sdk')) {
    foreach ($line in (Get-Content -LiteralPath (Join-Path $source "$recipe.Containerfile"))) {
        if ($line -match '^FROM ([^ ]+)') {
            if ($Matches[1] -notin $allowedReferences) { throw 'A recipe base is absent from the reviewed input inventory.' }
        }
    }
}
$runtimeSource = Get-Content -Raw -LiteralPath (Join-Path $source 'runtime-dependencies.sh')
$sdkSource = Get-Content -Raw -LiteralPath (Join-Path $source 'sdk-dependencies.sh')
if (-not $runtimeSource.Contains($inputs.steamcmd_sha256) -or -not $runtimeSource.Contains($inputs.steamcmd_url) -or
    -not $sdkSource.Contains('version=' + $inputs.playwright_dotnet) -or -not $sdkSource.Contains('version=' + $inputs.playwright_react)) {
    throw 'Preparation scripts and reviewed dependency inputs disagree.'
}
$authfile = Join-Path $output 'anonymous-auth.json'
[IO.File]::WriteAllText($authfile, '{"auths":{}}')

function Invoke-Local([string]$Executable, [string[]]$Arguments, [double]$Seconds = 30, [scriptblock]$Poll, [switch]$Capture) {
    $script:sequence += 1
    $log = Join-Path $script:output ('logs/{0:D4}-process' -f $script:sequence)
    Invoke-DeadlineProcess -Executable $Executable -Arguments $Arguments -Deadline $script:deadline -LogPrefix $log -CallSeconds $Seconds -OnPoll $Poll -Capture:$Capture
}
function Invoke-Engine([string[]]$EngineArguments) {
    return (Invoke-Local -Executable $podman -Arguments (@('--connection', $Connection) + $EngineArguments) -Capture)
}
function Get-Storage {
    return (Invoke-Engine -EngineArguments @('info', '--format', '{{json .Store}}') | ConvertFrom-Json)
}
$pythonVersion = Invoke-Local -Executable $python -Arguments @('-B','-c','import sys; assert sys.version_info >= (3, 11); print(sys.version)') -Capture
$verificationFiles = foreach ($file in @('prepare.ps1','safety.ps1','validate-archive.py','canonicalize-archive.py','native-build.py')) {
    [ordered]@{path=$file; sha256=(Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $source $file)).Hash.ToLowerInvariant()}
}
[ordered]@{protocol_validator_sha256=$inputs.protocol_validator_sha256; python_version=$pythonVersion.Trim(); source=$verificationFiles} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'verification-tools.json') -Encoding utf8NoBOM
$engine = Invoke-Engine -EngineArguments @('info','--format=json') | ConvertFrom-Json
$connections = Invoke-Engine -EngineArguments @('system','connection','list','--format=json') | ConvertFrom-Json
Assert-RootlessEngine $engine $connections $Connection
$machineSsh = Invoke-Local -Executable $podman -Arguments @('machine','inspect',$Connection,'--format','{{json .SSHConfig}}') -Capture | ConvertFrom-Json
Assert-MachineBinding $machineSsh $connections $Connection
$script:baseline = $engine.store
$engine.host | Select-Object os,arch,cgroupManager,cgroupVersion,idMappings,security | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'engine-preflight.json') -Encoding utf8NoBOM
if (($baseline.graphRootAllocated - $baseline.graphRootUsed) -lt 80GB) { throw 'Preparation requires at least 80 GiB free in the release VM.' }
function Assert-Budget {
    # Poll callbacks run beneath the subprocess helper's dynamic local scope.
    # Shared preparation state must not resolve to that helper's launch variables.
    if (([DateTime]::UtcNow - $script:preparationStartedAt).TotalMinutes -gt 120) { throw 'Preparation exceeded its 120-minute operator deadline.' }
    $store = Get-Storage
    $externalBytes = (Get-ChildItem -LiteralPath $script:output -File -Recurse | Measure-Object -Property Length -Sum).Sum
    if ([Math]::Max([long]0, [long]($store.graphRootUsed - $script:baseline.graphRootUsed)) + $externalBytes -gt 64GB) {
        throw 'Preparation exceeded its monitored 64 GiB incremental storage budget.'
    }
    if (($store.graphRootAllocated - $store.graphRootUsed) -lt 32GB) { throw 'Release VM free-space floor reached.' }
}
function Invoke-Bounded([string[]]$EngineArguments) {
    Assert-Budget
    Invoke-Local -Executable $podman -Arguments (@('--connection', $Connection) + $EngineArguments) -Seconds 3600 -Poll { Assert-Budget }
    Assert-Budget
}
function Test-Archive([string]$Archive, [string]$ImageId) {
    if ((Get-FileHash -Algorithm SHA256 -LiteralPath $validator).Hash.ToLowerInvariant() -cne $inputs.protocol_validator_sha256) { throw 'Protocol validator changed during preparation.' }
    # The real protocol validator checks the strict tar envelope and complete graph first.
    Invoke-Local -Executable $validator -Arguments @($Archive) -Seconds 600 -Poll { Assert-Budget }
    $receipt = Invoke-Local -Executable $python -Arguments @('-B',(Join-Path $source 'validate-archive.py'),$Archive,$ImageId) -Seconds 600 -Poll { Assert-Budget } -Capture
    return ($receipt | ConvertFrom-Json)
}
function Convert-Archive([string]$RawArchive, [string]$Archive, [string]$ImageId, [string]$Profile) {
    foreach ($name in @('canonicalize-archive.py','validate-archive.py')) {
        $recorded = @($verificationFiles | Where-Object { $_.path -ceq $name })[0].sha256
        if ((Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $source $name)).Hash.ToLowerInvariant() -cne $recorded) { throw 'Local transport verification source changed during preparation.' }
    }
    $receipt = Invoke-Local -Executable $python -Arguments @('-B',(Join-Path $source 'canonicalize-archive.py'),$RawArchive,$Archive,$ImageId,"localhost/utf-dependencies-$Profile") -Seconds 600 -Poll { Assert-Budget } -Capture
    $transport = $receipt | ConvertFrom-Json
    $checked = Test-Archive $Archive $ImageId
    if ($checked.archive_sha256 -cne $transport.archive_sha256 -or $checked.manifest_digest -cne $transport.manifest_digest) { throw 'Canonical transport changed before the independent gate.' }
    $transport | Add-Member -NotePropertyName protocol_validated -NotePropertyValue $true
    $transport | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output "receipts/transport-$Profile.json") -Encoding utf8NoBOM
    return $transport
}
$script:heartbeat = Join-Path $script:output 'native-heartbeat'
function Update-Heartbeat {
    [IO.File]::WriteAllText($script:heartbeat, [DateTime]::UtcNow.ToString('O'))
}
function Invoke-Native([string]$Mode, [string]$Profile) {
    Update-Heartbeat
    $request = [ordered]@{mode=$Mode;identity=$identity;slice=$CgroupParent;cgroup_path=$ParentCgroupPath;graph_root=$baseline.graphRoot;deadline=([DateTimeOffset]$deadline).ToUnixTimeSeconds();heartbeat=(ConvertTo-MachinePath $heartbeat)}
    if ($Mode -eq 'build') {
        [ordered]@{profile=$Profile; stage="/var/tmp/utf-sdk-$identity-$Profile"; scope="mozaic-sdk-utf$identity-$Profile.scope"; slice=$CgroupParent; cgroup_path=$ParentCgroupPath; identity=$identity} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output "native-intent-$Profile.json") -Encoding utf8NoBOM
        $names = if ($Profile -eq 'sdk') {
            @('sdk.Containerfile','capture-apt.sh','sdk-dependencies.sh','select-dotnet.sh','smoke-browser.cjs','inputs.json','archives/api.oci.tar','archives/worker.oci.tar','archives/aska.oci.tar','archives/nginx.oci.tar','archives/SHA256SUMS')
        } else { @("$Profile.Containerfile",'capture-apt.sh','runtime-dependencies.sh') }
        $hashes = [ordered]@{}
        foreach ($name in $names) { $hashes[$name] = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $context $name)).Hash.ToLowerInvariant() }
        $request.profile = $Profile
        $request.context = ConvertTo-MachinePath $context
        $request.files = $hashes
    }
    $payload = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes(($request | ConvertTo-Json -Compress -Depth 8)))
    $helper = ConvertTo-PosixArgument (ConvertTo-MachinePath (Join-Path $source 'native-build.py'))
    $remaining = [Math]::Floor(($deadline - [DateTime]::UtcNow).TotalSeconds)
    if ($remaining -lt 20) { throw 'Insufficient deadline for native preparation and bounded cleanup.' }
    $command = "/usr/bin/timeout --signal=TERM --kill-after=15 $remaining /usr/bin/python3 $helper $payload"
    # No manual SSH credentials, default connection changes or unbounded native calls.
    Invoke-Local -Executable $podman -Arguments @('machine','ssh','--username','user',$Connection,$command) -Seconds $remaining -Poll { Update-Heartbeat; Assert-Budget }
    Assert-Budget
}
# Validate native identity, matching graph root and the actual bounded parent
# before any pull or BUILD mutation, rather than trusting the machine's name.
Invoke-Native 'preflight' ''

# Copy a closed source list, never a repository or caller-supplied context.
$files = @('inputs.json','capture-apt.sh','runtime-dependencies.sh','sdk-dependencies.sh','select-dotnet.sh','api.Containerfile','worker.Containerfile','aska.Containerfile','sdk.Containerfile','smoke-browser.cjs')
$sourceHashes = foreach ($file in $files) {
    Copy-Item -LiteralPath (Join-Path $source $file) -Destination (Join-Path $context $file)
    [ordered]@{ path=$file; sha256=(Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $context $file)).Hash.ToLowerInvariant() }
}
$sourceHashes | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'source-files.json') -Encoding utf8NoBOM
foreach ($entry in $inputs.images.PSObject.Properties) {
    if ($entry.Value -notmatch '^[a-z0-9./-]+@sha256:[0-9a-f]{64}$') { throw 'Input is not an immutable image reference.' }
    if ($AllowAnonymousFetch) { Invoke-Bounded -EngineArguments @('pull','--quiet','--tls-verify=true','--authfile',$authfile,$entry.Value) }
    $image = Invoke-Engine -EngineArguments @('image','inspect',$entry.Value) | ConvertFrom-Json
    if ($image[0].Architecture -ne 'amd64' -or $image[0].Os -ne 'linux' -or $entry.Value -notin $image[0].RepoDigests) { throw 'A preloaded input has the wrong platform or digest binding.' }
}

[ordered]@{connection=$Connection; parent=$CgroupParent; path=$ParentCgroupPath; pids_max=512; observed_at=[DateTime]::UtcNow.ToString('O')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'cgroup-parent.json') -Encoding utf8NoBOM
$images = [ordered]@{}
foreach ($profile in @('api','worker','aska','sdk')) {
    $tag = "localhost/utf-dependencies-${profile}:$identity"
    Invoke-Native 'build' $profile
    $image = (Invoke-Engine -EngineArguments @('image','inspect',$tag) | ConvertFrom-Json)[0]
    $rawArchive = Join-Path $output "$profile.raw.oci.tar"
    $archive = Join-Path $output "$profile.oci.tar"
    Invoke-Bounded -EngineArguments @('save','--format=oci-archive','--output',$rawArchive,$image.Id)
    $checked = Convert-Archive $rawArchive $archive $image.Id $profile
    $images[$profile] = [ordered]@{local_tag=$tag; image_id=$image.Id; manifest_digest=$checked.manifest_digest; config_digest=$checked.config_digest; archive=(Split-Path -Leaf $archive); archive_sha256=$checked.archive_sha256; archive_bytes=$checked.archive_bytes; layer_count=$checked.layer_count; expanded_layer_bytes=$checked.expanded_layer_bytes; producer_reference=$checked.producer_reference; raw_archive=$checked.raw_archive; raw_archive_sha256=$checked.raw_archive_sha256; protocol_validated=$checked.protocol_validated}
    $container = "utf-preparation-receipts-$identity-$profile"
    try {
        $null = Invoke-Engine -EngineArguments @('create','--pull=never','--name',$container,'--entrypoint=/bin/true',$image.Id)
        $null = Invoke-Engine -EngineArguments @('cp',"${container}:/usr/share/utf-preparation",(Join-Path $output "receipts/$profile"))
    } finally {
        # Remote cleanup is not allowed to run past the shared deadline. An orphan
        # remains explicitly inspectable by this run's unique name after failure.
        if ([DateTime]::UtcNow -lt $deadline) { $null = Invoke-Engine -EngineArguments @('rm','--force',$container) }
    }
    if ($profile -ne 'sdk') {
        $embedded = Join-Path $context "archives/$profile.oci.tar"
        Copy-Item -LiteralPath $archive -Destination $embedded
        if ((Get-FileHash -Algorithm SHA256 -LiteralPath $embedded).Hash.ToLowerInvariant() -cne $checked.archive_sha256) { throw 'Embedded runtime differs from its validated canonical archive.' }
    }
    if ($profile -eq 'aska') {
        $nginxRaw = Join-Path $output 'nginx.raw.oci.tar'
        $nginx = Join-Path $output 'nginx.oci.tar'
        Invoke-Bounded -EngineArguments @('save','--format=oci-archive','--output',$nginxRaw,$inputs.images.nginx)
        $nginxImage = (Invoke-Engine -EngineArguments @('image','inspect',$inputs.images.nginx) | ConvertFrom-Json)[0]
        $checked = Convert-Archive $nginxRaw $nginx $nginxImage.Id 'nginx'
        $embedded = Join-Path $context 'archives/nginx.oci.tar'
        Copy-Item -LiteralPath $nginx -Destination $embedded
        if ((Get-FileHash -Algorithm SHA256 -LiteralPath $embedded).Hash.ToLowerInvariant() -cne $checked.archive_sha256) { throw 'Embedded Nginx differs from its validated canonical archive.' }
        $images['nginx'] = [ordered]@{source_ref=$inputs.images.nginx; image_id=$nginxImage.Id; manifest_digest=$checked.manifest_digest; config_digest=$checked.config_digest; archive='nginx.oci.tar'; archive_sha256=$checked.archive_sha256; archive_bytes=$checked.archive_bytes; layer_count=$checked.layer_count; expanded_layer_bytes=$checked.expanded_layer_bytes; producer_reference=$checked.producer_reference; raw_archive=$checked.raw_archive; raw_archive_sha256=$checked.raw_archive_sha256; protocol_validated=$checked.protocol_validated}
        $archiveHashes = Get-ChildItem -LiteralPath (Join-Path $context 'archives') -File | Sort-Object Name | ForEach-Object { (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash.ToLowerInvariant() + '  ' + $_.Name }
        [IO.File]::WriteAllText((Join-Path $context 'archives/SHA256SUMS'), ($archiveHashes -join "`n") + "`n")
    }
}
$images | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'images.json') -Encoding utf8NoBOM
$sdk = $images.sdk.image_id
foreach ($profile in @('dotnet','react')) {
    Assert-Budget
    # The in-container process boundary also covers a blocked Node event loop.
    $smokeArguments = @('run','--rm','--pull=never','--network=none','--read-only','--user=65532:65532','--cap-drop=ALL','--security-opt=no-new-privileges','--pids-limit=512','--memory=12g','--memory-swap=12g','--cpus=6','--tmpfs=/workspace:rw,nodev,nosuid,mode=1777,size=1g','--tmpfs=/tmp:rw,nodev,nosuid,mode=1777,size=1g','--env=HOME=/workspace',('--env=PLAYWRIGHT_BROWSERS_PATH=/opt/utf-playwright-browsers/'+$profile),'--entrypoint=/usr/bin/timeout',$sdk,'--signal=TERM','--kill-after=5','270','/usr/local/bin/node','/opt/utf-preparation/smoke-browser.cjs',$profile)
    Invoke-Local -Executable $podman -Arguments (@('--connection', $Connection) + $smokeArguments) -Seconds 300 -Poll { Assert-Budget }
    Assert-Budget
}
[IO.File]::WriteAllText((Join-Path $output 'PREPARATION-COMPLETE'), [DateTime]::UtcNow.ToString('O'))
Move-Item -LiteralPath (Join-Path $output 'PREPARATION-INCOMPLETE') -Destination (Join-Path $output 'PREPARATION-FINISHED')
Write-Host "Dependency preparation complete: $output"
Write-Host 'Local OCI identities are recorded; transport, publication and pipeline activation remain unapproved.'

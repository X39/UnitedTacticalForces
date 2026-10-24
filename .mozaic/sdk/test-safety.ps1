# Windows filesystem and process contracts. No Podman, builds or application CI.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'safety.ps1')
if (-not $IsWindows) { throw 'These contracts require actual Windows junction and rename behavior.' }
$root = Join-Path ([IO.Path]::GetTempPath()) ('utf-sdk-guards-' + [Guid]::NewGuid().ToString('N'))
$null = [IO.Directory]::CreateDirectory($root)
$repo = Join-Path $root 'repository'
$parent = Join-Path $root 'external'
$null = [IO.Directory]::CreateDirectory($repo)
$null = [IO.Directory]::CreateDirectory($parent)
$junction = Join-Path $root 'junction'
$jobs = @()
$passed = 0
function Expect-Rejection([scriptblock]$Action) {
    $rejected = $false
    try { & $Action | Out-Null } catch { $rejected = $true }
    if (-not $rejected) { throw 'Expected rejection did not occur.' }
    $script:passed += 1
}
try {
    Expect-Rejection { New-ExclusiveOutput (Join-Path $repo 'inside') $repo }
    Expect-Rejection { New-ExclusiveOutput 'relative-output' $repo }
    $existing = Join-Path $parent 'existing'
    $null = [IO.Directory]::CreateDirectory($existing)
    [IO.File]::WriteAllText((Join-Path $existing 'sentinel'), 'preserve')
    Expect-Rejection { New-ExclusiveOutput $existing $repo }
    if ([IO.File]::ReadAllText((Join-Path $existing 'sentinel')) -cne 'preserve') { throw 'Existing output was altered.' }
    $null = New-Item -ItemType Junction -Path $junction -Target $repo
    Expect-Rejection { New-ExclusiveOutput (Join-Path $junction 'escape') $repo }
    if (Test-Path -LiteralPath (Join-Path $repo 'escape')) { throw 'Junction bypass wrote into the repository.' }
    Remove-Item -LiteralPath $junction

    # Two independently started PowerShell processes race to claim one destination.
    $race = Join-Path $parent 'race'
    $gate = Join-Path $root 'race-start'
    foreach ($attempt in 1..2) {
        $jobs += Start-Job -ScriptBlock {
            param($Helper, $Destination, $Repository, $Gate)
            $ErrorActionPreference = 'Stop'
            . $Helper
            $deadline = [DateTime]::UtcNow.AddSeconds(10)
            while (-not [IO.File]::Exists($Gate)) {
                if ([DateTime]::UtcNow -gt $deadline) { throw 'Race test startup timed out.' }
                Start-Sleep -Milliseconds 20
            }
            try { $null = New-ExclusiveOutput $Destination $Repository; 'CLAIMED' } catch { 'REJECTED' }
        } -ArgumentList (Join-Path $PSScriptRoot 'safety.ps1'),$race,$repo,$gate
    }
    [IO.File]::WriteAllText($gate, 'start')
    $null = Wait-Job -Job $jobs -Timeout 20
    if (@($jobs | Where-Object State -ne 'Completed').Count) { throw 'Output claim race did not finish.' }
    $results = @($jobs | Receive-Job)
    if (@($results | Where-Object { $_ -ceq 'CLAIMED' }).Count -ne 1 -or
        @($results | Where-Object { $_ -ceq 'REJECTED' }).Count -ne 1) { throw 'Concurrent output claim did not yield exactly one winner.' }
    $passed += 1

    $good = @{
        host = @{os='linux';arch='amd64';cgroupVersion='v2';cgroupManager='systemd';cgroupControllers=@('cpu','memory','pids');security=@{rootless=$true};
            idMappings=@{uidmap=@(@{container_id=0;host_id=1000;size=1});gidmap=@(@{container_id=0;host_id=1000;size=1})};
            remoteSocket=@{path='unix:///run/user/1000/podman/podman.sock'}}
    }
    $connections = @(@{Name='mozaic-release-2a599af';URI='ssh://user@127.0.0.1:53487/run/user/1000/podman/podman.sock';Identity=(Join-Path $root 'reviewed-key')})
    Assert-RootlessEngine $good $connections 'mozaic-release-2a599af'
    $passed += 1
    foreach ($mutation in @('rootful','wrong-os','wrong-arch','v1','cgroupfs','missing-controller','uid-root','gid-root','socket-root')) {
        $bad = $good | ConvertTo-Json -Depth 10 | ConvertFrom-Json
        switch ($mutation) {
            rootful { $bad.host.security.rootless=$false }
            wrong-os { $bad.host.os='windows' }
            wrong-arch { $bad.host.arch='arm64' }
            v1 { $bad.host.cgroupVersion='v1' }
            cgroupfs { $bad.host.cgroupManager='cgroupfs' }
            missing-controller { $bad.host.cgroupControllers=@('cpu','pids') }
            uid-root { $bad.host.idMappings.uidmap[0].host_id=0 }
            gid-root { $bad.host.idMappings.gidmap[0].host_id=0 }
            socket-root { $bad.host.remoteSocket.path='unix:///run/podman/podman.sock' }
        }
        Expect-Rejection { Assert-RootlessEngine $bad $connections 'mozaic-release-2a599af' }
    }
    Expect-Rejection { Assert-RootlessEngine $good @() 'mozaic-release-2a599af' }
    Expect-Rejection { Assert-RootlessEngine $good @(@{Name='mozaic-release-2a599af';URI='ssh://root@127.0.0.1:53487/run/podman/podman.sock'}) 'mozaic-release-2a599af' }
    $machine = @{RemoteUsername='user';Port=53487;IdentityPath=(Join-Path $root 'reviewed-key')}
    Assert-MachineBinding $machine $connections 'mozaic-release-2a599af'
    $passed += 1
    foreach ($field in @('Port','RemoteUsername','IdentityPath')) {
        $bad = $machine.Clone()
        $bad[$field] = if ($field -eq 'Port') { 1 } elseif ($field -eq 'RemoteUsername') { 'root' } else { Join-Path $root 'other-key' }
        Expect-Rejection { Assert-MachineBinding $bad $connections 'mozaic-release-2a599af' }
    }
    if ((ConvertTo-MachinePath 'A:\releases\plain context') -cne '/mnt/a/releases/plain context') { throw 'WSL path mapping changed.' }
    $passed += 1
    Expect-Rejection { ConvertTo-MachinePath '\\server\share\context' }
    if ((ConvertTo-PosixArgument "with ' quote") -cne "'with '\'' quote'") { throw 'Native command argument quoting changed.' }
    $passed += 1

    $pwsh = (Get-Process -Id $PID).Path
    $value = Invoke-DeadlineProcess -Executable $pwsh -Arguments @('-NoProfile','-Command','[Console]::Write("probe")') -Deadline ([DateTime]::UtcNow.AddSeconds(20)) -LogPrefix (Join-Path $root 'success') -Capture
    if ($value -cne 'probe') { throw 'Captured metadata was not preserved.' }
    $passed += 1
    $timer = [Diagnostics.Stopwatch]::StartNew()
    Expect-Rejection {
        Invoke-DeadlineProcess -Executable $pwsh -Arguments @('-NoProfile','-Command','Start-Sleep -Seconds 60') -Deadline ([DateTime]::UtcNow.AddSeconds(20)) -CallSeconds 0.5 -LogPrefix (Join-Path $root 'hung-info')
    }
    if ($timer.Elapsed.TotalSeconds -gt 8) { throw 'Hung metadata call defeated its timeout.' }
    $timer.Restart()
    Expect-Rejection {
        Invoke-DeadlineProcess -Executable $pwsh -Arguments @('-NoProfile','-Command','Start-Sleep -Seconds 60') -Deadline ([DateTime]::UtcNow.AddMilliseconds(500)) -CallSeconds 60 -LogPrefix (Join-Path $root 'overall-deadline')
    }
    if ($timer.Elapsed.TotalSeconds -gt 8) { throw 'Overall deadline was not respected.' }
    $timer.Restart()
    Expect-Rejection {
        Invoke-DeadlineProcess -Executable $pwsh -Arguments @('-NoProfile','-Command','Start-Sleep -Seconds 60') -Deadline ([DateTime]::UtcNow.AddSeconds(10)) -LogPrefix (Join-Path $root 'outer-build') -OnPoll {
            Invoke-DeadlineProcess -Executable $pwsh -Arguments @('-NoProfile','-Command','Start-Sleep -Seconds 60') -Deadline ([DateTime]::UtcNow.AddSeconds(5)) -CallSeconds 0.5 -LogPrefix (Join-Path $root 'hung-storage-poll')
        }
    }
    if ($timer.Elapsed.TotalSeconds -gt 9) { throw 'Hung storage poll defeated local cleanup.' }
    $childCommand = '$child=Start-Process -FilePath (Get-Process -Id $PID).Path -ArgumentList @("-NoProfile","-Command","Start-Sleep -Seconds 60") -WindowStyle Hidden -PassThru; [Console]::WriteLine($child.Id); Start-Sleep -Seconds 60'
    Expect-Rejection {
        Invoke-DeadlineProcess -Executable $pwsh -Arguments @('-NoProfile','-Command',$childCommand) -Deadline ([DateTime]::UtcNow.AddSeconds(10)) -CallSeconds 2 -LogPrefix (Join-Path $root 'child-tree')
    }
    $childId = [int]([IO.File]::ReadAllText((Join-Path $root 'child-tree.stdout')).Trim())
    if ($childId -lt 1 -or (Get-Process -Id $childId -ErrorAction SilentlyContinue)) { throw 'The local child process was not reaped.' }
    Expect-Rejection {
        Invoke-DeadlineProcess -Executable $pwsh -Arguments @('-NoProfile','-Command','exit 7') -Deadline ([DateTime]::UtcNow.AddSeconds(20)) -LogPrefix (Join-Path $root 'failure')
    }
    Write-Output "PASS: $passed Windows output/engine/process contracts (including actual junction and concurrent claim)."
} finally {
    if ($jobs.Count) { $jobs | Stop-Job; $jobs | Remove-Job -Force }
    if (Test-Path -LiteralPath $junction) { Remove-Item -LiteralPath $junction }
    $resolvedRoot = [IO.Path]::GetFullPath($root)
    $expectedParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\utf-sdk-guards-'
    if (-not $resolvedRoot.StartsWith($expectedParent, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected contract-test cleanup path.' }
    Assert-NoReparseAncestry $resolvedRoot
    [IO.Directory]::Delete($resolvedRoot, $true)
}

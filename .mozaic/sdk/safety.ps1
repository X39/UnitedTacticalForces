# Source-only primitives, shared with the contract tests. No engine operations.
Set-StrictMode -Version Latest

function Assert-NoReparseAncestry([string]$Path) {
    $entry = [IO.DirectoryInfo]::new($Path)
    while ($null -ne $entry) {
        if (-not $entry.Exists -or ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'Output and repository ancestry must be existing directories without reparse points.'
        }
        $entry = $entry.Parent
    }
}

function New-ExclusiveOutput([string]$Value, [string]$Repository) {
    if (-not $IsWindows -or -not [IO.Path]::IsPathFullyQualified($Value)) {
        throw 'Preparation requires Windows and an absolute output path.'
    }
    $path = [IO.Path]::GetFullPath($Value).TrimEnd([IO.Path]::DirectorySeparatorChar)
    $repo = [IO.Path]::GetFullPath($Repository).TrimEnd([IO.Path]::DirectorySeparatorChar)
    Assert-NoReparseAncestry $repo
    $parent = [IO.Path]::GetDirectoryName($path)
    Assert-NoReparseAncestry $parent
    $resolvedParent = (Resolve-Path -LiteralPath $parent).ProviderPath
    if (-not $resolvedParent.Equals($parent, [StringComparison]::OrdinalIgnoreCase) -or
        $path.Equals($repo, [StringComparison]::OrdinalIgnoreCase) -or
        $path.StartsWith($repo + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Output must resolve outside the repository through a plain existing parent.'
    }
    # Directory.Move is an atomic no-replace rename on Windows, unlike CreateDirectory.
    # Nothing is written into the destination until this exclusive claim succeeds.
    $temporary = Join-Path $parent ('.utf-output-claim-' + [Guid]::NewGuid().ToString('N'))
    $null = [IO.Directory]::CreateDirectory($temporary)
    try {
        Assert-NoReparseAncestry $temporary
        if ([IO.Directory]::EnumerateFileSystemEntries($temporary).GetEnumerator().MoveNext()) { throw 'Temporary output claim is not empty.' }
        [IO.Directory]::Move($temporary, $path)
        Assert-NoReparseAncestry $path
        return $path
    } finally {
        # Never recursively remove a contested or nonempty directory.
        if ([IO.Directory]::Exists($temporary)) { [IO.Directory]::Delete($temporary, $false) }
    }
}

function Assert-RootlessEngine($Information, $Connections, [string]$Name) {
    $hostInfo = $Information.host
    if ($hostInfo.security.rootless -isnot [bool] -or -not $hostInfo.security.rootless -or
        $hostInfo.os -cne 'linux' -or $hostInfo.arch -cne 'amd64' -or
        $hostInfo.cgroupVersion -cne 'v2' -or $hostInfo.cgroupManager -cne 'systemd') {
        throw 'Preparation requires the inspected Linux amd64 rootless systemd/cgroup-v2 engine.'
    }
    foreach ($controller in @('cpu','memory','pids')) {
        if ($controller -notin $hostInfo.cgroupControllers) { throw 'The release engine lacks a required resource controller.' }
    }
    foreach ($kind in @('uidmap','gidmap')) {
        $mapping = @($hostInfo.idMappings.$kind | Where-Object { $_.container_id -eq 0 })
        if ($mapping.Count -ne 1 -or $mapping[0].host_id -ne 1000 -or $mapping[0].size -ne 1) {
            throw 'The release VM must retain its reviewed unprivileged UID/GID 1000 mapping.'
        }
    }
    $selected = @($Connections | Where-Object { $_.Name -ceq $Name })
    if ($selected.Count -ne 1) { throw 'The explicit release connection is absent or ambiguous.' }
    $uri = [Uri]$selected[0].URI
    if ($uri.Scheme -cne 'ssh' -or $uri.UserInfo -cne 'user' -or $uri.Host -cne '127.0.0.1' -or
        $uri.AbsolutePath -cne '/run/user/1000/podman/podman.sock' -or
        $hostInfo.remoteSocket.path -cne 'unix:///run/user/1000/podman/podman.sock') {
        throw 'The selected connection does not match the reviewed release VM user socket.'
    }
}

function Assert-MachineBinding($SshConfiguration, $Connections, [string]$Name) {
    $selected = @($Connections | Where-Object { $_.Name -ceq $Name })
    if ($selected.Count -ne 1) { throw 'The release connection is absent or ambiguous.' }
    $uri = [Uri]$selected[0].URI
    if ($SshConfiguration.RemoteUsername -cne 'user' -or $SshConfiguration.Port -ne $uri.Port -or
        -not [IO.Path]::GetFullPath($SshConfiguration.IdentityPath).Equals([IO.Path]::GetFullPath($selected[0].Identity), [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Machine SSH and the inspected remote connection do not share the reviewed identity and endpoint.'
    }
}

function ConvertTo-MachinePath([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path)
    if ($full -notmatch '^[A-Za-z]:\\' -or $full.Contains("`n") -or $full.Contains("`r")) {
        throw 'Native preparation requires an existing WSL drive mount, not a UNC or special path.'
    }
    return '/mnt/' + $full.Substring(0,1).ToLowerInvariant() + '/' + $full.Substring(3).Replace('\','/')
}

function ConvertTo-PosixArgument([string]$Value) {
    return "'" + $Value.Replace("'", "'\''") + "'"
}

function Invoke-DeadlineProcess {
    param(
        [Parameter(Mandatory)][string]$Executable,
        [Parameter(Mandatory)][string[]]$Arguments,
        [Parameter(Mandatory)][DateTime]$Deadline,
        [Parameter(Mandatory)][string]$LogPrefix,
        [double]$CallSeconds = 30,
        [scriptblock]$OnPoll,
        [switch]$Capture
    )
    $limit = [DateTime]::UtcNow.AddSeconds($CallSeconds)
    if ($Deadline -lt $limit) { $limit = $Deadline }
    if ([DateTime]::UtcNow -ge $limit) { throw 'Preparation deadline reached before starting a subprocess.' }
    $info = [Diagnostics.ProcessStartInfo]::new($Executable)
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    $stdout = [IO.File]::Open($LogPrefix + '.stdout', 'CreateNew', 'Write', 'Read')
    $stderr = [IO.File]::Open($LogPrefix + '.stderr', 'CreateNew', 'Write', 'Read')
    $started = $false
    try {
        $null = $process.Start()
        $started = $true
        $outCopy = $process.StandardOutput.BaseStream.CopyToAsync($stdout)
        $errCopy = $process.StandardError.BaseStream.CopyToAsync($stderr)
        $nextPoll = [DateTime]::UtcNow.AddSeconds(2.5)
        while (-not $process.WaitForExit(100)) {
            if ([DateTime]::UtcNow -ge $limit) { throw 'Preparation subprocess exceeded its deadline.' }
            if ($OnPoll -and [DateTime]::UtcNow -ge $nextPoll) {
                & $OnPoll
                $nextPoll = [DateTime]::UtcNow.AddSeconds(2.5)
            }
        }
        # A child inheriting pipe handles must not turn output draining into an unbounded wait.
        if (-not $outCopy.Wait(2000) -or -not $errCopy.Wait(2000)) { throw 'Subprocess output did not close within the cleanup bound.' }
        if ($process.ExitCode -ne 0) { throw 'Preparation subprocess failed; inspect its private local log.' }
        if ([DateTime]::UtcNow -gt $limit) { throw 'Preparation subprocess exceeded its deadline.' }
    } finally {
        if ($started -and -not $process.HasExited) {
            $process.Kill($true)
            $null = $process.WaitForExit(5000)
        }
        if ($started) {
            $process.StandardOutput.Dispose()
            $process.StandardError.Dispose()
        }
        $stdout.Dispose()
        $stderr.Dispose()
        $process.Dispose()
    }
    if ($Capture) {
        if ((Get-Item -LiteralPath ($LogPrefix + '.stdout')).Length -gt 16MB) { throw 'Subprocess metadata exceeds the capture bound.' }
        return [IO.File]::ReadAllText($LogPrefix + '.stdout')
    }
}

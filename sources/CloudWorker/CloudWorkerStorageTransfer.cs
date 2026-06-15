using System.IO.Compression;
using System.Net.Http.Headers;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>
/// Transfers API-owned persistent data over the command-bound worker protocol.
/// </summary>
public sealed class CloudWorkerStorageTransfer(
    HttpClient httpClient,
    CloudWorkerCredentialStore credentialStore,
    CloudWorkerConnectionConfig connection,
    string agentStateDirectory) : ICloudWorkerStorageTransfer
{
    private const int PersistentOverlayManifestVersion = 1;
    private const int MaximumPlanBytes = 1024 * 1024;
    private const string PersistentOverlayManifestFileName = ".utf-persistent-overlay-manifest.json";
    private static readonly StringComparer ProtocolPathComparer = StringComparer.OrdinalIgnoreCase;
    private readonly string _persistentOverlayStateDirectory = Path.Combine(
        Path.GetFullPath(agentStateDirectory),
        "persistent-overlays",
        connection.DeploymentId.ToString("N"));

    public async Task ResetPersistentOverlayAsync(
        string deploymentRoot,
        CancellationToken cancellationToken)
    {
        var root = PrepareDeploymentRoot(deploymentRoot);
        var manifest = await ReadPersistentOverlayManifestAsync(cancellationToken).ConfigureAwait(false);
        DeleteTrackedEntries(root, manifest.Entries, cancellationToken);
        await WritePersistentOverlayManifestAsync(
                CreatePersistentOverlayManifest([]),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task ApplyPersistentSnapshotAsync(
        WorkerCommandClaimResponse command,
        string deploymentRoot,
        CancellationToken cancellationToken)
    {
        command.Validate(connection);
        var root = PrepareDeploymentRoot(deploymentRoot);
        using var response = await SendAsync(
                HttpMethod.Get,
                command.CommandId,
                "persistent-snapshot",
                null,
                WorkerStorageProtocol.ArchiveMediaType,
                cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response, "persistent snapshot", cancellationToken).ConfigureAwait(false);
        if (!string.Equals(
                response.Content.Headers.ContentType?.MediaType,
                WorkerStorageProtocol.ArchiveMediaType,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Persistent snapshot returned an unexpected content type.");
        }
        if (response.Content.Headers.ContentLength is > WorkerStorageProtocol.MaximumArchiveBytes)
            throw new InvalidDataException("Persistent snapshot exceeds the archive size limit.");

        await using var archive = new MemoryStream();
        await using (var responseStream = await response.Content
                         .ReadAsStreamAsync(cancellationToken)
                         .ConfigureAwait(false))
        {
            await CopyBoundedAsync(
                    responseStream,
                    archive,
                    WorkerStorageProtocol.MaximumArchiveBytes,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        archive.Position = 0;

        var stagingRoot = Path.Combine(
            PreparePersistentOverlayStateDirectory(),
            $"snapshot-{command.CommandId:N}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingRoot);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                stagingRoot,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        try
        {
            var entries = await ValidateAndExtractSnapshotAsync(
                    command,
                    archive,
                    stagingRoot,
                    cancellationToken)
                .ConfigureAwait(false);
            var previous = await ReadPersistentOverlayManifestAsync(cancellationToken).ConfigureAwait(false);
            var desired = entries
                .Select(q => new PersistentOverlayEntry(q.RelativePath, q.IsDirectory))
                .ToArray();
            ValidateUntrackedTypeConflicts(root, previous.Entries, desired);
            var recoveryEntries = MergeTrackedEntries(previous.Entries, desired);
            await WritePersistentOverlayManifestAsync(
                    CreatePersistentOverlayManifest(recoveryEntries),
                    cancellationToken)
                .ConfigureAwait(false);
            ReconcileStaleEntries(root, recoveryEntries, desired, cancellationToken);
            PrepareTrackedTypeTransitions(root, recoveryEntries, desired, cancellationToken);
            await ApplySnapshotAsync(stagingRoot, root, entries, cancellationToken).ConfigureAwait(false);
            await WritePersistentOverlayManifestAsync(
                    CreatePersistentOverlayManifest(desired),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            TryDeleteStaging(stagingRoot);
        }
    }

    public async Task PublishPreservationAsync(
        WorkerCommandClaimResponse command,
        string deploymentRoot,
        CancellationToken cancellationToken)
    {
        command.Validate(connection);
        var root = PrepareDeploymentRoot(deploymentRoot);
        var plan = await GetPreservationPlanAsync(command, cancellationToken).ConfigureAwait(false);
        await using var archive = await BuildPreservationArchiveAsync(
                command,
                plan,
                root,
                cancellationToken)
            .ConfigureAwait(false);
        using var content = new StreamContent(archive);
        content.Headers.ContentType = new MediaTypeHeaderValue(WorkerStorageProtocol.ArchiveMediaType);
        content.Headers.ContentLength = archive.Length;
        using var response = await SendAsync(
                HttpMethod.Post,
                command.CommandId,
                "preservation",
                content,
                "application/json",
                cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response, "preservation publication", cancellationToken).ConfigureAwait(false);
    }

    private async Task<WorkerPreservationPlanResponse> GetPreservationPlanAsync(
        WorkerCommandClaimResponse command,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
                HttpMethod.Get,
                command.CommandId,
                "preservation-plan",
                null,
                "application/json",
                cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response, "preservation plan", cancellationToken).ConfigureAwait(false);
        if (response.Content.Headers.ContentLength is > MaximumPlanBytes)
            throw new InvalidDataException("Preservation plan exceeds the response size limit.");
        await using var body = new MemoryStream();
        await using (var responseStream = await response.Content
                         .ReadAsStreamAsync(cancellationToken)
                         .ConfigureAwait(false))
        {
            await CopyBoundedAsync(responseStream, body, MaximumPlanBytes, cancellationToken).ConfigureAwait(false);
        }
        body.Position = 0;
        var plan = await JsonSerializer.DeserializeAsync<WorkerPreservationPlanResponse>(
                       body,
                       CloudWorkerJson.Options,
                       cancellationToken)
                   .ConfigureAwait(false)
                   ?? throw new InvalidDataException("Preservation plan response is empty.");
        ValidatePlan(plan, command);
        return plan;
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        Guid commandId,
        string operationPath,
        HttpContent? content,
        string accept,
        CancellationToken cancellationToken)
    {
        var credential = await ReadCurrentCredentialAsync(cancellationToken).ConfigureAwait(false);
        using var request = new HttpRequestMessage(
            method,
            BuildStorageUri(commandId, operationPath));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
        request.Content = content;
        return await httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<string> ReadCurrentCredentialAsync(CancellationToken cancellationToken)
    {
        var stored = await credentialStore.ReadAsync(cancellationToken).ConfigureAwait(false)
                     ?? throw new InvalidOperationException("Worker credential is not available for storage transfer.");
        if (!ConnectionMatches(connection, stored.Connection))
            throw new InvalidOperationException("Persisted worker identity does not match storage transfer runtime.");
        return stored.Credential;
    }

    private Uri BuildStorageUri(Guid commandId, string operationPath)
    {
        var relativePath = $"{connection.WorkerApiPath.Trim('/')}/{connection.WorkerId:D}/commands/"
                           + $"{commandId:D}/storage/{operationPath}";
        return new Uri(
            new Uri(connection.ApiBaseUrl.EndsWith('/') ? connection.ApiBaseUrl : connection.ApiBaseUrl + "/"),
            relativePath.TrimStart('/'));
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (response.IsSuccessStatusCode)
            return;
        throw new HttpRequestException(
            $"Worker {operation} failed with HTTP {(int) response.StatusCode}.",
            null,
            response.StatusCode);
    }

    private static bool ConnectionMatches(
        CloudWorkerConnectionConfig expected,
        CloudWorkerConnectionConfig actual)
        => expected.WorkerId == actual.WorkerId
           && expected.DeploymentId == actual.DeploymentId
           && expected.ServerId == actual.ServerId
           && string.Equals(expected.ApiBaseUrl, actual.ApiBaseUrl, StringComparison.Ordinal)
           && string.Equals(expected.GameIdentifier, actual.GameIdentifier, StringComparison.Ordinal)
           && string.Equals(expected.WorkerApiPath, actual.WorkerApiPath, StringComparison.Ordinal);

    private static string PrepareDeploymentRoot(string deploymentRoot)
    {
        var root = Path.GetFullPath(deploymentRoot);
        Directory.CreateDirectory(root);
        WorkerStoragePath.RejectExistingLinks(root, root);
        return root;
    }

    private string PreparePersistentOverlayStateDirectory()
    {
        Directory.CreateDirectory(_persistentOverlayStateDirectory);
        WorkerStoragePath.RejectExistingLinks(
            _persistentOverlayStateDirectory,
            _persistentOverlayStateDirectory);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                _persistentOverlayStateDirectory,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        return _persistentOverlayStateDirectory;
    }

    private async Task<PersistentOverlayManifest> ReadPersistentOverlayManifestAsync(
        CancellationToken cancellationToken)
    {
        var stateDirectory = PreparePersistentOverlayStateDirectory();
        var path = Path.Combine(stateDirectory, PersistentOverlayManifestFileName);
        if (!File.Exists(path))
            return CreatePersistentOverlayManifest([]);
        WorkerStoragePath.RejectExistingLinks(stateDirectory, path);
        if (new FileInfo(path).Length > MaximumPlanBytes)
            throw new InvalidDataException("Persistent overlay manifest exceeds its size limit.");
        RestrictedFilePermissions.EnsureOwnerOnlyReadable(path, "Persistent overlay manifest");
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var manifest = await JsonSerializer.DeserializeAsync<PersistentOverlayManifest>(
                           stream,
                           CloudWorkerJson.Options,
                           cancellationToken)
                       .ConfigureAwait(false)
                       ?? throw new InvalidDataException("Persistent overlay manifest is empty.");
        if (manifest.Version != PersistentOverlayManifestVersion
            || manifest.ServerId != connection.ServerId
            || manifest.DeploymentId != connection.DeploymentId
            || manifest.Entries.Count > WorkerStorageProtocol.MaximumEntryCount)
        {
            throw new InvalidDataException("Persistent overlay manifest attribution or limits are invalid.");
        }

        var normalized = new Dictionary<string, PersistentOverlayEntry>(ProtocolPathComparer);
        foreach (var entry in manifest.Entries)
        {
            var pathValue = WorkerStoragePath.Normalize(entry.RelativePath, false);
            if (!string.Equals(pathValue, entry.RelativePath, StringComparison.Ordinal)
                || !normalized.TryAdd(pathValue, entry with { RelativePath = pathValue }))
            {
                throw new InvalidDataException("Persistent overlay manifest contains a duplicate or non-canonical path.");
            }
        }
        ValidateEntryTree(normalized.Values);
        return CreatePersistentOverlayManifest(normalized.Values.ToArray());
    }

    private async Task WritePersistentOverlayManifestAsync(
        PersistentOverlayManifest manifest,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(manifest, CloudWorkerJson.Options);
        if (Encoding.UTF8.GetByteCount(json) > MaximumPlanBytes)
            throw new InvalidDataException("Persistent overlay manifest exceeds its size limit.");
        var stateDirectory = PreparePersistentOverlayStateDirectory();
        var path = Path.Combine(stateDirectory, PersistentOverlayManifestFileName);
        WorkerStoragePath.RejectExistingLinks(stateDirectory, path);
        using var temporary = await RestrictedTemporaryFile
            .CreateAsync(
                stateDirectory,
                ".utf-persistent-overlay-manifest",
                ".tmp",
                json,
                cancellationToken)
            .ConfigureAwait(false);
        File.Move(temporary.Path, path, true);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    private PersistentOverlayManifest CreatePersistentOverlayManifest(
        IReadOnlyList<PersistentOverlayEntry> entries)
        => new()
        {
            ServerId = connection.ServerId,
            DeploymentId = connection.DeploymentId,
            Entries = entries
                .OrderBy(q => q.RelativePath, ProtocolPathComparer)
                .ToList(),
        };

    private static IReadOnlyList<PersistentOverlayEntry> MergeTrackedEntries(
        IReadOnlyList<PersistentOverlayEntry> previous,
        IReadOnlyList<PersistentOverlayEntry> desired)
    {
        var merged = previous.ToDictionary(q => q.RelativePath, ProtocolPathComparer);
        foreach (var entry in desired)
            merged[entry.RelativePath] = entry;
        return merged.Values.ToArray();
    }

    private static void ValidateUntrackedTypeConflicts(
        string deploymentRoot,
        IReadOnlyList<PersistentOverlayEntry> tracked,
        IReadOnlyList<PersistentOverlayEntry> desired)
    {
        var trackedPaths = tracked.Select(q => q.RelativePath).ToHashSet(ProtocolPathComparer);
        foreach (var entry in desired.Where(q => !trackedPaths.Contains(q.RelativePath)))
        {
            var target = WorkerStoragePath.ResolveWithinRoot(deploymentRoot, entry.RelativePath, false);
            if ((entry.IsDirectory && File.Exists(target))
                || (!entry.IsDirectory && Directory.Exists(target)))
            {
                throw new InvalidDataException("Persistent snapshot entry conflicts with untracked runtime content.");
            }
        }
    }

    private static void ReconcileStaleEntries(
        string deploymentRoot,
        IReadOnlyList<PersistentOverlayEntry> tracked,
        IReadOnlyList<PersistentOverlayEntry> desired,
        CancellationToken cancellationToken)
    {
        var desiredByPath = desired.ToDictionary(q => q.RelativePath, ProtocolPathComparer);
        var stale = tracked
            .Where(q => !desiredByPath.TryGetValue(q.RelativePath, out var current)
                        || current.IsDirectory != q.IsDirectory)
            .ToArray();
        DeleteTrackedEntries(deploymentRoot, stale, cancellationToken);
    }

    private static void PrepareTrackedTypeTransitions(
        string deploymentRoot,
        IReadOnlyList<PersistentOverlayEntry> tracked,
        IReadOnlyList<PersistentOverlayEntry> desired,
        CancellationToken cancellationToken)
    {
        var trackedPaths = tracked.Select(q => q.RelativePath).ToHashSet(ProtocolPathComparer);
        foreach (var entry in desired)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var target = WorkerStoragePath.ResolveWithinRoot(deploymentRoot, entry.RelativePath, false);
            if (entry.IsDirectory && File.Exists(target))
            {
                if (!trackedPaths.Contains(entry.RelativePath))
                    throw new InvalidDataException("Persistent snapshot directory conflicts with a runtime file.");
                File.Delete(target);
            }
            else if (!entry.IsDirectory && Directory.Exists(target))
            {
                if (!trackedPaths.Contains(entry.RelativePath)
                    || Directory.EnumerateFileSystemEntries(target).Any())
                {
                    throw new InvalidDataException("Persistent snapshot file conflicts with a runtime directory.");
                }
                Directory.Delete(target);
            }
        }
    }

    private static void DeleteTrackedEntries(
        string deploymentRoot,
        IReadOnlyCollection<PersistentOverlayEntry> entries,
        CancellationToken cancellationToken)
    {
        foreach (var entry in entries.OrderByDescending(q => PathDepth(q.RelativePath)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var target = WorkerStoragePath.ResolveWithinRoot(deploymentRoot, entry.RelativePath, false);
            if (File.Exists(target) && !Directory.Exists(target))
                File.Delete(target);
        }
        foreach (var entry in entries.OrderByDescending(q => PathDepth(q.RelativePath)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var target = WorkerStoragePath.ResolveWithinRoot(deploymentRoot, entry.RelativePath, false);
            if (Directory.Exists(target) && !Directory.EnumerateFileSystemEntries(target).Any())
                Directory.Delete(target);
        }
    }

    private static void ValidateEntryTree(IEnumerable<PersistentOverlayEntry> entries)
    {
        var files = entries
            .Where(q => !q.IsDirectory)
            .Select(q => q.RelativePath)
            .ToHashSet(ProtocolPathComparer);
        foreach (var entry in entries)
        {
            var index = entry.RelativePath.IndexOf('/');
            while (index >= 0)
            {
                if (files.Contains(entry.RelativePath[..index]))
                    throw new InvalidDataException("Persistent overlay manifest contains a file used as a directory.");
                index = entry.RelativePath.IndexOf('/', index + 1);
            }
        }
    }

    private async Task<IReadOnlyList<WorkerStorageArchiveEntry>> ValidateAndExtractSnapshotAsync(
        WorkerCommandClaimResponse command,
        Stream archive,
        string stagingRoot,
        CancellationToken cancellationToken)
    {
        using var zip = new ZipArchive(archive, ZipArchiveMode.Read, true, Encoding.UTF8);
        if (zip.Entries.Count > WorkerStorageProtocol.MaximumEntryCount + 1)
            throw new InvalidDataException("Persistent snapshot contains too many entries.");
        var manifestZipEntries = zip.Entries
            .Where(q => string.Equals(q.FullName, WorkerStorageProtocol.ManifestEntryName, StringComparison.Ordinal))
            .ToArray();
        if (manifestZipEntries.Length != 1 || manifestZipEntries[0].Length > MaximumPlanBytes)
            throw new InvalidDataException("Persistent snapshot manifest is missing or invalid.");

        WorkerStorageArchiveManifest manifest;
        await using (var manifestStream = manifestZipEntries[0].Open())
        {
            manifest = await JsonSerializer.DeserializeAsync<WorkerStorageArchiveManifest>(
                           manifestStream,
                           CloudWorkerJson.Options,
                           cancellationToken)
                       .ConfigureAwait(false)
                       ?? throw new InvalidDataException("Persistent snapshot manifest is empty.");
        }
        ValidateSnapshotManifest(manifest, command);

        var expectedByPath = new Dictionary<string, WorkerStorageArchiveEntry>(ProtocolPathComparer);
        long expectedBytes = 0;
        foreach (var entry in manifest.Entries)
        {
            var normalized = ValidateManifestEntry(entry);
            if (!string.Equals(normalized, entry.RelativePath, StringComparison.Ordinal)
                || !expectedByPath.TryAdd(normalized, entry with { RelativePath = normalized }))
            {
                throw new InvalidDataException("Persistent snapshot manifest contains a duplicate or non-canonical path.");
            }
            if (!entry.IsDirectory)
            {
                expectedBytes = checked(expectedBytes + entry.Length);
                EnsureExpandedLength(expectedBytes);
            }
        }
        EnsureEntryCount(expectedByPath.Count);
        ValidateEntryTree(expectedByPath.Values.Select(q =>
            new PersistentOverlayEntry(q.RelativePath, q.IsDirectory)));

        var actualByPath = MapArchiveEntries(zip);
        if (actualByPath.Count != expectedByPath.Count)
            throw new InvalidDataException("Persistent snapshot archive is incomplete.");
        foreach (var (path, expected) in expectedByPath)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!actualByPath.TryGetValue(path, out var zipEntry))
                throw new InvalidDataException("Persistent snapshot is missing a declared entry.");
            var isDirectory = zipEntry.FullName.EndsWith("/", StringComparison.Ordinal);
            if (isDirectory != expected.IsDirectory)
                throw new InvalidDataException("Persistent snapshot entry type does not match its manifest.");
            var destination = WorkerStoragePath.ResolveWithinRoot(stagingRoot, path, false);
            if (isDirectory)
            {
                Directory.CreateDirectory(destination);
                continue;
            }
            if (zipEntry.Length != expected.Length)
                throw new InvalidDataException("Persistent snapshot entry length does not match its manifest.");
            var parent = Path.GetDirectoryName(destination)
                         ?? throw new InvalidDataException("Persistent snapshot path has no parent directory.");
            Directory.CreateDirectory(parent);
            await using var input = zipEntry.Open();
            await using var output = new FileStream(
                destination,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.WriteThrough);
            var (length, hash) = await CopyWithHashAsync(
                    input,
                    output,
                    WorkerStorageProtocol.MaximumFileBytes,
                    cancellationToken)
                .ConfigureAwait(false);
            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            if (length != expected.Length || !HashMatches(expected.Sha256, hash))
                throw new InvalidDataException("Persistent snapshot entry failed its integrity check.");
        }
        return expectedByPath.Values.ToArray();
    }

    private static Dictionary<string, ZipArchiveEntry> MapArchiveEntries(ZipArchive zip)
    {
        var entries = new Dictionary<string, ZipArchiveEntry>(ProtocolPathComparer);
        foreach (var entry in zip.Entries.Where(q =>
                     !string.Equals(q.FullName, WorkerStorageProtocol.ManifestEntryName, StringComparison.Ordinal)))
        {
            RejectArchiveLink(entry);
            if (!entry.FullName.StartsWith(WorkerStorageProtocol.FileEntryPrefix, StringComparison.Ordinal)
                || entry.FullName.Contains('\\'))
            {
                throw new InvalidDataException("Worker storage archive contains an invalid entry name.");
            }
            var rawPath = entry.FullName[WorkerStorageProtocol.FileEntryPrefix.Length..].TrimEnd('/');
            var normalized = WorkerStoragePath.Normalize(rawPath, false);
            if (!string.Equals(rawPath, normalized, StringComparison.Ordinal)
                || !entries.TryAdd(normalized, entry))
            {
                throw new InvalidDataException("Worker storage archive contains a duplicate or non-canonical entry.");
            }
        }
        return entries;
    }

    private static async Task ApplySnapshotAsync(
        string stagingRoot,
        string deploymentRoot,
        IReadOnlyCollection<WorkerStorageArchiveEntry> entries,
        CancellationToken cancellationToken)
    {
        foreach (var entry in entries
                     .Where(q => q.IsDirectory)
                     .OrderBy(q => PathDepth(q.RelativePath)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var target = WorkerStoragePath.ResolveWithinRoot(deploymentRoot, entry.RelativePath, false);
            if (File.Exists(target) && !Directory.Exists(target))
                throw new InvalidDataException("Persistent snapshot directory conflicts with a runtime file.");
            Directory.CreateDirectory(target);
            WorkerStoragePath.RejectExistingLinks(deploymentRoot, target);
            EnsureRuntimeDirectoryPermissions(deploymentRoot, target);
        }
        foreach (var entry in entries.Where(q => !q.IsDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = WorkerStoragePath.ResolveWithinRoot(stagingRoot, entry.RelativePath, false);
            var target = WorkerStoragePath.ResolveWithinRoot(deploymentRoot, entry.RelativePath, false);
            if (Directory.Exists(target))
                throw new InvalidDataException("Persistent snapshot file conflicts with a runtime directory.");
            var parent = Path.GetDirectoryName(target)
                         ?? throw new InvalidDataException("Persistent snapshot path has no parent directory.");
            Directory.CreateDirectory(parent);
            WorkerStoragePath.RejectExistingLinks(deploymentRoot, parent);
            EnsureRuntimeDirectoryPermissions(deploymentRoot, parent);
            var temporary = Path.Combine(parent, $".utf-storage-{Guid.NewGuid():N}.tmp");
            try
            {
                await CopyFileAsync(source, temporary, cancellationToken).ConfigureAwait(false);
                File.Move(temporary, target, true);
                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(
                        target,
                        UnixFileMode.UserRead
                        | UnixFileMode.UserWrite
                        | UnixFileMode.GroupRead
                        | UnixFileMode.GroupWrite);
                }
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }
    }

    private static void EnsureRuntimeDirectoryPermissions(string deploymentRoot, string directory)
    {
        if (OperatingSystem.IsWindows())
            return;
        var relative = Path.GetRelativePath(deploymentRoot, directory);
        var current = deploymentRoot;
        SetRuntimeDirectoryMode(current);
        if (relative == ".")
            return;
        foreach (var segment in relative.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            SetRuntimeDirectoryMode(current);
        }
    }

    [UnsupportedOSPlatform("windows")]
    private static void SetRuntimeDirectoryMode(string path)
        => File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead
            | UnixFileMode.UserWrite
            | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead
            | UnixFileMode.GroupWrite
            | UnixFileMode.GroupExecute
            | UnixFileMode.SetGroup);

    private async Task<MemoryStream> BuildPreservationArchiveAsync(
        WorkerCommandClaimResponse command,
        WorkerPreservationPlanResponse plan,
        string deploymentRoot,
        CancellationToken cancellationToken)
    {
        var localEntries = EnumerateSelectedEntries(deploymentRoot, plan.Selections, cancellationToken);
        EnsureEntryCount(localEntries.Count);
        var output = new MemoryStream();
        var manifestEntries = new List<WorkerStorageArchiveEntry>(localEntries.Count);
        long expandedBytes = 0;
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true, Encoding.UTF8))
        {
            foreach (var localEntry in localEntries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var zipEntry = zip.CreateEntry(
                    ArchiveEntryName(localEntry.RelativePath, localEntry.IsDirectory),
                    localEntry.IsDirectory ? CompressionLevel.NoCompression : CompressionLevel.Fastest);
                if (localEntry.IsDirectory)
                {
                    manifestEntries.Add(new WorkerStorageArchiveEntry
                    {
                        RelativePath = localEntry.RelativePath,
                        IsDirectory = true,
                    });
                    continue;
                }
                await using var destination = zipEntry.Open();
                await using var source = new FileStream(
                    localEntry.FullPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    81920,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                var (length, hash) = await CopyWithHashAsync(
                        source,
                        destination,
                        WorkerStorageProtocol.MaximumFileBytes,
                        cancellationToken)
                    .ConfigureAwait(false);
                expandedBytes = checked(expandedBytes + length);
                EnsureExpandedLength(expandedBytes);
                manifestEntries.Add(new WorkerStorageArchiveEntry
                {
                    RelativePath = localEntry.RelativePath,
                    Length = length,
                    Sha256 = hash,
                });
            }
            var manifest = new WorkerStorageArchiveManifest
            {
                Kind = EWorkerStorageArchiveKind.PreservationUpload,
                ServerId = connection.ServerId,
                DeploymentId = connection.DeploymentId,
                WorkerId = connection.WorkerId,
                CommandId = command.CommandId,
                PreservationPlanToken = plan.PlanToken,
                Entries = manifestEntries,
            };
            var manifestEntry = zip.CreateEntry(WorkerStorageProtocol.ManifestEntryName, CompressionLevel.Fastest);
            await using var manifestStream = manifestEntry.Open();
            await JsonSerializer.SerializeAsync(
                    manifestStream,
                    manifest,
                    CloudWorkerJson.Options,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        if (output.Length > WorkerStorageProtocol.MaximumArchiveBytes)
        {
            output.Dispose();
            throw new InvalidDataException("Preservation archive exceeds the request size limit.");
        }
        output.Position = 0;
        return output;
    }

    private static List<LocalStorageEntry> EnumerateSelectedEntries(
        string deploymentRoot,
        IReadOnlyList<WorkerPreservationSelection> selections,
        CancellationToken cancellationToken)
    {
        var entries = new Dictionary<string, LocalStorageEntry>(ProtocolPathComparer);
        foreach (var selection in selections)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var selectedPath = WorkerStoragePath.Normalize(
                selection.RelativePath,
                selection.Scope is not EPreservationScope.File);
            var selectedFullPath = WorkerStoragePath.ResolveWithinRoot(
                deploymentRoot,
                selectedPath,
                selection.Scope is not EPreservationScope.File);
            switch (selection.Scope)
            {
                case EPreservationScope.File:
                    if (File.Exists(selectedFullPath))
                        AddLocalEntry(entries, deploymentRoot, selectedFullPath, false);
                    break;
                case EPreservationScope.FlatFolder:
                    if (!Directory.Exists(selectedFullPath))
                        break;
                    foreach (var path in Directory.EnumerateFiles(selectedFullPath, "*", SearchOption.TopDirectoryOnly))
                    {
                        WorkerStoragePath.RejectLink(path);
                        AddLocalEntry(entries, deploymentRoot, path, false);
                    }
                    break;
                case EPreservationScope.RecursiveFolder:
                    if (File.Exists(selectedFullPath))
                    {
                        AddLocalEntry(entries, deploymentRoot, selectedFullPath, false);
                    }
                    else if (Directory.Exists(selectedFullPath))
                    {
                        if (selectedPath.Length > 0)
                            AddLocalEntry(entries, deploymentRoot, selectedFullPath, true);
                        EnumerateRecursive(entries, deploymentRoot, selectedFullPath, cancellationToken);
                    }
                    break;
                default:
                    throw new InvalidDataException("Preservation plan contains an unknown selection scope.");
            }
            EnsureEntryCount(entries.Count);
        }
        return entries.Values.OrderBy(q => q.RelativePath, ProtocolPathComparer).ToList();
    }

    private static void EnumerateRecursive(
        IDictionary<string, LocalStorageEntry> entries,
        string deploymentRoot,
        string selectedRoot,
        CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        pending.Push(selectedRoot);
        while (pending.TryPop(out var directory))
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(directory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("A preservation selection contains a symbolic link.");
                var isDirectory = (attributes & FileAttributes.Directory) != 0;
                try
                {
                    AddLocalEntry(entries, deploymentRoot, path, isDirectory);
                }
                catch (InvalidDataException) when (IsProtectedMetadataPath(deploymentRoot, path))
                {
                    continue;
                }
                EnsureEntryCount(entries.Count);
                if (isDirectory)
                    pending.Push(path);
            }
        }
    }

    private static bool IsProtectedMetadataPath(string deploymentRoot, string path)
    {
        var relative = Path.GetRelativePath(deploymentRoot, path).Replace('\\', '/');
        return relative.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment =>
            segment.StartsWith(".utf-", StringComparison.OrdinalIgnoreCase)
            || segment.StartsWith(".env", StringComparison.OrdinalIgnoreCase)
            || segment.Contains("credential", StringComparison.OrdinalIgnoreCase)
            || segment.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || segment.Contains("provider-token", StringComparison.OrdinalIgnoreCase));
    }

    private static void AddLocalEntry(
        IDictionary<string, LocalStorageEntry> entries,
        string deploymentRoot,
        string fullPath,
        bool isDirectory)
    {
        WorkerStoragePath.RejectLink(fullPath);
        var relativePath = WorkerStoragePath.Normalize(
            Path.GetRelativePath(deploymentRoot, fullPath).Replace('\\', '/'),
            false);
        entries.TryAdd(relativePath, new LocalStorageEntry(fullPath, relativePath, isDirectory));
    }

    private void ValidatePlan(
        WorkerPreservationPlanResponse plan,
        WorkerCommandClaimResponse command)
    {
        if (plan.ServerId != connection.ServerId
            || plan.DeploymentId != connection.DeploymentId
            || plan.WorkerId != connection.WorkerId
            || plan.CommandId != command.CommandId
            || !IsSha256(plan.PlanToken)
            || plan.Selections.Count > WorkerStorageProtocol.MaximumSelectionCount)
        {
            throw new InvalidDataException("Preservation plan attribution or limits are invalid.");
        }
        var ids = new HashSet<Guid>();
        foreach (var selection in plan.Selections)
        {
            if (selection.SelectionId == Guid.Empty
                || !ids.Add(selection.SelectionId)
                || !Enum.IsDefined(selection.Scope)
                || !Enum.IsDefined(selection.Mode))
            {
                throw new InvalidDataException("Preservation plan contains an invalid selection.");
            }
            var normalized = WorkerStoragePath.Normalize(
                selection.RelativePath,
                selection.Scope is not EPreservationScope.File);
            if (normalized.Length == 0
                || !string.Equals(normalized, selection.RelativePath, StringComparison.Ordinal))
                throw new InvalidDataException("Preservation plan contains a non-canonical selection path.");
        }
    }

    private void ValidateSnapshotManifest(
        WorkerStorageArchiveManifest manifest,
        WorkerCommandClaimResponse command)
    {
        if (manifest.Version != WorkerStorageProtocol.Version
            || manifest.Kind != EWorkerStorageArchiveKind.PersistentSnapshot
            || manifest.ServerId != connection.ServerId
            || manifest.DeploymentId != connection.DeploymentId
            || manifest.WorkerId != connection.WorkerId
            || manifest.CommandId != command.CommandId
            || manifest.PreservationPlanToken is not null)
        {
            throw new InvalidDataException("Persistent snapshot attribution is invalid.");
        }
    }

    private static string ValidateManifestEntry(WorkerStorageArchiveEntry entry)
    {
        var normalized = WorkerStoragePath.Normalize(entry.RelativePath, false);
        if (entry.IsDirectory)
        {
            if (entry.Length != 0 || entry.Sha256 is not null)
                throw new InvalidDataException("A snapshot directory contains file metadata.");
        }
        else
        {
            EnsureFileLength(entry.Length);
            if (!IsSha256(entry.Sha256))
                throw new InvalidDataException("A snapshot file has invalid integrity metadata.");
        }
        return normalized;
    }

    private static bool HashMatches(string? expected, string actual)
        => IsSha256(expected)
           && CryptographicOperations.FixedTimeEquals(
               Convert.FromHexString(expected!),
               Convert.FromHexString(actual));

    private static bool IsSha256(string? value)
    {
        if (value is not { Length: 64 })
            return false;
        try
        {
            return Convert.FromHexString(value).Length == SHA256.HashSizeInBytes;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static void RejectArchiveLink(ZipArchiveEntry entry)
    {
        const int unixFileTypeMask = 0xF000;
        const int unixSymbolicLink = 0xA000;
        if (((entry.ExternalAttributes >> 16) & unixFileTypeMask) == unixSymbolicLink)
            throw new InvalidDataException("Symbolic links are not accepted in worker storage archives.");
    }

    private static async Task<(long length, string hash)> CopyWithHashAsync(
        Stream input,
        Stream output,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long copied = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
                break;
            copied = checked(copied + read);
            if (copied > maximumBytes)
                throw new InvalidDataException("A worker storage file exceeds the per-file size limit.");
            hash.AppendData(buffer, 0, read);
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
        return (copied, Convert.ToHexString(hash.GetHashAndReset()));
    }

    private static async Task CopyBoundedAsync(
        Stream input,
        Stream output,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long copied = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
                break;
            copied = checked(copied + read);
            if (copied > maximumBytes)
                throw new InvalidDataException("Worker storage transfer exceeds its size limit.");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task CopyFileAsync(
        string source,
        string destination,
        CancellationToken cancellationToken)
    {
        await using var input = new FileStream(
            source,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var output = new FileStream(
            destination,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void EnsureEntryCount(int count)
    {
        if (count > WorkerStorageProtocol.MaximumEntryCount)
            throw new InvalidDataException("Worker storage archive contains too many entries.");
    }

    private static void EnsureFileLength(long length)
    {
        if (length < 0 || length > WorkerStorageProtocol.MaximumFileBytes)
            throw new InvalidDataException("A worker storage file exceeds the per-file size limit.");
    }

    private static void EnsureExpandedLength(long length)
    {
        if (length > WorkerStorageProtocol.MaximumExpandedBytes)
            throw new InvalidDataException("Worker storage archive exceeds the expanded size limit.");
    }

    private static string ArchiveEntryName(string relativePath, bool isDirectory)
        => WorkerStorageProtocol.FileEntryPrefix + relativePath + (isDirectory ? "/" : string.Empty);

    private static int PathDepth(string path) => path.Count(q => q == '/');

    private static void TryDeleteStaging(string stagingRoot)
    {
        try
        {
            if (Directory.Exists(stagingRoot))
                Directory.Delete(stagingRoot, true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record PersistentOverlayManifest
    {
        public int Version { get; init; } = PersistentOverlayManifestVersion;
        public Guid ServerId { get; init; }
        public Guid DeploymentId { get; init; }
        public IReadOnlyList<PersistentOverlayEntry> Entries { get; init; } = [];
    }

    private sealed record PersistentOverlayEntry(string RelativePath, bool IsDirectory);

    private sealed record LocalStorageEntry(string FullPath, string RelativePath, bool IsDirectory);
}

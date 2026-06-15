using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using X39.UnitedTacticalForces.CloudWorker;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using Xunit;

namespace CloudWorker.Tests;

public class CloudWorkerStorageTransferTests
{
    [Fact]
    public async Task ApplyPersistentSnapshot_VerifiesThenOverlaysRuntimeTree()
    {
        using var directory = new TemporaryDirectory();
        var connection = CloudWorkerBootstrapConfigTests.CreateBootstrap().ToConnectionConfig();
        var command = CreateCommand(connection, "start");
        var credentialStore = await CreateCredentialStoreAsync(directory.Path, connection, "worker-secret");
        var persistentContent = Encoding.UTF8.GetBytes("persistent-server-config");
        var archive = BuildSnapshotArchive(connection, command.CommandId, "server/server.cfg", persistentContent);
        var handler = new CallbackHandler((request, _) =>
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("worker-secret", request.Headers.Authorization?.Parameter);
            Assert.EndsWith(
                $"/commands/{command.CommandId:D}/storage/persistent-snapshot",
                request.RequestUri?.AbsolutePath,
                StringComparison.Ordinal);
            var content = new ByteArrayContent(archive);
            content.Headers.ContentType = new MediaTypeHeaderValue(WorkerStorageProtocol.ArchiveMediaType);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        });
        using var client = new HttpClient(handler);
        var transfer = new CloudWorkerStorageTransfer(
            client,
            credentialStore,
            connection,
            Path.Combine(directory.Path, "state"));
        var deploymentRoot = Path.Combine(directory.Path, "runtime", connection.DeploymentId.ToString("N"));
        var target = Path.Combine(deploymentRoot, "server", "server.cfg");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        await File.WriteAllTextAsync(target, "rendered-server-config");

        await transfer.ApplyPersistentSnapshotAsync(command, deploymentRoot, CancellationToken.None);

        Assert.Equal("persistent-server-config", await File.ReadAllTextAsync(target));
        Assert.Empty(Directory.EnumerateDirectories(deploymentRoot, ".utf-snapshot-*"));
        Assert.False(File.Exists(Path.Combine(deploymentRoot, ".utf-persistent-overlay-manifest.json")));
        Assert.True(File.Exists(Path.Combine(
            directory.Path,
            "state",
            "persistent-overlays",
            connection.DeploymentId.ToString("N"),
            ".utf-persistent-overlay-manifest.json")));
    }

    [Fact]
    public async Task ApplyPersistentSnapshot_RemovesPriorAndCrashPartialFilesToConvergeExactly()
    {
        using var directory = new TemporaryDirectory();
        var connection = CloudWorkerBootstrapConfigTests.CreateBootstrap().ToConnectionConfig();
        var command = CreateCommand(connection, "start");
        var credentialStore = await CreateCredentialStoreAsync(directory.Path, connection, "worker-secret");
        var firstArchive = BuildSnapshotArchive(
            connection,
            command.CommandId,
            new Dictionary<string, byte[]>
            {
                ["server/profiles/old.txt"] = Encoding.UTF8.GetBytes("old"),
                ["server/profiles/keep.txt"] = Encoding.UTF8.GetBytes("keep-v1"),
            });
        var currentArchive = BuildSnapshotArchive(
            connection,
            command.CommandId,
            new Dictionary<string, byte[]>
            {
                ["server/profiles/keep.txt"] = Encoding.UTF8.GetBytes("keep-v2"),
            });
        var requestCount = 0;
        var handler = new CallbackHandler((_, _) =>
        {
            var content = new ByteArrayContent(requestCount++ == 0 ? firstArchive : currentArchive);
            content.Headers.ContentType = new MediaTypeHeaderValue(WorkerStorageProtocol.ArchiveMediaType);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        });
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var transfer = new CloudWorkerStorageTransfer(
            client,
            credentialStore,
            connection,
            Path.Combine(directory.Path, "state"));
        var deploymentRoot = Path.Combine(directory.Path, "runtime", connection.DeploymentId.ToString("N"));

        await transfer.ApplyPersistentSnapshotAsync(command, deploymentRoot, CancellationToken.None);
        await SimulateInterruptedOverlayAsync(
            deploymentRoot,
            Path.Combine(directory.Path, "state"),
            connection,
            "server/profiles/partial.txt",
            "partially-applied");

        await transfer.ApplyPersistentSnapshotAsync(command, deploymentRoot, CancellationToken.None);

        Assert.False(File.Exists(Path.Combine(deploymentRoot, "server", "profiles", "old.txt")));
        Assert.False(File.Exists(Path.Combine(deploymentRoot, "server", "profiles", "partial.txt")));
        Assert.Equal(
            "keep-v2",
            await File.ReadAllTextAsync(Path.Combine(deploymentRoot, "server", "profiles", "keep.txt")));
    }

    [Fact]
    public async Task ResetPersistentOverlay_AfterWorkerRestartRemovesOnlyDurablyTrackedFiles()
    {
        using var directory = new TemporaryDirectory();
        var connection = CloudWorkerBootstrapConfigTests.CreateBootstrap().ToConnectionConfig();
        var command = CreateCommand(connection, "start");
        var credentialStore = await CreateCredentialStoreAsync(directory.Path, connection, "worker-secret");
        var archive = BuildSnapshotArchive(
            connection,
            command.CommandId,
            "server/profiles/tracked.txt",
            Encoding.UTF8.GetBytes("tracked"));
        var handler = new CallbackHandler((_, _) =>
        {
            var content = new ByteArrayContent(archive);
            content.Headers.ContentType = new MediaTypeHeaderValue(WorkerStorageProtocol.ArchiveMediaType);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        });
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var stateDirectory = Path.Combine(directory.Path, "state");
        var deploymentRoot = Path.Combine(directory.Path, "runtime", connection.DeploymentId.ToString("N"));
        var firstWorker = new CloudWorkerStorageTransfer(client, credentialStore, connection, stateDirectory);
        await firstWorker.ApplyPersistentSnapshotAsync(command, deploymentRoot, CancellationToken.None);
        await WriteAsync(deploymentRoot, "server/profiles/untracked.txt", "untracked");

        var restartedWorker = new CloudWorkerStorageTransfer(client, credentialStore, connection, stateDirectory);
        await restartedWorker.ResetPersistentOverlayAsync(deploymentRoot, CancellationToken.None);

        Assert.False(File.Exists(Path.Combine(deploymentRoot, "server", "profiles", "tracked.txt")));
        Assert.Equal(
            "untracked",
            await File.ReadAllTextAsync(Path.Combine(deploymentRoot, "server", "profiles", "untracked.txt")));
    }

    [Fact]
    public async Task ApplyPersistentSnapshot_SlowTransferUsesCallerCancellationWithInfiniteClientTimeout()
    {
        using var directory = new TemporaryDirectory();
        var connection = CloudWorkerBootstrapConfigTests.CreateBootstrap().ToConnectionConfig();
        var command = CreateCommand(connection, "start");
        var credentialStore = await CreateCredentialStoreAsync(directory.Path, connection, "worker-secret");
        var handler = new CallbackHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The delayed request unexpectedly completed.");
        });
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var transfer = new CloudWorkerStorageTransfer(
            client,
            credentialStore,
            connection,
            Path.Combine(directory.Path, "state"));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => transfer.ApplyPersistentSnapshotAsync(
            command,
            Path.Combine(directory.Path, "runtime", connection.DeploymentId.ToString("N")),
            cancellation.Token));

        Assert.Equal(Timeout.InfiniteTimeSpan, client.Timeout);
    }

    [Fact]
    public async Task ApplyPersistentSnapshot_RejectsTraversalBeforeChangingRuntimeTree()
    {
        using var directory = new TemporaryDirectory();
        var connection = CloudWorkerBootstrapConfigTests.CreateBootstrap().ToConnectionConfig();
        var command = CreateCommand(connection, "start");
        var credentialStore = await CreateCredentialStoreAsync(directory.Path, connection, "worker-secret");
        var archive = BuildSnapshotArchive(connection, command.CommandId, "../escaped.txt", [1, 2, 3]);
        var handler = new CallbackHandler((_, _) =>
        {
            var content = new ByteArrayContent(archive);
            content.Headers.ContentType = new MediaTypeHeaderValue(WorkerStorageProtocol.ArchiveMediaType);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        });
        using var client = new HttpClient(handler);
        var transfer = new CloudWorkerStorageTransfer(
            client,
            credentialStore,
            connection,
            Path.Combine(directory.Path, "state"));
        var deploymentRoot = Path.Combine(directory.Path, "runtime", connection.DeploymentId.ToString("N"));
        var target = Path.Combine(deploymentRoot, "server", "server.cfg");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        await File.WriteAllTextAsync(target, "unchanged");

        await Assert.ThrowsAsync<InvalidDataException>(() => transfer.ApplyPersistentSnapshotAsync(
            command,
            deploymentRoot,
            CancellationToken.None));

        Assert.Equal("unchanged", await File.ReadAllTextAsync(target));
        Assert.False(File.Exists(Path.Combine(directory.Path, "runtime", "escaped.txt")));
    }

    [Fact]
    public async Task ApplyPersistentSnapshot_RejectsWorkerOwnedInstallRoot()
    {
        using var directory = new TemporaryDirectory();
        var connection = CloudWorkerBootstrapConfigTests.CreateBootstrap().ToConnectionConfig();
        var command = CreateCommand(connection, "start");
        var credentialStore = await CreateCredentialStoreAsync(directory.Path, connection, "worker-secret");
        var archive = BuildSnapshotArchive(
            connection,
            command.CommandId,
            "server-instance/arma3server_x64",
            [1, 2, 3]);
        var handler = new CallbackHandler((_, _) =>
        {
            var content = new ByteArrayContent(archive);
            content.Headers.ContentType = new MediaTypeHeaderValue(WorkerStorageProtocol.ArchiveMediaType);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        });
        using var client = new HttpClient(handler);
        var transfer = new CloudWorkerStorageTransfer(
            client,
            credentialStore,
            connection,
            Path.Combine(directory.Path, "state"));
        var deploymentRoot = Path.Combine(directory.Path, "runtime", connection.DeploymentId.ToString("N"));

        await Assert.ThrowsAsync<InvalidDataException>(() => transfer.ApplyPersistentSnapshotAsync(
            command,
            deploymentRoot,
            CancellationToken.None));

        Assert.False(File.Exists(Path.Combine(deploymentRoot, "server-instance", "arma3server_x64")));
    }

    [Fact]
    public async Task PublishPreservation_RejectsWorkerOwnedSelectionBeforeReadingRuntime()
    {
        using var directory = new TemporaryDirectory();
        var connection = CloudWorkerBootstrapConfigTests.CreateBootstrap().ToConnectionConfig();
        var command = CreateCommand(connection, "stop");
        var credentialStore = await CreateCredentialStoreAsync(directory.Path, connection, "worker-secret");
        var plan = new WorkerPreservationPlanResponse
        {
            ServerId = connection.ServerId,
            DeploymentId = connection.DeploymentId,
            WorkerId = connection.WorkerId,
            CommandId = command.CommandId,
            PlanToken = new string('A', 64),
            Selections =
            [
                new WorkerPreservationSelection
                {
                    SelectionId = Guid.NewGuid(),
                    RelativePath = "server-instance",
                    Scope = EPreservationScope.RecursiveFolder,
                    Mode = EPreservationMode.CreateOrUpdate,
                },
            ],
        };
        var requestCount = 0;
        var handler = new CallbackHandler((_, _) =>
        {
            requestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(plan, options: CloudWorkerJson.Options),
            });
        });
        using var client = new HttpClient(handler);
        var transfer = new CloudWorkerStorageTransfer(
            client,
            credentialStore,
            connection,
            Path.Combine(directory.Path, "state"));
        var deploymentRoot = Path.Combine(directory.Path, "runtime", connection.DeploymentId.ToString("N"));

        await Assert.ThrowsAsync<InvalidDataException>(() => transfer.PublishPreservationAsync(
            command,
            deploymentRoot,
            CancellationToken.None));

        Assert.Equal(1, requestCount);
    }

    [Fact]
    public async Task PublishPreservation_HonorsScopesAndReloadsCredentialBeforeUpload()
    {
        using var directory = new TemporaryDirectory();
        var connection = CloudWorkerBootstrapConfigTests.CreateBootstrap().ToConnectionConfig();
        var command = CreateCommand(connection, "stop");
        var credentialStore = await CreateCredentialStoreAsync(directory.Path, connection, "old-secret");
        var plan = new WorkerPreservationPlanResponse
        {
            ServerId = connection.ServerId,
            DeploymentId = connection.DeploymentId,
            WorkerId = connection.WorkerId,
            CommandId = command.CommandId,
            PlanToken = new string('A', 64),
            Selections =
            [
                new WorkerPreservationSelection
                {
                    SelectionId = Guid.NewGuid(),
                    RelativePath = "server/profiles",
                    Scope = EPreservationScope.RecursiveFolder,
                    Mode = EPreservationMode.CreateOrUpdate,
                },
                new WorkerPreservationSelection
                {
                    SelectionId = Guid.NewGuid(),
                    RelativePath = "server/battleye/bans.txt",
                    Scope = EPreservationScope.File,
                    Mode = EPreservationMode.MirrorDeletions,
                },
                new WorkerPreservationSelection
                {
                    SelectionId = Guid.NewGuid(),
                    RelativePath = "server/flat",
                    Scope = EPreservationScope.FlatFolder,
                    Mode = EPreservationMode.MirrorDeletions,
                },
            ],
        };
        byte[]? uploadedArchive = null;
        var requestCount = 0;
        var handler = new CallbackHandler(async (request, cancellationToken) =>
        {
            requestCount++;
            if (requestCount == 1)
            {
                Assert.Equal("old-secret", request.Headers.Authorization?.Parameter);
                await credentialStore.SaveAsync(
                    Stored(connection, "new-secret"),
                    cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(plan, options: CloudWorkerJson.Options),
                };
            }
            Assert.Equal("new-secret", request.Headers.Authorization?.Parameter);
            uploadedArchive = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new WorkerPreservationPublicationResponse
                {
                    CopiedFileCount = 4,
                }),
            };
        });
        using var client = new HttpClient(handler);
        var transfer = new CloudWorkerStorageTransfer(
            client,
            credentialStore,
            connection,
            Path.Combine(directory.Path, "state"));
        var deploymentRoot = Path.Combine(directory.Path, "runtime", connection.DeploymentId.ToString("N"));
        await WriteAsync(deploymentRoot, "server/profiles/player.vars", "player");
        await WriteAsync(deploymentRoot, "server/profiles/nested/mission.vars", "mission");
        await WriteAsync(deploymentRoot, "server/battleye/bans.txt", "ban");
        await WriteAsync(deploymentRoot, "server/flat/top.txt", "top");
        await WriteAsync(deploymentRoot, "server/flat/nested/not-selected.txt", "nested");
        await WriteAsync(deploymentRoot, "server/not-selected.txt", "other");
        await WriteAsync(deploymentRoot, ".utf-arma3-process.json", "metadata");

        await transfer.PublishPreservationAsync(command, deploymentRoot, CancellationToken.None);

        Assert.Equal(2, requestCount);
        Assert.NotNull(uploadedArchive);
        var manifest = ReadManifest(uploadedArchive!);
        var paths = manifest.Entries.Select(q => q.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("server/profiles", paths);
        Assert.Contains("server/profiles/player.vars", paths);
        Assert.Contains("server/profiles/nested", paths);
        Assert.Contains("server/profiles/nested/mission.vars", paths);
        Assert.Contains("server/battleye/bans.txt", paths);
        Assert.Contains("server/flat/top.txt", paths);
        Assert.DoesNotContain("server/flat", paths);
        Assert.DoesNotContain("server/flat/nested/not-selected.txt", paths);
        Assert.DoesNotContain("server/not-selected.txt", paths);
        Assert.DoesNotContain(".utf-arma3-process.json", paths);
        Assert.Equal(plan.PlanToken, manifest.PreservationPlanToken);
    }

    private static async Task<CloudWorkerCredentialStore> CreateCredentialStoreAsync(
        string root,
        CloudWorkerConnectionConfig connection,
        string credential)
    {
        var store = new CloudWorkerCredentialStore(Path.Combine(root, "state"));
        await store.SaveAsync(Stored(connection, credential));
        return store;
    }

    private static StoredWorkerCredential Stored(CloudWorkerConnectionConfig connection, string credential)
        => new(connection, credential, DateTimeOffset.UtcNow.AddHours(1), 15, DateTimeOffset.UtcNow);

    private static WorkerCommandClaimResponse CreateCommand(
        CloudWorkerConnectionConfig connection,
        string action)
        => FlatFileWorkerCommandJournalTests.CreateCommand(
            connection.WorkerId,
            connection.DeploymentId,
            connection.ServerId,
            action);

    private static byte[] BuildSnapshotArchive(
        CloudWorkerConnectionConfig connection,
        Guid commandId,
        string path,
        byte[] content)
        => BuildSnapshotArchive(connection, commandId, new Dictionary<string, byte[]> { [path] = content });

    private static byte[] BuildSnapshotArchive(
        CloudWorkerConnectionConfig connection,
        Guid commandId,
        IReadOnlyDictionary<string, byte[]> contents)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true, Encoding.UTF8))
        {
            foreach (var (path, content) in contents)
            {
                var file = zip.CreateEntry(WorkerStorageProtocol.FileEntryPrefix + path);
                using var stream = file.Open();
                stream.Write(content);
            }
            var manifest = new WorkerStorageArchiveManifest
            {
                Kind = EWorkerStorageArchiveKind.PersistentSnapshot,
                ServerId = connection.ServerId,
                DeploymentId = connection.DeploymentId,
                WorkerId = connection.WorkerId,
                CommandId = commandId,
                Entries = contents.Select(q =>
                    new WorkerStorageArchiveEntry
                    {
                        RelativePath = q.Key,
                        Length = q.Value.Length,
                        Sha256 = Convert.ToHexString(SHA256.HashData(q.Value)),
                    })
                    .ToArray(),
            };
            var manifestEntry = zip.CreateEntry(WorkerStorageProtocol.ManifestEntryName);
            using var manifestStream = manifestEntry.Open();
            JsonSerializer.Serialize(manifestStream, manifest, CloudWorkerJson.Options);
        }
        return output.ToArray();
    }

    private static async Task SimulateInterruptedOverlayAsync(
        string deploymentRoot,
        string agentStateDirectory,
        CloudWorkerConnectionConfig connection,
        string partialPath,
        string partialContent)
    {
        await WriteAsync(deploymentRoot, partialPath, partialContent);
        var manifestPath = Path.Combine(
            agentStateDirectory,
            "persistent-overlays",
            connection.DeploymentId.ToString("N"),
            ".utf-persistent-overlay-manifest.json");
        using var current = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
        var entries = current.RootElement.GetProperty("entries")
            .EnumerateArray()
            .Select(q => new
            {
                relativePath = q.GetProperty("relativePath").GetString()!,
                isDirectory = q.GetProperty("isDirectory").GetBoolean(),
            })
            .Append(new { relativePath = partialPath, isDirectory = false })
            .ToArray();
        await File.WriteAllTextAsync(
            manifestPath,
            JsonSerializer.Serialize(
                new
                {
                    version = 1,
                    serverId = connection.ServerId,
                    deploymentId = connection.DeploymentId,
                    entries,
                },
                CloudWorkerJson.Options));
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(manifestPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    private static WorkerStorageArchiveManifest ReadManifest(byte[] archive)
    {
        using var input = new MemoryStream(archive);
        using var zip = new ZipArchive(input, ZipArchiveMode.Read);
        using var stream = zip.GetEntry(WorkerStorageProtocol.ManifestEntryName)!.Open();
        return JsonSerializer.Deserialize<WorkerStorageArchiveManifest>(stream, CloudWorkerJson.Options)!;
    }

    private static async Task WriteAsync(string root, string path, string content)
    {
        var fullPath = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, content);
    }

    private sealed class CallbackHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => callback(request, cancellationToken);
    }
}

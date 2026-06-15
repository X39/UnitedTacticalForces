using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Api.Services.WorkerProtocol;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using Xunit;

namespace Api.Tests.GameServerHosting;

public sealed class WorkerStorageTransferServiceTests
{
    private static readonly JsonSerializerOptions ArchiveJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void GameCapabilityPolicy_DeniesUnknownRootsAndExactFileDirectories()
    {
        var definition = new StorageGameDefinitionCatalog().GetDefinitions().Single();

        Assert.True(GameServerFileCapabilityPolicy.CanMutate(
            definition,
            "server/server.cfg",
            false,
            EFileContext.Persistent));
        Assert.False(GameServerFileCapabilityPolicy.CanMutate(
            definition,
            "server/server.cfg",
            true,
            EFileContext.Persistent));
        Assert.False(GameServerFileCapabilityPolicy.CanMutate(
            definition,
            "server-instance/arma3server_x64",
            false,
            EFileContext.Persistent));
        Assert.False(GameServerFileCapabilityPolicy.CanMutate(
            definition,
            "unlisted/file.cfg",
            false,
            EFileContext.Persistent));
        Assert.True(GameServerFileCapabilityPolicy.IsPersistentSnapshotPathAllowed(
            definition,
            "server",
            true));
    }

    [Fact]
    public async Task StartSnapshot_IsCommandBoundAndRejectsProtectedMetadata()
    {
        await using var store = await HostingTestStore.CreateAsync();
        using var directory = new StorageTempDirectory();
        var seed = await SeedAsync(store, directory.Path, ELifecycleJobKind.Start, EDeploymentStatus.Preparing);
        var persistentRoot = seed.PathResolver.ResolvePersistentRoot(seed.ServerId);
        await WriteAsync(persistentRoot, "server/server.cfg", "persistent-config");
        await WriteAsync(persistentRoot, "server/profiles/player.vars", "profile-state");

        await using var download = await seed.Service.CreatePersistentSnapshotAsync(
            seed.WorkerId,
            seed.CommandId,
            seed.Credential);
        using var zip = new ZipArchive(download.Content, ZipArchiveMode.Read, true);
        var manifest = ReadManifest(zip);

        Assert.Equal(EWorkerStorageArchiveKind.PersistentSnapshot, manifest.Kind);
        Assert.Equal(seed.ServerId, manifest.ServerId);
        Assert.Equal(seed.DeploymentId, manifest.DeploymentId);
        Assert.Equal(seed.WorkerId, manifest.WorkerId);
        Assert.Equal(seed.CommandId, manifest.CommandId);
        Assert.Contains(manifest.Entries, q => q.RelativePath == "server/server.cfg" && !q.IsDirectory);
        Assert.Equal(
            "persistent-config",
            ReadText(zip, WorkerStorageProtocol.FileEntryPrefix + "server/server.cfg"));
        await Assert.ThrowsAsync<WorkerProtocolAuthenticationException>(() =>
            seed.Service.CreatePersistentSnapshotAsync(seed.WorkerId, seed.CommandId, "wrong-credential"));

        await WriteAsync(persistentRoot, ".utf-worker-credentials", "must-not-transfer");
        await Assert.ThrowsAsync<WorkerProtocolConflictException>(() =>
            seed.Service.CreatePersistentSnapshotAsync(seed.WorkerId, seed.CommandId, seed.Credential));
        File.Delete(Resolve(persistentRoot, ".utf-worker-credentials"));
        await WriteAsync(persistentRoot, "server-instance/arma3server_x64", "worker-owned-binary");
        await Assert.ThrowsAsync<WorkerProtocolConflictException>(() =>
            seed.Service.CreatePersistentSnapshotAsync(seed.WorkerId, seed.CommandId, seed.Credential));
    }

    [Fact]
    public async Task PreservationPlan_RejectsWorkerOwnedOrUndeclaredRoots()
    {
        await using var store = await HostingTestStore.CreateAsync();
        using var directory = new StorageTempDirectory();
        var seed = await SeedAsync(store, directory.Path, ELifecycleJobKind.Stop, EDeploymentStatus.Stopping);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        store.DbContext.PreservationSelections.Add(Selection(
            seed.ServerId,
            "server-instance",
            EPreservationScope.RecursiveFolder,
            EPreservationMode.CreateOrUpdate,
            now));
        await store.DbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<WorkerProtocolConflictException>(() => seed.Service.GetPreservationPlanAsync(
            seed.WorkerId,
            seed.CommandId,
            seed.Credential));
    }

    [Fact]
    public async Task FaultStop_PreservationIsAcceptedOnlyInsideFaultTerminationWindow()
    {
        await using var store = await HostingTestStore.CreateAsync();
        using var directory = new StorageTempDirectory();
        var seed = await SeedAsync(store, directory.Path, ELifecycleJobKind.Stop, EDeploymentStatus.Stopping);
        var command = await store.DbContext.WorkerCommands.SingleAsync(q => q.PrimaryKey == seed.CommandId);
        command.ActionIdentifier = GameServerActionIds.FaultStop;
        command.Payload = JsonSerializer.Serialize(
            new WorkerCommandPayload { FaultEvidenceDeadline = DateTimeOffset.UtcNow.AddMinutes(1) },
            ArchiveJsonOptions);
        var deployment = await store.DbContext.GameServerDeployments.SingleAsync(q => q.PrimaryKey == seed.DeploymentId);
        deployment.IsProvisioningBlocked = true;
        deployment.Status = EDeploymentStatus.TerminatingDueToFault;
        await store.DbContext.SaveChangesAsync();

        var plan = await seed.Service.GetPreservationPlanAsync(
            seed.WorkerId,
            seed.CommandId,
            seed.Credential);

        Assert.Equal(seed.CommandId, plan.CommandId);

        deployment.Status = EDeploymentStatus.Terminating;
        await store.DbContext.SaveChangesAsync();
        await Assert.ThrowsAsync<WorkerProtocolConflictException>(() => seed.Service.GetPreservationPlanAsync(
            seed.WorkerId,
            seed.CommandId,
            seed.Credential));

        deployment.Status = EDeploymentStatus.TerminatingDueToFault;
        command.Payload = JsonSerializer.Serialize(
            new WorkerCommandPayload { FaultEvidenceDeadline = DateTimeOffset.UtcNow.AddMinutes(-1) },
            ArchiveJsonOptions);
        await store.DbContext.SaveChangesAsync();
        await Assert.ThrowsAsync<WorkerProtocolConflictException>(() => seed.Service.GetPreservationPlanAsync(
            seed.WorkerId,
            seed.CommandId,
            seed.Credential));
    }

    [Fact]
    public async Task FaultStop_PublicationCrossingEvidenceDeadlineLeavesPersistentTreeUnchanged()
    {
        await using var store = await HostingTestStore.CreateAsync();
        using var directory = new StorageTempDirectory();
        var seed = await SeedAsync(store, directory.Path, ELifecycleJobKind.Stop, EDeploymentStatus.Stopping);
        var command = await store.DbContext.WorkerCommands.SingleAsync(q => q.PrimaryKey == seed.CommandId);
        command.ActionIdentifier = GameServerActionIds.FaultStop;
        var deployment = await store.DbContext.GameServerDeployments.SingleAsync(q => q.PrimaryKey == seed.DeploymentId);
        deployment.IsProvisioningBlocked = true;
        deployment.Status = EDeploymentStatus.TerminatingDueToFault;
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        store.DbContext.PreservationSelections.Add(Selection(
            seed.ServerId,
            "server/create.txt",
            EPreservationScope.File,
            EPreservationMode.CreateOrUpdate,
            now));
        await store.DbContext.SaveChangesAsync();
        var persistentRoot = seed.PathResolver.ResolvePersistentRoot(seed.ServerId);
        await WriteAsync(persistentRoot, "server/create.txt", "original-value");
        command.Payload = JsonSerializer.Serialize(
            new WorkerCommandPayload { FaultEvidenceDeadline = DateTimeOffset.UtcNow.AddSeconds(1) },
            ArchiveJsonOptions);
        await store.DbContext.SaveChangesAsync();
        var plan = await seed.Service.GetPreservationPlanAsync(
            seed.WorkerId,
            seed.CommandId,
            seed.Credential);
        await using var archive = BuildPreservationArchive(
            seed,
            plan.PlanToken,
            new Dictionary<string, string> { ["server/create.txt"] = "late-value" });
        await using var delayedArchive = new DelayOnFirstReadStream(archive, TimeSpan.FromMilliseconds(1_500));

        await Assert.ThrowsAsync<WorkerProtocolConflictException>(() => seed.Service.PublishPreservationAsync(
            seed.WorkerId,
            seed.CommandId,
            seed.Credential,
            delayedArchive));

        Assert.Equal("original-value", await ReadAsync(persistentRoot, "server/create.txt"));
        Assert.DoesNotContain(
            await store.DbContext.HostingAuditRecords.ToArrayAsync(),
            q => q.Kind == EHostingAuditEventKind.PreservationPublished
                 && q.WorkerCommandFk == seed.CommandId);
    }

    [Fact]
    public async Task StopPublication_AppliesSelectionModesAndMirrorDeletionsAtomically()
    {
        await using var store = await HostingTestStore.CreateAsync();
        using var directory = new StorageTempDirectory();
        var seed = await SeedAsync(store, directory.Path, ELifecycleJobKind.Stop, EDeploymentStatus.Stopping);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        store.DbContext.PreservationSelections.AddRange(
            Selection(seed.ServerId, "server/create.txt", EPreservationScope.File, EPreservationMode.CreateOrUpdate, now),
            Selection(seed.ServerId, "server/mirror", EPreservationScope.RecursiveFolder, EPreservationMode.MirrorDeletions, now),
            Selection(seed.ServerId, "server/flat", EPreservationScope.FlatFolder, EPreservationMode.MirrorDeletions, now),
            Selection(
                seed.ServerId,
                "server/profiles",
                EPreservationScope.RecursiveFolder,
                EPreservationMode.CreateOrUpdate,
                now));
        await store.DbContext.SaveChangesAsync();
        var persistentRoot = seed.PathResolver.ResolvePersistentRoot(seed.ServerId);
        await WriteAsync(persistentRoot, "server/create.txt", "retain-create-or-update");
        await WriteAsync(persistentRoot, "server/mirror/keep.txt", "old-keep");
        await WriteAsync(persistentRoot, "server/mirror/stale.txt", "delete-stale");
        await WriteAsync(persistentRoot, "server/mirror/sub/old.txt", "delete-recursive");
        await WriteAsync(persistentRoot, "server/flat/top-stale.txt", "delete-flat");
        await WriteAsync(persistentRoot, "server/flat/nested/retain.txt", "retain-nested");
        await WriteAsync(persistentRoot, "server/profiles/update.txt", "old-profile");
        await WriteAsync(persistentRoot, "server/profiles/missing-live.txt", "retain-missing-profile");
        await WriteAsync(persistentRoot, "server/unselected.txt", "retain-unselected");
        var plan = await seed.Service.GetPreservationPlanAsync(
            seed.WorkerId,
            seed.CommandId,
            seed.Credential);
        await using var archive = BuildPreservationArchive(
            seed,
            plan.PlanToken,
            new Dictionary<string, string>
            {
                ["server/mirror/keep.txt"] = "new-keep",
                ["server/mirror/new.txt"] = "new-file",
                ["server/flat/top-new.txt"] = "new-flat",
                ["server/profiles/update.txt"] = "new-profile",
            });

        var result = await seed.Service.PublishPreservationAsync(
            seed.WorkerId,
            seed.CommandId,
            seed.Credential,
            archive);

        Assert.Equal(4, result.CopiedFileCount);
        Assert.True(result.DeletedFileCount >= 3);
        Assert.Equal("retain-create-or-update", await ReadAsync(persistentRoot, "server/create.txt"));
        Assert.Equal("new-keep", await ReadAsync(persistentRoot, "server/mirror/keep.txt"));
        Assert.Equal("new-file", await ReadAsync(persistentRoot, "server/mirror/new.txt"));
        Assert.False(File.Exists(Resolve(persistentRoot, "server/mirror/stale.txt")));
        Assert.False(File.Exists(Resolve(persistentRoot, "server/mirror/sub/old.txt")));
        Assert.False(File.Exists(Resolve(persistentRoot, "server/flat/top-stale.txt")));
        Assert.Equal("new-flat", await ReadAsync(persistentRoot, "server/flat/top-new.txt"));
        Assert.Equal("retain-nested", await ReadAsync(persistentRoot, "server/flat/nested/retain.txt"));
        Assert.Equal("new-profile", await ReadAsync(persistentRoot, "server/profiles/update.txt"));
        Assert.Equal(
            "retain-missing-profile",
            await ReadAsync(persistentRoot, "server/profiles/missing-live.txt"));
        Assert.Equal("retain-unselected", await ReadAsync(persistentRoot, "server/unselected.txt"));
        Assert.Contains(
            await store.DbContext.HostingAuditRecords.ToArrayAsync(),
            q => q.Kind == EHostingAuditEventKind.PreservationPublished
                 && q.WorkerCommandFk == seed.CommandId);

        await using var corruptArchive = BuildPreservationArchive(
            seed,
            plan.PlanToken,
            new Dictionary<string, string> { ["server/mirror/keep.txt"] = "must-not-publish" },
            corruptHash: true);
        await Assert.ThrowsAsync<WorkerProtocolValidationException>(() => seed.Service.PublishPreservationAsync(
            seed.WorkerId,
            seed.CommandId,
            seed.Credential,
            corruptArchive));
        Assert.Equal("new-keep", await ReadAsync(persistentRoot, "server/mirror/keep.txt"));
        Assert.Contains(
            await store.DbContext.HostingAuditRecords.ToArrayAsync(),
            q => q.Kind == EHostingAuditEventKind.PreservationFailed
                 && q.WorkerCommandFk == seed.CommandId);
    }

    private static async Task<TransferSeed> SeedAsync(
        HostingTestStore store,
        string persistentBasePath,
        ELifecycleJobKind kind,
        EDeploymentStatus deploymentStatus)
    {
        var hosting = await HostingTestData.SeedServerAsync(
            store.DbContext,
            withActiveDeployment: true,
            deploymentStatus: deploymentStatus);
        var deployment = hosting.Deployment!;
        deployment.IsProvisioningBlocked = false;
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var worker = new LifecycleWorker
        {
            PrimaryKey = Guid.NewGuid(),
            Deployment = deployment,
            DeploymentFk = deployment.PrimaryKey,
            Identity = "external-storage-test-worker",
            Status = EWorkerStatus.Online,
            LastHeartbeatAt = now,
            TimeStampCreated = now,
            TimeStampUpdated = now,
            TimeStampRegistered = now,
        };
        var job = new LifecycleJob
        {
            PrimaryKey = Guid.NewGuid(),
            GameServer = hosting.Server,
            GameServerFk = hosting.Server.PrimaryKey,
            Deployment = deployment,
            DeploymentFk = deployment.PrimaryKey,
            RequestedBy = hosting.User,
            RequestedByFk = hosting.User.PrimaryKey,
            Kind = kind,
            ActionIdentifier = kind is ELifecycleJobKind.Start ? GameServerActions.Start : GameServerActions.Stop,
            Sequence = 1,
            Status = ELifecycleJobStatus.Running,
            Stage = "worker-running",
            Progress = 50,
            Attempt = 1,
            MaxAttempts = 3,
            TimeStampCreated = now,
            TimeStampUpdated = now,
            TimeStampStarted = now,
        };
        var command = new WorkerCommand
        {
            PrimaryKey = Guid.NewGuid(),
            LifecycleJob = job,
            LifecycleJobFk = job.PrimaryKey,
            Deployment = deployment,
            DeploymentFk = deployment.PrimaryKey,
            Worker = worker,
            WorkerFk = worker.PrimaryKey,
            Sequence = 1,
            ActionIdentifier = job.ActionIdentifier,
            Payload = "{}",
            Status = EWorkerCommandStatus.Running,
            Stage = "running",
            Attempt = 1,
            TimeStampCreated = now,
            TimeStampReceived = now,
            TimeStampStarted = now,
        };
        var options = Options.Create(new LifecycleWorkerOptions
        {
            WorkerCredentialLifetime = TimeSpan.FromHours(1),
        });
        var credentials = new WorkerCredentialService(options);
        var credential = credentials.IssuePendingWorkerCredential(worker, now).Credential;
        store.DbContext.AddRange(worker, job, command);
        await store.DbContext.SaveChangesAsync();
        var pathResolver = new TestPersistentPathResolver(persistentBasePath);
        var service = new WorkerStorageTransferService(
            store.DbContext,
            credentials,
            new StorageGameDefinitionCatalog(),
            pathResolver,
            new ServerFileLockProvider(),
            new PreservationSelectionResolver(),
            new HostingAuditWriter(),
            NullLogger<WorkerStorageTransferService>.Instance);
        return new TransferSeed(
            service,
            pathResolver,
            hosting.Server.PrimaryKey,
            deployment.PrimaryKey,
            worker.PrimaryKey,
            command.PrimaryKey,
            credential);
    }

    private static PreservationSelection Selection(
        Guid serverId,
        string path,
        EPreservationScope scope,
        EPreservationMode mode,
        Instant now)
        => new()
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = serverId,
            Path = path,
            Scope = scope,
            Mode = mode,
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };

    private static MemoryStream BuildPreservationArchive(
        TransferSeed seed,
        string planToken,
        IReadOnlyDictionary<string, string> files,
        bool corruptHash = false)
    {
        var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true, Encoding.UTF8))
        {
            var entries = new List<WorkerStorageArchiveEntry>();
            foreach (var (path, value) in files)
            {
                var bytes = Encoding.UTF8.GetBytes(value);
                var file = zip.CreateEntry(WorkerStorageProtocol.FileEntryPrefix + path);
                using (var stream = file.Open())
                    stream.Write(bytes);
                entries.Add(new WorkerStorageArchiveEntry
                {
                    RelativePath = path,
                    Length = bytes.Length,
                    Sha256 = corruptHash
                        ? new string('0', 64)
                        : Convert.ToHexString(SHA256.HashData(bytes)),
                });
            }
            var manifest = new WorkerStorageArchiveManifest
            {
                Kind = EWorkerStorageArchiveKind.PreservationUpload,
                ServerId = seed.ServerId,
                DeploymentId = seed.DeploymentId,
                WorkerId = seed.WorkerId,
                CommandId = seed.CommandId,
                PreservationPlanToken = planToken,
                Entries = entries,
            };
            var manifestEntry = zip.CreateEntry(WorkerStorageProtocol.ManifestEntryName);
            using var manifestStream = manifestEntry.Open();
            JsonSerializer.Serialize(manifestStream, manifest, ArchiveJsonOptions);
        }
        output.Position = 0;
        return output;
    }

    private static WorkerStorageArchiveManifest ReadManifest(ZipArchive zip)
    {
        using var stream = zip.GetEntry(WorkerStorageProtocol.ManifestEntryName)!.Open();
        return JsonSerializer.Deserialize<WorkerStorageArchiveManifest>(stream, ArchiveJsonOptions)!;
    }

    private static string ReadText(ZipArchive zip, string entryName)
    {
        using var stream = zip.GetEntry(entryName)!.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static async Task WriteAsync(string root, string relativePath, string value)
    {
        var path = Resolve(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, value);
    }

    private static Task<string> ReadAsync(string root, string relativePath)
        => File.ReadAllTextAsync(Resolve(root, relativePath));

    private static string Resolve(string root, string relativePath)
        => Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private sealed record TransferSeed(
        WorkerStorageTransferService Service,
        TestPersistentPathResolver PathResolver,
        Guid ServerId,
        Guid DeploymentId,
        Guid WorkerId,
        Guid CommandId,
        string Credential);

    private sealed class TestPersistentPathResolver(string persistentBasePath) : IPersistentStoragePathResolver
    {
        public string PersistentBasePath { get; } = Path.GetFullPath(persistentBasePath);

        public string ResolvePersistentRoot(Guid serverId)
            => Path.Combine(PersistentBasePath, serverId.ToString("N"));
    }

    private sealed class StorageGameDefinitionCatalog : IGameServerDefinitionCatalog
    {
        private static readonly GameServerDefinition Definition = new(
            HostingTestData.GameIdentifier,
            "Storage test game",
            [],
            [
                new("server/server.cfg", EFileClassification.Configuration, false, true, false, false),
                new("server/create.txt", EFileClassification.Configuration, false, true, false, false),
                new("server/mirror", EFileClassification.SaveFile, true, true, false, false),
                new("server/flat", EFileClassification.SaveFile, true, true, false, false),
                new("server/profiles", EFileClassification.Configuration, true, true, false, false),
            ],
            []);

        public IReadOnlyList<GameServerDefinition> GetDefinitions() => [Definition];
    }

    private sealed class DelayOnFirstReadStream(Stream inner, TimeSpan delay) : Stream
    {
        private int _delayed;

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }

        public override void Flush() => inner.Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            DelayOnce();
            return inner.Read(buffer, offset, count);
        }

        public override int Read(Span<byte> buffer)
        {
            DelayOnce();
            return inner.Read(buffer);
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            await DelayOnceAsync(cancellationToken).ConfigureAwait(false);
            return await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        public override async Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            await DelayOnceAsync(cancellationToken).ConfigureAwait(false);
            return await inner.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
        }

        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                inner.Dispose();
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await inner.DisposeAsync().ConfigureAwait(false);
            await base.DisposeAsync().ConfigureAwait(false);
        }

        private void DelayOnce()
        {
            if (Interlocked.Exchange(ref _delayed, 1) == 0)
                Thread.Sleep(delay);
        }

        private async Task DelayOnceAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Exchange(ref _delayed, 1) == 0)
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class StorageTempDirectory : IDisposable
    {
        public StorageTempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "utf-storage-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, true);
        }
    }
}

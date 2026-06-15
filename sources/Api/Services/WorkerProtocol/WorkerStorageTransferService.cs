using System.Data;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using X39.Util.DependencyInjection.Attributes;
using TransferFailureSanitizer = X39.UnitedTacticalForces.Api.Services.HostingRuntime.HostingFailureSanitizer;
using GameDefinition = X39.UnitedTacticalForces.Api.Services.GameServerDefinition.GameServerDefinition;

namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

/// <summary>
/// Authenticated bridge between API-owned persistent data and one deployment-bound external worker.
/// </summary>
[Scoped<WorkerStorageTransferService, IWorkerStorageTransferService>]
internal sealed class WorkerStorageTransferService(
    DatabaseContext dbContext,
    IWorkerCredentialService credentialService,
    IGameServerDefinitionCatalog gameDefinitions,
    IPersistentStoragePathResolver pathResolver,
    IServerFileLockProvider lockProvider,
    IPreservationSelectionResolver selectionResolver,
    IHostingAuditWriter auditWriter,
    ILogger<WorkerStorageTransferService> logger) : IWorkerStorageTransferService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
    private static readonly StringComparer ProtocolPathComparer = StringComparer.OrdinalIgnoreCase;

    public async Task<WorkerStorageArchiveDownload> CreatePersistentSnapshotAsync(
        Guid workerId,
        Guid commandId,
        string workerCredential,
        CancellationToken cancellationToken = default)
    {
        var context = await AuthenticateCommandAsync(
                workerId,
                commandId,
                workerCredential,
                GameServerActions.Start,
                ELifecycleJobKind.Start,
                allowFaultStop: false,
                cancellationToken)
            .ConfigureAwait(false);
        await using var serverLock = await lockProvider
            .AcquireAsync(context.ServerId, cancellationToken)
            .ConfigureAwait(false);
        var persistentRoot = pathResolver.ResolvePersistentRoot(context.ServerId);
        PersistentTreePublicationRecovery.Recover(
            pathResolver.PersistentBasePath,
            persistentRoot,
            context.ServerId,
            true);
        Directory.CreateDirectory(persistentRoot);
        SafeServerPath.RejectExistingReparsePoints(persistentRoot, persistentRoot);

        try
        {
            var stream = await BuildSnapshotArchiveAsync(context, persistentRoot, cancellationToken)
                .ConfigureAwait(false);
            return new WorkerStorageArchiveDownload(stream);
        }
        catch (Exception ex) when (ex is IOException
                                       or InvalidDataException
                                       or UnauthorizedAccessException
                                       or ArgumentException)
        {
            throw new WorkerProtocolConflictException(
                TransferFailureSanitizer.Sanitize(ex.Message, persistentRoot));
        }
    }

    public async Task<WorkerPreservationPlanResponse> GetPreservationPlanAsync(
        Guid workerId,
        Guid commandId,
        string workerCredential,
        CancellationToken cancellationToken = default)
    {
        var context = await AuthenticateCommandAsync(
                workerId,
                commandId,
                workerCredential,
                GameServerActions.Stop,
                ELifecycleJobKind.Stop,
                allowFaultStop: true,
                cancellationToken)
            .ConfigureAwait(false);
        var selections = await GetSelectionsAsync(context, cancellationToken).ConfigureAwait(false);
        return new WorkerPreservationPlanResponse
        {
            ServerId = context.ServerId,
            DeploymentId = context.DeploymentId,
            WorkerId = context.WorkerId,
            CommandId = context.CommandId,
            PlanToken = ComputePlanToken(selections),
            Selections = selections.Select(ToContractSelection).ToArray(),
        };
    }

    public async Task<WorkerPreservationPublicationResponse> PublishPreservationAsync(
        Guid workerId,
        Guid commandId,
        string workerCredential,
        Stream archive,
        CancellationToken cancellationToken = default)
    {
        if (archive is null || !archive.CanRead)
            throw new WorkerProtocolValidationException("A readable preservation archive is required.");

        var context = await AuthenticateCommandAsync(
                workerId,
                commandId,
                workerCredential,
                GameServerActions.Stop,
                ELifecycleJobKind.Stop,
                allowFaultStop: true,
                cancellationToken)
            .ConfigureAwait(false);
        var selections = await GetSelectionsAsync(context, cancellationToken).ConfigureAwait(false);
        var planToken = ComputePlanToken(selections);
        var persistentRoot = pathResolver.ResolvePersistentRoot(context.ServerId);
        var stagingBase = PersistentTreePublicationRecovery.ResolveStagingBase(pathResolver.PersistentBasePath);
        var transferRoot = Path.Combine(stagingBase, $"{context.ServerId:N}-{context.CommandId:N}-upload");
        var publicationRoot = Path.Combine(stagingBase, $"{context.ServerId:N}-{context.CommandId:N}-publish");
        var backupRoot = PersistentTreePublicationRecovery.ResolveBackupRoot(
            pathResolver.PersistentBasePath,
            context.ServerId);
        var publicationCompleted = false;
        var hadPreviousTree = false;

        await using var serverLock = await lockProvider
            .AcquireAsync(context.ServerId, cancellationToken)
            .ConfigureAwait(false);
        try
        {
            PersistentTreePublicationRecovery.Recover(
                pathResolver.PersistentBasePath,
                persistentRoot,
                context.ServerId,
                true);
            Directory.CreateDirectory(stagingBase);
            Directory.CreateDirectory(transferRoot);
            var uploaded = await ValidateAndExtractPreservationAsync(
                    context,
                    planToken,
                    selections,
                    archive,
                    transferRoot,
                    cancellationToken)
                .ConfigureAwait(false);

            Directory.CreateDirectory(publicationRoot);
            if (Directory.Exists(persistentRoot))
                await CopyTreeAsync(persistentRoot, publicationRoot, cancellationToken).ConfigureAwait(false);
            var copied = await OverlayTransferAsync(
                    transferRoot,
                    publicationRoot,
                    uploaded,
                    cancellationToken)
                .ConfigureAwait(false);
            var deleted = ApplyMirroredDeletions(publicationRoot, uploaded, selections, cancellationToken);

            await using var transaction = await BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            var finalContext = await LockAndValidateCommandAsync(
                    workerId,
                    commandId,
                    workerCredential,
                    GameServerActions.Stop,
                    ELifecycleJobKind.Stop,
                    allowFaultStop: true,
                    cancellationToken)
                .ConfigureAwait(false);
            var finalSelections = await GetSelectionsAsync(finalContext, cancellationToken).ConfigureAwait(false);
            if (!CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(planToken),
                    Convert.FromHexString(ComputePlanToken(finalSelections))))
            {
                throw new WorkerProtocolConflictException(
                    "Preservation selections changed while the worker transfer was in progress.");
            }

            hadPreviousTree = Directory.Exists(persistentRoot);
            PublishStagedTree(persistentRoot, publicationRoot, backupRoot);
            publicationCompleted = true;
            auditWriter.Add(
                dbContext,
                EHostingAuditEventKind.PreservationPublished,
                "External worker preservation was verified and atomically published.",
                serverId: context.ServerId,
                deploymentId: context.DeploymentId,
                lifecycleJobId: context.LifecycleJobId,
                workerId: context.WorkerId,
                workerCommandId: context.CommandId,
                costOwnerId: context.CostOwnerId,
                details: $"Copied={copied}; Deleted={deleted}; Entries={uploaded.Count}");
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            publicationCompleted = false;

            PersistentTreePublicationRecovery.TryDeleteTree(backupRoot);
            logger.LogInformation(
                "Published {CopiedFileCount} preserved files and {DeletedFileCount} deletions for server {ServerId}",
                copied,
                deleted,
                context.ServerId);
            return new WorkerPreservationPublicationResponse
            {
                CopiedFileCount = copied,
                DeletedFileCount = deleted,
            };
        }
        catch (OperationCanceledException)
        {
            if (publicationCompleted)
                RestorePreviousTree(persistentRoot, publicationRoot, backupRoot, hadPreviousTree);
            throw;
        }
        catch (Exception ex) when (ex is IOException
                                       or InvalidDataException
                                       or JsonException
                                       or UnauthorizedAccessException
                                       or ArgumentException)
        {
            if (publicationCompleted)
                RestorePreviousTree(persistentRoot, publicationRoot, backupRoot, hadPreviousTree);
            var failure = TransferFailureSanitizer.Sanitize(
                ex.Message,
                persistentRoot,
                transferRoot,
                publicationRoot,
                backupRoot);
            await TryAuditPreservationFailureAsync(context, failure, cancellationToken).ConfigureAwait(false);
            throw new WorkerProtocolValidationException(failure);
        }
        catch
        {
            if (publicationCompleted)
                RestorePreviousTree(persistentRoot, publicationRoot, backupRoot, hadPreviousTree);
            throw;
        }
        finally
        {
            PersistentTreePublicationRecovery.TryDeleteTree(transferRoot);
            PersistentTreePublicationRecovery.TryDeleteTree(publicationRoot);
        }
    }

    private async Task<MemoryStream> BuildSnapshotArchiveAsync(
        AuthenticatedCommand context,
        string persistentRoot,
        CancellationToken cancellationToken)
    {
        var definition = GetGameDefinition(context.GameIdentifier);
        var treeEntries = Directory.Exists(persistentRoot)
            ? EnumerateTree(persistentRoot).ToArray()
            : [];
        EnsureEntryLimit(treeEntries.Length);
        foreach (var entry in treeEntries)
        {
            var normalized = SafeServerPath.Normalize(entry.RelativePath, false);
            if (!GameServerFileCapabilityPolicy.IsPersistentSnapshotPathAllowed(
                    definition,
                    normalized,
                    entry.IsDirectory))
            {
                throw new InvalidDataException(
                    "Persistent data contains a path outside the game implementation's allowed roots.");
            }
        }

        var output = new MemoryStream();
        var manifestEntries = new List<WorkerStorageArchiveEntry>();
        long expandedBytes = 0;
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true, Encoding.UTF8))
        {
            foreach (var entry in treeEntries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var normalized = SafeServerPath.Normalize(entry.RelativePath, false);
                if (entry.IsDirectory)
                {
                    zip.CreateEntry(ArchiveEntryName(normalized, true), CompressionLevel.NoCompression);
                    manifestEntries.Add(new WorkerStorageArchiveEntry
                    {
                        RelativePath = normalized,
                        IsDirectory = true,
                    });
                    continue;
                }

                var length = new FileInfo(entry.FullPath).Length;
                EnsureFileLength(length);
                expandedBytes = checked(expandedBytes + length);
                EnsureExpandedLength(expandedBytes);
                var zipEntry = zip.CreateEntry(ArchiveEntryName(normalized, false), CompressionLevel.Fastest);
                await using var destination = zipEntry.Open();
                var hash = await CopyFileWithHashAsync(entry.FullPath, destination, cancellationToken)
                    .ConfigureAwait(false);
                manifestEntries.Add(new WorkerStorageArchiveEntry
                {
                    RelativePath = normalized,
                    Length = length,
                    Sha256 = hash,
                });
            }

            var manifest = new WorkerStorageArchiveManifest
            {
                Kind = EWorkerStorageArchiveKind.PersistentSnapshot,
                ServerId = context.ServerId,
                DeploymentId = context.DeploymentId,
                WorkerId = context.WorkerId,
                CommandId = context.CommandId,
                Entries = manifestEntries,
            };
            var manifestEntry = zip.CreateEntry(WorkerStorageProtocol.ManifestEntryName, CompressionLevel.Fastest);
            await using var manifestStream = manifestEntry.Open();
            await JsonSerializer.SerializeAsync(
                    manifestStream,
                    manifest,
                    SerializerOptions,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (output.Length > WorkerStorageProtocol.MaximumArchiveBytes)
        {
            output.Dispose();
            throw new IOException("The persistent snapshot exceeds the worker archive size limit.");
        }
        output.Position = 0;
        return output;
    }

    private async Task<IReadOnlyList<WorkerStorageArchiveEntry>> ValidateAndExtractPreservationAsync(
        AuthenticatedCommand context,
        string planToken,
        IReadOnlyCollection<PreservationPathSelection> selections,
        Stream source,
        string transferRoot,
        CancellationToken cancellationToken)
    {
        await using var boundedArchive = new MemoryStream();
        await CopyBoundedAsync(
                source,
                boundedArchive,
                WorkerStorageProtocol.MaximumArchiveBytes,
                cancellationToken)
            .ConfigureAwait(false);
        boundedArchive.Position = 0;
        using var zip = new ZipArchive(boundedArchive, ZipArchiveMode.Read, true, Encoding.UTF8);
        if (zip.Entries.Count > WorkerStorageProtocol.MaximumEntryCount + 1)
            throw new InvalidDataException("The preservation archive contains too many entries.");
        var manifestEntries = zip.Entries
            .Where(q => string.Equals(q.FullName, WorkerStorageProtocol.ManifestEntryName, StringComparison.Ordinal))
            .ToArray();
        if (manifestEntries.Length != 1 || manifestEntries[0].Length > 1024 * 1024)
            throw new InvalidDataException("The preservation archive manifest is missing or invalid.");

        WorkerStorageArchiveManifest manifest;
        await using (var manifestStream = manifestEntries[0].Open())
        {
            manifest = await JsonSerializer.DeserializeAsync<WorkerStorageArchiveManifest>(
                           manifestStream,
                           SerializerOptions,
                           cancellationToken)
                       .ConfigureAwait(false)
                       ?? throw new InvalidDataException("The preservation archive manifest is empty.");
        }
        ValidateManifestAttribution(
            manifest,
            EWorkerStorageArchiveKind.PreservationUpload,
            context,
            planToken);
        EnsureEntryLimit(manifest.Entries.Count);

        var manifestByPath = new Dictionary<string, WorkerStorageArchiveEntry>(ProtocolPathComparer);
        foreach (var entry in manifest.Entries)
        {
            var normalized = ValidateManifestEntry(entry);
            if (!manifestByPath.TryAdd(normalized, entry with { RelativePath = normalized }))
                throw new InvalidDataException("The preservation manifest contains a duplicate path.");
            if (selectionResolver.Resolve(normalized, entry.IsDirectory, selections) is null)
                throw new InvalidDataException("The worker archive contains a path outside current preservation selections.");
        }

        var archiveByPath = new Dictionary<string, ZipArchiveEntry>(ProtocolPathComparer);
        foreach (var entry in zip.Entries.Where(q =>
                     !string.Equals(q.FullName, WorkerStorageProtocol.ManifestEntryName, StringComparison.Ordinal)))
        {
            RejectArchiveLink(entry);
            if (!entry.FullName.StartsWith(WorkerStorageProtocol.FileEntryPrefix, StringComparison.Ordinal)
                || entry.FullName.Contains('\\'))
            {
                throw new InvalidDataException("The preservation archive contains an invalid entry name.");
            }
            var rawPath = entry.FullName[WorkerStorageProtocol.FileEntryPrefix.Length..].TrimEnd('/');
            var normalized = SafeServerPath.Normalize(rawPath, false);
            if (!archiveByPath.TryAdd(normalized, entry))
                throw new InvalidDataException("The preservation archive contains a duplicate entry.");
        }
        if (archiveByPath.Count != manifestByPath.Count)
            throw new InvalidDataException("The preservation archive is incomplete.");

        long expandedBytes = 0;
        foreach (var (path, expected) in manifestByPath)
        {
            if (!archiveByPath.TryGetValue(path, out var archiveEntry))
                throw new InvalidDataException("The preservation archive is missing a declared entry.");
            var isDirectory = archiveEntry.FullName.EndsWith("/", StringComparison.Ordinal);
            if (isDirectory != expected.IsDirectory)
                throw new InvalidDataException("The preservation archive entry type does not match its manifest.");
            var destination = SafeServerPath.ResolveWithinRoot(transferRoot, path, false);
            if (isDirectory)
            {
                if (archiveEntry.Length != 0)
                    throw new InvalidDataException("A preservation directory entry contains file data.");
                Directory.CreateDirectory(destination);
                continue;
            }

            EnsureFileLength(expected.Length);
            expandedBytes = checked(expandedBytes + expected.Length);
            EnsureExpandedLength(expandedBytes);
            var parent = Path.GetDirectoryName(destination)
                         ?? throw new InvalidDataException("A preservation path has no parent directory.");
            Directory.CreateDirectory(parent);
            SafeServerPath.RejectExistingReparsePoints(transferRoot, parent);
            await using var input = archiveEntry.Open();
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
            if (length != expected.Length || !IsExpectedHash(expected.Sha256, hash))
                throw new InvalidDataException("A preservation archive entry failed its size or integrity check.");
        }
        return manifestByPath.Values.ToArray();
    }

    private static async Task<int> OverlayTransferAsync(
        string transferRoot,
        string publicationRoot,
        IReadOnlyCollection<WorkerStorageArchiveEntry> entries,
        CancellationToken cancellationToken)
    {
        var copied = 0;
        foreach (var entry in entries.OrderBy(q => q.RelativePath.Count(c => c == '/')))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = SafeServerPath.ResolveWithinRoot(transferRoot, entry.RelativePath, false);
            var destination = SafeServerPath.ResolveWithinRoot(publicationRoot, entry.RelativePath, false);
            if (entry.IsDirectory)
            {
                if (File.Exists(destination))
                    throw new IOException("A preserved directory conflicts with an existing persistent file.");
                Directory.CreateDirectory(destination);
                continue;
            }
            if (Directory.Exists(destination))
                throw new IOException("A preserved file conflicts with an existing persistent directory.");
            var parent = Path.GetDirectoryName(destination)
                         ?? throw new IOException("A preserved file has no parent directory.");
            Directory.CreateDirectory(parent);
            await CopyFileAsync(source, destination, cancellationToken).ConfigureAwait(false);
            copied++;
        }
        return copied;
    }

    private int ApplyMirroredDeletions(
        string publicationRoot,
        IReadOnlyCollection<WorkerStorageArchiveEntry> uploaded,
        IReadOnlyCollection<PreservationPathSelection> selections,
        CancellationToken cancellationToken)
    {
        var livePaths = uploaded.Select(q => q.RelativePath).ToHashSet(ProtocolPathComparer);
        var deletedFiles = 0;
        foreach (var entry in EnumerateTree(publicationRoot).Where(q => !q.IsDirectory).ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalized = SafeServerPath.Normalize(entry.RelativePath, false);
            var match = selectionResolver.Resolve(normalized, false, selections);
            if (match?.Selection.Mode is not EPreservationMode.MirrorDeletions || livePaths.Contains(normalized))
                continue;
            File.Delete(entry.FullPath);
            deletedFiles++;
        }

        foreach (var entry in EnumerateTree(publicationRoot)
                     .Where(q => q.IsDirectory)
                     .OrderByDescending(q => q.RelativePath.Count(c => c == '/'))
                     .ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalized = SafeServerPath.Normalize(entry.RelativePath, false);
            var match = selectionResolver.Resolve(normalized, true, selections);
            if (match?.Selection.Mode is EPreservationMode.MirrorDeletions
                && !livePaths.Contains(normalized)
                && !Directory.EnumerateFileSystemEntries(entry.FullPath).Any())
            {
                Directory.Delete(entry.FullPath, false);
            }
        }
        return deletedFiles;
    }

    private async Task<AuthenticatedCommand> AuthenticateCommandAsync(
        Guid workerId,
        Guid commandId,
        string workerCredential,
        string expectedAction,
        ELifecycleJobKind expectedKind,
        bool allowFaultStop,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var context = await LockAndValidateCommandAsync(
                workerId,
                commandId,
                workerCredential,
                expectedAction,
                expectedKind,
                allowFaultStop,
                cancellationToken)
            .ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return context;
    }

    private async Task<AuthenticatedCommand> LockAndValidateCommandAsync(
        Guid workerId,
        Guid commandId,
        string workerCredential,
        string expectedAction,
        ELifecycleJobKind expectedKind,
        bool allowFaultStop,
        CancellationToken cancellationToken)
    {
        var target = await dbContext.WorkerCommands
            .AsNoTracking()
            .Where(q => q.PrimaryKey == commandId && q.WorkerFk == workerId)
            .Select(q => new CommandTarget(
                q.DeploymentFk,
                q.Deployment!.GameServerFk,
                q.Deployment.GameServer!.GameIdentifier))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (target is null)
            throw new WorkerProtocolAuthenticationException();

        LifecycleWorker? worker;
        if (IsPostgreSql)
        {
            await dbContext.GameServers
                .FromSqlInterpolated(
                    $"""SELECT * FROM "GameServers" WHERE "PrimaryKey" = {target.ServerId} FOR UPDATE""")
                .LoadAsync(cancellationToken)
                .ConfigureAwait(false);
            var lockedDeployment = await dbContext.GameServerDeployments
                .FromSqlInterpolated(
                    $"""SELECT * FROM "GameServerDeployments" WHERE "PrimaryKey" = {target.DeploymentId} FOR UPDATE""")
                .SingleAsync(cancellationToken)
                .ConfigureAwait(false);
            worker = await dbContext.LifecycleWorkers
                .FromSqlInterpolated(
                    $"""SELECT * FROM "LifecycleWorkers" WHERE "PrimaryKey" = {workerId} FOR UPDATE""")
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (worker is not null)
                worker.Deployment = lockedDeployment;
        }
        else
        {
            worker = await dbContext.LifecycleWorkers
                .Include(q => q.Deployment)
                .SingleOrDefaultAsync(
                    q => q.PrimaryKey == workerId && q.DeploymentFk == target.DeploymentId,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var now = NodaSystemClock.Instance.GetCurrentInstant();
        if (worker is null
            || worker.DeploymentFk != target.DeploymentId
            || worker.TimeStampStopped is not null
            || worker.Deployment?.TimeStampResolved is not null
            || !credentialService.VerifyAndPromoteWorkerCredential(worker, workerCredential, now))
        {
            throw new WorkerProtocolAuthenticationException();
        }

        var command = await dbContext.WorkerCommands
            .Include(q => q.LifecycleJob)
            .SingleOrDefaultAsync(q => q.PrimaryKey == commandId, cancellationToken)
            .ConfigureAwait(false);
        if (command is null
            || command.WorkerFk != worker.PrimaryKey
            || command.DeploymentFk != worker.DeploymentFk)
        {
            throw new WorkerProtocolAuthenticationException();
        }
        var isFaultStop = allowFaultStop
                          && string.Equals(
                              command.ActionIdentifier,
                              GameServerActionIds.FaultStop,
                              StringComparison.OrdinalIgnoreCase);
        var lifecycleJob = command.LifecycleJob
                           ?? throw new WorkerProtocolConflictException(
                               "The worker command has no durable lifecycle job.");
        if ((!string.Equals(command.ActionIdentifier, expectedAction, StringComparison.OrdinalIgnoreCase)
             || lifecycleJob.Kind != expectedKind)
            && !isFaultStop)
        {
            throw new WorkerProtocolConflictException("The worker command is not valid for this storage operation.");
        }
        if (command.Status is not (EWorkerCommandStatus.Received or EWorkerCommandStatus.Running)
            || lifecycleJob.Status is not (ELifecycleJobStatus.Running or ELifecycleJobStatus.WaitingForWorker))
        {
            throw new WorkerProtocolConflictException("The worker command no longer accepts storage transfer.");
        }

        var deployment = worker.Deployment
                         ?? throw new WorkerProtocolAuthenticationException();
        if (isFaultStop)
        {
            if (!deployment.IsProvisioningBlocked
                || deployment.Status is not EDeploymentStatus.TerminatingDueToFault
                || !HasUnexpiredFaultEvidenceDeadline(command.Payload, now))
            {
                throw new WorkerProtocolConflictException(
                    "Fault preservation is allowed only during bounded fault-triggered termination.");
            }
        }
        else if (expectedKind is ELifecycleJobKind.Start)
        {
            if (deployment.IsProvisioningBlocked
                || deployment.Status is not (EDeploymentStatus.Preparing or EDeploymentStatus.WaitingForWorker))
            {
                throw new WorkerProtocolConflictException("The deployment no longer accepts a persistent overlay.");
            }
        }
        else if (deployment.Status is not (EDeploymentStatus.Stopping or EDeploymentStatus.PreservationFailed))
        {
            throw new WorkerProtocolConflictException("The deployment no longer accepts preservation publication.");
        }

        return new AuthenticatedCommand(
            target.ServerId,
            target.DeploymentId,
            worker.PrimaryKey,
            command.PrimaryKey,
            command.LifecycleJobFk,
            deployment.CostOwnerFk,
            target.GameIdentifier);
    }

    private static bool HasUnexpiredFaultEvidenceDeadline(string payload, Instant now)
    {
        try
        {
            var commandPayload = JsonSerializer.Deserialize<WorkerCommandPayload>(payload, SerializerOptions);
            return commandPayload?.FaultEvidenceDeadline is { } deadline
                   && Instant.FromDateTimeOffset(deadline) > now;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task<PreservationPathSelection[]> GetSelectionsAsync(
        AuthenticatedCommand context,
        CancellationToken cancellationToken)
    {
        var selections = await dbContext.PreservationSelections
            .AsNoTracking()
            .Where(q => q.GameServerFk == context.ServerId)
            .OrderBy(q => q.PrimaryKey)
            .Select(q => new PreservationPathSelection(q.PrimaryKey, q.Path, q.Scope, q.Mode))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (selections.Length > WorkerStorageProtocol.MaximumSelectionCount)
            throw new WorkerProtocolConflictException("The server has too many preservation selections.");
        PreservationPathSelection[] normalized;
        try
        {
            normalized = selections.Select(q => q with
            {
                RelativePath = SafeServerPath.Normalize(q.RelativePath, q.Scope is not EPreservationScope.File),
            }).ToArray();
        }
        catch (ArgumentException)
        {
            throw new WorkerProtocolConflictException("A preservation selection contains an unsafe path.");
        }
        var definition = GetGameDefinition(context.GameIdentifier);
        if (normalized.Any(q => !GameServerFileCapabilityPolicy.IsPreservationSelectionAllowed(
                definition,
                q.RelativePath,
                q.Scope)))
        {
            throw new WorkerProtocolConflictException(
                "A preservation selection is outside the game implementation's allowed persistent roots.");
        }
        return normalized;
    }

    private GameDefinition GetGameDefinition(string gameIdentifier)
        => gameDefinitions.GetDefinitions().Single(q => string.Equals(
            q.GameIdentifier,
            gameIdentifier,
            StringComparison.OrdinalIgnoreCase));

    private async Task TryAuditPreservationFailureAsync(
        AuthenticatedCommand context,
        string failure,
        CancellationToken cancellationToken)
    {
        try
        {
            auditWriter.Add(
                dbContext,
                EHostingAuditEventKind.PreservationFailed,
                "External worker preservation failed before publication; prior persistent data was retained.",
                serverId: context.ServerId,
                deploymentId: context.DeploymentId,
                lifecycleJobId: context.LifecycleJobId,
                workerId: context.WorkerId,
                workerCommandId: context.CommandId,
                costOwnerId: context.CostOwnerId,
                details: failure);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                "Could not persist preservation-failure audit for deployment {DeploymentId}: {Failure}",
                context.DeploymentId,
                TransferFailureSanitizer.Sanitize(ex.Message));
        }
    }

    private static WorkerPreservationSelection ToContractSelection(PreservationPathSelection selection)
        => new()
        {
            SelectionId = selection.SelectionId,
            RelativePath = selection.RelativePath,
            Scope = selection.Scope,
            Mode = selection.Mode,
        };

    private static string ComputePlanToken(IEnumerable<PreservationPathSelection> selections)
    {
        var canonical = string.Join(
            '\n',
            selections.OrderBy(q => q.SelectionId).Select(q =>
                $"{q.SelectionId:N}|{q.RelativePath}|{(int) q.Scope}|{(int) q.Mode}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static void ValidateManifestAttribution(
        WorkerStorageArchiveManifest manifest,
        EWorkerStorageArchiveKind expectedKind,
        AuthenticatedCommand context,
        string? expectedPlanToken)
    {
        if (manifest.Version != WorkerStorageProtocol.Version
            || manifest.Kind != expectedKind
            || manifest.ServerId != context.ServerId
            || manifest.DeploymentId != context.DeploymentId
            || manifest.WorkerId != context.WorkerId
            || manifest.CommandId != context.CommandId)
        {
            throw new InvalidDataException("The worker storage archive attribution is invalid.");
        }
        if (expectedPlanToken is not null
            && !string.Equals(manifest.PreservationPlanToken, expectedPlanToken, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The preservation archive does not match the current selection plan.");
        }
    }

    private static string ValidateManifestEntry(WorkerStorageArchiveEntry entry)
    {
        var normalized = SafeServerPath.Normalize(entry.RelativePath, false);
        if (entry.RelativePath.Length > 2048 || entry.Length < 0)
            throw new InvalidDataException("A worker storage manifest entry is invalid.");
        if (entry.IsDirectory)
        {
            if (entry.Length != 0 || entry.Sha256 is not null)
                throw new InvalidDataException("A directory manifest entry contains file metadata.");
        }
        else
        {
            EnsureFileLength(entry.Length);
            if (!IsSha256(entry.Sha256))
                throw new InvalidDataException("A file manifest entry has an invalid integrity hash.");
        }
        return normalized;
    }

    private static bool IsExpectedHash(string? expected, string actual)
    {
        if (!IsSha256(expected))
            return false;
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(expected!),
            Convert.FromHexString(actual));
    }

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

    private static string ArchiveEntryName(string relativePath, bool isDirectory)
        => WorkerStorageProtocol.FileEntryPrefix + relativePath + (isDirectory ? "/" : string.Empty);

    private static void EnsureEntryLimit(int count)
    {
        if (count > WorkerStorageProtocol.MaximumEntryCount)
            throw new InvalidDataException("The worker storage archive contains too many entries.");
    }

    private static void EnsureFileLength(long length)
    {
        if (length < 0 || length > WorkerStorageProtocol.MaximumFileBytes)
            throw new InvalidDataException("A worker storage file exceeds the per-file size limit.");
    }

    private static void EnsureExpandedLength(long length)
    {
        if (length > WorkerStorageProtocol.MaximumExpandedBytes)
            throw new InvalidDataException("The worker storage archive exceeds the expanded size limit.");
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
                throw new InvalidDataException("The worker storage request exceeds the archive size limit.");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<string> CopyFileWithHashAsync(
        string source,
        Stream destination,
        CancellationToken cancellationToken)
    {
        await using var input = new FileStream(
            source,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var (_, hash) = await CopyWithHashAsync(
                input,
                destination,
                WorkerStorageProtocol.MaximumFileBytes,
                cancellationToken)
            .ConfigureAwait(false);
        return hash;
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

    private static IEnumerable<TreeEntry> EnumerateTree(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.TryPop(out var directory))
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(directory))
            {
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Worker storage cannot traverse a symbolic link or reparse point.");
                var isDirectory = (attributes & FileAttributes.Directory) != 0;
                var relativePath = Path.GetRelativePath(root, path).Replace('\\', '/');
                _ = SafeServerPath.Normalize(relativePath, false);
                yield return new TreeEntry(path, relativePath, isDirectory);
                if (isDirectory)
                    pending.Push(path);
            }
        }
    }

    private static async Task CopyTreeAsync(
        string sourceRoot,
        string destinationRoot,
        CancellationToken cancellationToken)
    {
        foreach (var entry in EnumerateTree(sourceRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destination = SafeServerPath.ResolveWithinRoot(destinationRoot, entry.RelativePath, false);
            if (entry.IsDirectory)
            {
                Directory.CreateDirectory(destination);
                continue;
            }
            var parent = Path.GetDirectoryName(destination)
                         ?? throw new IOException("A persistent server path has no parent directory.");
            Directory.CreateDirectory(parent);
            await CopyFileAsync(entry.FullPath, destination, cancellationToken).ConfigureAwait(false);
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
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void PublishStagedTree(string persistentRoot, string stagingRoot, string backupRoot)
    {
        var hadPrevious = Directory.Exists(persistentRoot);
        if (hadPrevious)
            Directory.Move(persistentRoot, backupRoot);
        try
        {
            Directory.Move(stagingRoot, persistentRoot);
        }
        catch
        {
            if (hadPrevious && Directory.Exists(backupRoot) && !Directory.Exists(persistentRoot))
                Directory.Move(backupRoot, persistentRoot);
            throw;
        }
    }

    private static void RestorePreviousTree(
        string persistentRoot,
        string failedPublicationRoot,
        string backupRoot,
        bool hadPreviousTree)
    {
        if (Directory.Exists(persistentRoot))
            Directory.Move(persistentRoot, failedPublicationRoot);
        if (hadPreviousTree && Directory.Exists(backupRoot))
            Directory.Move(backupRoot, persistentRoot);
    }

    private Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken)
        => dbContext.Database.BeginTransactionAsync(
            IsPostgreSql ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable,
            cancellationToken);

    private bool IsPostgreSql
        => dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;

    private sealed record TreeEntry(string FullPath, string RelativePath, bool IsDirectory);

    private sealed record CommandTarget(Guid DeploymentId, Guid ServerId, string GameIdentifier);

    private sealed record AuthenticatedCommand(
        Guid ServerId,
        Guid DeploymentId,
        Guid WorkerId,
        Guid CommandId,
        Guid LifecycleJobId,
        Guid CostOwnerId,
        string GameIdentifier);
}

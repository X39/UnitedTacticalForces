using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.StorageDemand;

[Scoped<StorageDemandService, IStorageDemandService>]
internal sealed class StorageDemandService(
    DatabaseContext dbContext,
    IGameServerContentSnapshotResolver contentResolver,
    ISteamWorkshopMetadataClient steamMetadataClient,
    IPersistentStoragePathResolver pathResolver,
    IServerFileLockProvider fileLockProvider,
    IOptions<StorageDemandOptions> options,
    ILogger<StorageDemandService> logger) : IStorageDemandService
{
    internal const string SteamWorkshopSource = "steam-workshop";
    internal const string GameRuntimeSource = "game-runtime";
    internal const string ArmaRuntimeApplication = "233780";
    internal const string ArmaWorkshopApplication = "107410";
    internal const string RuntimeItem = "dedicated-server";

    private readonly StorageDemandOptions _options = options.Value;

    public async Task<StorageDemandResult> EstimateAsync(
        string gameIdentifier,
        long? modPackDefinitionId,
        Guid? gameServerId,
        bool refreshMetadata,
        CancellationToken cancellationToken = default)
    {
        gameIdentifier = gameIdentifier.Trim().ToLowerInvariant();
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var references = await contentResolver.ResolveAsync(gameIdentifier, modPackDefinitionId, cancellationToken)
            .ConfigureAwait(false);
        var workshopReferences = references
            .Where(q => string.Equals(q.Source, SteamWorkshopSource, StringComparison.OrdinalIgnoreCase))
            .GroupBy(q => q.Identifier, StringComparer.Ordinal)
            .Select(q => q.First())
            .OrderBy(q => q.Identifier, StringComparer.Ordinal)
            .ToArray();

        if (refreshMetadata && workshopReferences.Length > 0)
            await RefreshSteamMetadataAsync(gameIdentifier, workshopReferences, now, cancellationToken)
                .ConfigureAwait(false);

        var observations = await dbContext.ContentStorageObservations
            .AsNoTracking()
            .Where(q => q.GameIdentifier == gameIdentifier)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var components = new List<StorageDemandComponent>();
        var missing = new List<string>();

        if (string.Equals(
                gameIdentifier,
                Arma3GameServerDefinitionRegistration.GameIdentifier,
                StringComparison.OrdinalIgnoreCase))
        {
            var runtime = observations.SingleOrDefault(q => q.SourceIdentifier == GameRuntimeSource
                                                             && q.ApplicationIdentifier == ArmaRuntimeApplication
                                                             && q.ItemIdentifier == RuntimeItem);
            components.Add(SelectComponent(
                runtime,
                "runtime:arma3",
                "Arma 3 dedicated server runtime",
                "runtime",
                _options.UnknownArmaRuntimeBytes,
                now,
                _options,
                missing));

            foreach (var reference in workshopReferences)
            {
                var observation = observations.SingleOrDefault(q => q.SourceIdentifier == SteamWorkshopSource
                                                                     && q.ApplicationIdentifier == ArmaWorkshopApplication
                                                                     && q.ItemIdentifier == reference.Identifier);
                components.Add(SelectComponent(
                    observation,
                    $"workshop:{reference.Identifier}",
                    string.IsNullOrWhiteSpace(reference.DisplayName)
                        ? $"Workshop item {reference.Identifier}"
                        : reference.DisplayName,
                    "workshop",
                    _options.UnknownWorkshopItemBytes,
                    now,
                    _options,
                    missing));
            }
        }
        else
        {
            missing.Add($"runtime:{gameIdentifier}");
            components.Add(new StorageDemandComponent(
                $"runtime:{gameIdentifier}",
                "Installed game runtime",
                "runtime",
                0,
                EStorageEstimateStatus.Unavailable,
                "unsupported-game-estimator",
                Detail: "No storage estimator is registered for this game."));
        }

        var persistentBytes = gameServerId is { } serverId
            ? await MeasurePersistentBytesAsync(serverId, cancellationToken).ConfigureAwait(false)
            : 0;
        components.Add(new StorageDemandComponent(
            "persistent-files",
            "Persistent server files",
            "persistent",
            persistentBytes,
            EStorageEstimateStatus.Measured,
            gameServerId.HasValue ? "api-filesystem-measurement" : "not-yet-created",
            now));
        components.Add(new StorageDemandComponent(
            "scenarios",
            "Scenario artifacts",
            "scenario",
            0,
            EStorageEstimateStatus.Estimated,
            "not-selected",
            Detail: "No Scenario domain selection exists for this server."));

        var steady = SumSaturated(components.Select(q => q.RequiredBytes));
        var downloadable = SumSaturated(components
            .Where(q => q.Kind is "runtime" or "workshop" or "scenario")
            .Select(q => q.RequiredBytes));
        var (workspace, margin, peak, policyOverflowed) = CalculatePeakPolicy(
            steady,
            downloadable,
            _options.PreparationWorkspaceMultiplier,
            _options.PreparationWorkspaceFixedBytes,
            _options.SafetyMarginPercent);
        var overflowed = steady == long.MaxValue || downloadable == long.MaxValue || policyOverflowed;
        if (overflowed)
            missing.Add("byte-aggregation-overflow");
        var status = overflowed || missing.Count > 0
            ? EStorageEstimateStatus.Incomplete
            : components.Any(q => q.Status is EStorageEstimateStatus.Estimated)
                ? EStorageEstimateStatus.Estimated
                : EStorageEstimateStatus.Measured;
        var measurementTimestamp = components
            .Where(q => q.Status is EStorageEstimateStatus.Measured && q.ObservedAt.HasValue)
            .Select(q => q.ObservedAt)
            .Max();
        return new StorageDemandResult
        {
            SteadyStateRequiredBytes = steady,
            PreparationWorkspaceBytes = workspace,
            SafetyMarginPercent = _options.SafetyMarginPercent,
            SafetyMarginBytes = margin,
            PeakPreparationRequiredBytes = peak,
            Status = status,
            CalculatedAt = now,
            MeasurementTimestamp = measurementTimestamp,
            MissingComponents = missing.Distinct(StringComparer.Ordinal).ToArray(),
            Components = components,
            EffectiveContent = references,
            ProfileSystemReserveBytes = Math.Max(0, _options.RuntimeProfileSystemReserveBytes),
        };
    }

    internal static StorageDemandComponent SelectComponent(
        ContentStorageObservation? observation,
        string identifier,
        string displayName,
        string kind,
        long fallbackBytes,
        Instant now,
        StorageDemandOptions options,
        ICollection<string> missing)
    {
        var measurementFresh = observation?.MeasuredAt is { } measuredAt
                               && now - measuredAt <= Duration.FromTimeSpan(options.MeasurementFreshness);
        var versionMatches = observation?.MeasuredVersion is { Length: > 0 } measuredVersion
                             && string.Equals(measuredVersion, observation.ExternalVersion, StringComparison.Ordinal);
        if (measurementFresh
            && observation?.MeasuredBytes is { } measuredBytes
            && (kind == "runtime" || versionMatches || observation.ReportedAt is null))
        {
            return new StorageDemandComponent(
                identifier,
                displayName,
                kind,
                measuredBytes,
                EStorageEstimateStatus.Measured,
                "worker-filesystem-measurement",
                observation.MeasuredAt,
                observation.MeasuredVersion);
        }

        var metadataFresh = observation?.ReportedAt is { } reportedAt
                            && now - reportedAt <= Duration.FromTimeSpan(options.SteamMetadataFreshness);
        if (metadataFresh && observation?.ReportedBytes is { } reportedBytes)
        {
            return new StorageDemandComponent(
                identifier,
                displayName,
                kind,
                reportedBytes,
                EStorageEstimateStatus.Estimated,
                observation.ObservationSource,
                observation.ReportedAt,
                observation.ExternalVersion);
        }

        missing.Add(identifier);
        var detail = observation is null
            ? "No observation is available; a conservative fallback is applied."
            : $"Observation status is {observation.ObservationStatus}; a conservative fallback is applied.";
        return new StorageDemandComponent(
            identifier,
            displayName,
            kind,
            Math.Max(0, fallbackBytes),
            EStorageEstimateStatus.Incomplete,
            "configured-fallback",
            observation?.MeasuredAt ?? observation?.ReportedAt,
            detail);
    }

    private async Task RefreshSteamMetadataAsync(
        string gameIdentifier,
        IReadOnlyCollection<Hosting.Contracts.WorkerProtocol.WorkerContentReference> references,
        Instant now,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.ContentStorageObservations
            .Where(q => q.GameIdentifier == gameIdentifier
                        && q.SourceIdentifier == SteamWorkshopSource
                        && q.ApplicationIdentifier == ArmaWorkshopApplication)
            .ToDictionaryAsync(q => q.ItemIdentifier, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);
        var stale = references
            .Select(q => q.Identifier)
            .Where(identifier => !existing.TryGetValue(identifier, out var observation)
                                 || observation.ReportedAt is null
                                 || now - observation.ReportedAt.Value
                                 > Duration.FromTimeSpan(_options.SteamMetadataFreshness))
            .ToArray();
        foreach (var batch in stale.Chunk(100))
        {
            IReadOnlyCollection<SteamWorkshopMetadata> metadata;
            try
            {
                metadata = await steamMetadataClient.GetDetailsAsync(batch, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    "Steam Workshop storage metadata refresh failed: {Failure}",
                    X39.UnitedTacticalForces.Api.Services.GameServerLifecycle.HostingFailureSanitizer.Sanitize(
                        exception.Message,
                        512));
                metadata = batch.Select(q => new SteamWorkshopMetadata(q, null, "metadata-unavailable", null, null))
                    .ToArray();
            }

            foreach (var item in metadata)
            {
                if (!existing.TryGetValue(item.ItemIdentifier, out var observation))
                {
                    observation = new ContentStorageObservation
                    {
                        PrimaryKey = Guid.NewGuid(),
                        GameIdentifier = gameIdentifier,
                        ComponentKind = "workshop",
                        SourceIdentifier = SteamWorkshopSource,
                        ApplicationIdentifier = ArmaWorkshopApplication,
                        ItemIdentifier = item.ItemIdentifier,
                        TimeStampCreated = now,
                    };
                    existing.Add(item.ItemIdentifier, observation);
                    dbContext.ContentStorageObservations.Add(observation);
                }
                observation.ReportedBytes = item.ReportedBytes;
                observation.ExternalVersion = item.Version;
                observation.ExternalUpdatedAt = item.UpdatedAt;
                observation.ReportedAt = now;
                observation.ObservationStatus = item.Status;
                observation.ObservationSource = "steam-web-api";
                observation.TimeStampUpdated = now;
            }
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<long> MeasurePersistentBytesAsync(Guid serverId, CancellationToken cancellationToken)
    {
        await using var serverLock = await fileLockProvider.AcquireAsync(serverId, cancellationToken)
            .ConfigureAwait(false);
        var root = pathResolver.ResolvePersistentRoot(serverId);
        if (!Directory.Exists(root))
            return 0;
        SafeServerPath.RejectExistingReparsePoints(root, root);
        return MeasureTree(root, cancellationToken);
    }

    internal static long MeasureTree(string root, CancellationToken cancellationToken = default)
    {
        long total = 0;
        var pending = new Stack<string>();
        pending.Push(Path.GetFullPath(root));
        while (pending.TryPop(out var directory))
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Storage measurement refuses links beneath the controlled root.");
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    pending.Push(path);
                    continue;
                }
                if (!Path.GetFileName(path).StartsWith(".utf-", StringComparison.OrdinalIgnoreCase))
                    total = AddSaturated(total, new FileInfo(path).Length);
            }
        }
        return total;
    }

    internal static long SumSaturated(IEnumerable<long> values)
        => values.Aggregate(0L, AddSaturated);

    internal static long AddSaturated(long left, long right)
    {
        if (left < 0 || right < 0 || left > long.MaxValue - right)
            return long.MaxValue;
        return left + right;
    }

    internal static (long Workspace, long Margin, long Peak, bool Overflowed) CalculatePeakPolicy(
        long steady,
        long downloadable,
        decimal workspaceMultiplier,
        long fixedWorkspaceBytes,
        decimal safetyMarginPercent)
    {
        var workspace = AddSaturated(
            MultiplySaturated(downloadable, workspaceMultiplier),
            Math.Max(0, fixedWorkspaceBytes));
        var beforeMargin = AddSaturated(steady, workspace);
        var margin = PercentSaturated(beforeMargin, safetyMarginPercent);
        var peak = AddSaturated(beforeMargin, margin);
        return (workspace, margin, peak, workspace == long.MaxValue
                                          || margin == long.MaxValue
                                          || peak == long.MaxValue);
    }

    private static long MultiplySaturated(long value, decimal multiplier)
    {
        if (value < 0 || multiplier < 0)
            return long.MaxValue;
        try
        {
            return checked((long) decimal.Ceiling(value * multiplier));
        }
        catch (OverflowException)
        {
            return long.MaxValue;
        }
    }

    private static long PercentSaturated(long value, decimal percent)
        => MultiplySaturated(value, percent / 100m);
}

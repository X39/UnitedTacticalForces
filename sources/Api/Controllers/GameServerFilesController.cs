using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.GameServerConfiguration;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.UnitedTacticalForces.GameServerImplementations;
using StorageFailureSanitizer = X39.UnitedTacticalForces.Api.Services.HostingRuntime.HostingFailureSanitizer;

namespace X39.UnitedTacticalForces.Api.Controllers;

[ApiController]
[Authorize]
[Route(Constants.Routes.GameServers + "/{gameServerId:guid}/files/{context}")]
public sealed class GameServerFilesController(
    DatabaseContext dbContext,
    IServerFileStorage fileStorage,
    IServerFileClassifier fileClassifier,
    IGameServerDefinitionCatalog gameDefinitions,
    IGameServerConfigurationService configurationService,
    IPreservationSelectionResolver preservationSelectionResolver,
    IHostingAuthorizationService authorizationService,
    IHostingAuditWriter auditWriter,
    ILogger<GameServerFilesController> logger) : ControllerBase
{
    private const long MaximumUploadBytes = 16 * 1024 * 1024;

    [HttpGet]
    [Authorize(Claims.ResourceBased.Server.View)]
    [ProducesResponseType<IReadOnlyCollection<HostingFileDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> BrowseAsync(
        [FromRoute] Guid gameServerId,
        [FromRoute] string context,
        [FromQuery] string? path,
        CancellationToken cancellationToken)
    {
        if (!TryParseContext(context, out var fileContext))
            return BadRequest(new { error = "Unknown file context." });
        try
        {
            var deploymentId = await ResolveDeploymentAsync(gameServerId, fileContext, cancellationToken)
                .ConfigureAwait(false);
            var capability = await GetCapabilityAsync(gameServerId, cancellationToken).ConfigureAwait(false);
            var canViewSensitive = HasModifyPermission(gameServerId);
            var normalizedPath = SafeServerPath.Normalize(path ?? string.Empty);
            ResolvedGameConfiguration? configuration = null;
            if (fileContext is EFileContext.Persistent
                && GameConfigurationImplementations.TryGet(capability.GameIdentifier, out _))
                configuration = await configurationService.ResolveAsync(gameServerId, cancellationToken)
                    .ConfigureAwait(false);
            IReadOnlyList<ServerFileEntry> entries;
            try
            {
                entries = await fileStorage
                    .BrowseAsync(gameServerId, deploymentId, fileContext, normalizedPath, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (DirectoryNotFoundException) when (
                configuration is not null && HasGeneratedDescendant(configuration, normalizedPath))
            {
                entries = [];
            }
            var projected = ProjectEffectiveEntries(
                entries,
                capability,
                configuration,
                normalizedPath,
                canViewSensitive);
            if (fileContext is EFileContext.Persistent
                && await dbContext.GameServerDeployments.AnyAsync(
                    q => q.GameServerFk == gameServerId && q.TimeStampResolved == null,
                    cancellationToken).ConfigureAwait(false))
            {
                var selections = await GetSelectionsAsync(gameServerId, cancellationToken).ConfigureAwait(false);
                projected = projected
                    .Select(q => IsProtectedByPreservation(q.Path, q.IsDirectory, selections)
                        ? q with { CanModify = false }
                        : q)
                    .ToArray();
            }
            return Ok(projected);
        }
        catch (Exception ex) when (TryMapStorageException(ex, out var result))
        {
            return result;
        }
    }

    [HttpGet("content")]
    [Authorize(Claims.ResourceBased.Server.View)]
    public async Task<IActionResult> DownloadAsync(
        [FromRoute] Guid gameServerId,
        [FromRoute] string context,
        [FromQuery] string path,
        CancellationToken cancellationToken)
    {
        if (!TryParseContext(context, out var fileContext))
            return BadRequest(new { error = "Unknown file context." });
        try
        {
            var deploymentId = await ResolveDeploymentAsync(gameServerId, fileContext, cancellationToken)
                .ConfigureAwait(false);
            var capability = await GetCapabilityAsync(gameServerId, cancellationToken).ConfigureAwait(false);
            var canViewSensitive = HasModifyPermission(gameServerId);
            var generatedTarget = fileContext is EFileContext.Persistent
                ? GetGeneratedTarget(capability.GameIdentifier, path)
                : null;
            if ((GameServerFileCapabilityPolicy.Resolve(capability, path, false)?.Sensitive == true
                 || generatedTarget?.Sensitive == true)
                && !canViewSensitive)
                return Forbid();
            try
            {
                var download = await fileStorage
                    .OpenReadAsync(gameServerId, deploymentId, fileContext, path, cancellationToken)
                    .ConfigureAwait(false);
                return File(download.Content, "application/octet-stream", download.FileName, enableRangeProcessing: true);
            }
            catch (FileNotFoundException) when (generatedTarget is not null)
            {
                var configuration = await configurationService.ResolveAsync(gameServerId, cancellationToken)
                    .ConfigureAwait(false);
                if (!IsTargetComplete(configuration, generatedTarget.Path))
                {
                    return Conflict(new
                    {
                        error = "The generated configuration file is incomplete.",
                        missingKeys = MissingTargetKeys(configuration, generatedTarget.Path),
                    });
                }
                var rendered = configuration.Implementation.Render(configuration.Values)
                    .Single(q => q.Path.Equals(generatedTarget.Path, StringComparison.OrdinalIgnoreCase));
                return File(rendered.Content, "application/octet-stream", Path.GetFileName(rendered.Path));
            }
            catch (DirectoryNotFoundException) when (generatedTarget is not null)
            {
                var configuration = await configurationService.ResolveAsync(gameServerId, cancellationToken)
                    .ConfigureAwait(false);
                if (!IsTargetComplete(configuration, generatedTarget.Path))
                {
                    return Conflict(new
                    {
                        error = "The generated configuration file is incomplete.",
                        missingKeys = MissingTargetKeys(configuration, generatedTarget.Path),
                    });
                }
                var rendered = configuration.Implementation.Render(configuration.Values)
                    .Single(q => q.Path.Equals(generatedTarget.Path, StringComparison.OrdinalIgnoreCase));
                return File(rendered.Content, "application/octet-stream", Path.GetFileName(rendered.Path));
            }
        }
        catch (Exception ex) when (TryMapStorageException(ex, out var result))
        {
            return result;
        }
    }

    [HttpPut]
    [RequestSizeLimit(MaximumUploadBytes)]
    [ProducesResponseType<HostingFileDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UploadAsync(
        [FromRoute] Guid gameServerId,
        [FromRoute] string context,
        [FromQuery] string path,
        [FromQuery] bool confirmGeneratedOverride,
        CancellationToken cancellationToken)
    {
        if (!TryParseContext(context, out var fileContext))
            return BadRequest(new { error = "Unknown file context." });
        if (!HasModifyPermission(gameServerId))
            return Forbid();
        try
        {
            var capability = await GetCapabilityAsync(gameServerId, cancellationToken).ConfigureAwait(false);
            var generatedTarget = fileContext is EFileContext.Persistent
                ? GetGeneratedTarget(capability.GameIdentifier, path)
                : null;
            if (generatedTarget is not null && !confirmGeneratedOverride)
            {
                return Conflict(new
                {
                    error = "Uploading here overrides a generated configuration file; explicit confirmation is required.",
                    requiresOverrideConfirmation = true,
                });
            }
            if (!await CanMutatePathAsync(gameServerId, fileContext, path, false, cancellationToken)
                    .ConfigureAwait(false))
                return Conflict(new { error = "The selected file classification is read-only." });
            var deploymentId = await ResolveDeploymentAsync(gameServerId, fileContext, cancellationToken)
                .ConfigureAwait(false);
            await AuditMutationIntentAsync(
                gameServerId,
                deploymentId,
                EHostingAuditEventKind.FileUpdated,
                "Server file upload requested.",
                path,
                cancellationToken).ConfigureAwait(false);
            var entry = await fileStorage
                .UploadAsync(gameServerId, deploymentId, fileContext, path, Request.Body, true, cancellationToken)
                .ConfigureAwait(false);
            var projected = Project(entry, capability);
            return Ok(generatedTarget is null
                ? projected
                : projected with
                {
                    IsGeneratedTarget = true,
                    OverridesGenerated = true,
                    Origin = EHostingFileOrigin.PersistentOverride,
                });
        }
        catch (Exception ex) when (TryMapStorageException(ex, out var result))
        {
            return result;
        }
    }

    [HttpPost]
    [ProducesResponseType<HostingFileDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateDirectoryAsync(
        [FromRoute] Guid gameServerId,
        [FromRoute] string context,
        [FromBody] DirectoryCreationPayload payload,
        CancellationToken cancellationToken)
    {
        if (!TryParseContext(context, out var fileContext))
            return BadRequest(new { error = "Unknown file context." });
        if (!HasModifyPermission(gameServerId))
            return Forbid();
        try
        {
            if (fileContext is EFileContext.Persistent
                && GetGeneratedTarget(
                    (await GetCapabilityAsync(gameServerId, cancellationToken).ConfigureAwait(false)).GameIdentifier,
                    payload.Path) is not null)
                return Conflict(new { error = "A generated file target cannot be replaced by a directory." });
            if (!await CanMutatePathAsync(gameServerId, fileContext, payload.Path, true, cancellationToken)
                    .ConfigureAwait(false))
                return Conflict(new { error = "The selected directory classification is read-only." });
            var deploymentId = await ResolveDeploymentAsync(gameServerId, fileContext, cancellationToken)
                .ConfigureAwait(false);
            await AuditMutationIntentAsync(
                gameServerId,
                deploymentId,
                EHostingAuditEventKind.FileCreated,
                "Server directory creation requested.",
                payload.Path,
                cancellationToken).ConfigureAwait(false);
            var entry = await fileStorage
                .CreateDirectoryAsync(gameServerId, deploymentId, fileContext, payload.Path, cancellationToken)
                .ConfigureAwait(false);
            return Created(
                string.Empty,
                Project(entry, await GetCapabilityAsync(gameServerId, cancellationToken).ConfigureAwait(false)));
        }
        catch (Exception ex) when (TryMapStorageException(ex, out var result))
        {
            return result;
        }
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAsync(
        [FromRoute] Guid gameServerId,
        [FromRoute] string context,
        [FromQuery] string path,
        [FromQuery] bool recursive,
        CancellationToken cancellationToken)
    {
        if (!TryParseContext(context, out var fileContext))
            return BadRequest(new { error = "Unknown file context." });
        if (!HasModifyPermission(gameServerId))
            return Forbid();
        try
        {
            var deploymentId = await ResolveDeploymentAsync(gameServerId, fileContext, cancellationToken)
                .ConfigureAwait(false);
            var existing = await ResolveExistingEntryAsync(
                    gameServerId,
                    deploymentId,
                    fileContext,
                    path,
                    cancellationToken)
                .ConfigureAwait(false);
            if (existing is null)
            {
                var capability = await GetCapabilityAsync(gameServerId, cancellationToken).ConfigureAwait(false);
                if (fileContext is EFileContext.Persistent
                    && GetGeneratedTarget(capability.GameIdentifier, path) is not null)
                    return Conflict(new { error = "A generated target cannot be raw-deleted." });
                return NotFound();
            }
            if (!await CanMutatePathAsync(
                    gameServerId,
                    fileContext,
                    path,
                    existing.IsDirectory,
                    cancellationToken)
                    .ConfigureAwait(false))
                return Conflict(new { error = "The selected file classification is read-only." });
            await AuditMutationIntentAsync(
                gameServerId,
                deploymentId,
                EHostingAuditEventKind.FileDeleted,
                "Server path deletion requested.",
                path,
                cancellationToken).ConfigureAwait(false);
            await fileStorage
                .DeleteAsync(gameServerId, deploymentId, fileContext, path, recursive, cancellationToken)
                .ConfigureAwait(false);
            return NoContent();
        }
        catch (Exception ex) when (TryMapStorageException(ex, out var result))
        {
            return result;
        }
    }

    private async Task<bool> CanMutatePathAsync(
        Guid gameServerId,
        EFileContext context,
        string path,
        bool isDirectory,
        CancellationToken cancellationToken)
    {
        var normalized = SafeServerPath.Normalize(path, false);
        var classification = fileClassifier.Classify(normalized, isDirectory);
        if (!fileClassifier.IsWritable(context, classification))
            return false;
        var capability = await GetCapabilityAsync(gameServerId, cancellationToken).ConfigureAwait(false);
        if (!GameServerFileCapabilityPolicy.CanMutate(capability, normalized, isDirectory, context))
            return false;
        if (context is not EFileContext.Persistent
            || !await dbContext.GameServerDeployments.AnyAsync(
                q => q.GameServerFk == gameServerId && q.TimeStampResolved == null,
                cancellationToken).ConfigureAwait(false))
            return true;

        var selections = await GetSelectionsAsync(gameServerId, cancellationToken).ConfigureAwait(false);
        return !IsProtectedByPreservation(normalized, isDirectory, selections);
    }

    private bool HasModifyPermission(Guid gameServerId)
        => authorizationService.HasServerPermission(
            User,
            gameServerId,
            Claims.ResourceBased.Server.Modify);

    private Task<PreservationPathSelection[]> GetSelectionsAsync(
        Guid gameServerId,
        CancellationToken cancellationToken)
        => dbContext.PreservationSelections
            .AsNoTracking()
            .Where(q => q.GameServerFk == gameServerId)
            .Select(q => new PreservationPathSelection(q.PrimaryKey, q.Path, q.Scope, q.Mode))
            .ToArrayAsync(cancellationToken);

    private bool IsProtectedByPreservation(
        string path,
        bool isDirectory,
        IReadOnlyCollection<PreservationPathSelection> selections)
    {
        if (preservationSelectionResolver.Resolve(path, isDirectory, selections) is not null)
            return true;
        if (!isDirectory)
            return false;
        var normalized = SafeServerPath.Normalize(path, false).TrimEnd('/');
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return selections.Any(q =>
        {
            var selected = SafeServerPath.Normalize(q.RelativePath, q.Scope is not EPreservationScope.File);
            return selected.Length == 0
                   || q.Scope is not EPreservationScope.File && selected.Equals(normalized, comparison)
                   || selected.StartsWith(normalized + "/", comparison);
        });
    }

    private async Task<Guid?> ResolveDeploymentAsync(
        Guid gameServerId,
        EFileContext context,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.GameServers
                .AsNoTracking()
                .AnyAsync(q => q.PrimaryKey == gameServerId && !q.IsArchived, cancellationToken)
                .ConfigureAwait(false))
            throw new FileNotFoundException("The game server does not exist.");
        if (context is EFileContext.Persistent)
            return null;
        return await dbContext.GameServerDeployments
                   .AsNoTracking()
                   .Where(q => q.GameServerFk == gameServerId && q.TimeStampResolved == null)
                   .Select(q => (Guid?) q.PrimaryKey)
                   .SingleOrDefaultAsync(cancellationToken)
                   .ConfigureAwait(false)
               ?? throw new DirectoryNotFoundException("The server has no active live runtime.");
    }

    private async Task<ServerFileEntry?> ResolveExistingEntryAsync(
        Guid gameServerId,
        Guid? deploymentId,
        EFileContext context,
        string path,
        CancellationToken cancellationToken)
    {
        var normalized = SafeServerPath.Normalize(path, false);
        var separator = normalized.LastIndexOf('/');
        var parent = separator < 0 ? string.Empty : normalized[..separator];
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var entries = await fileStorage
            .BrowseAsync(gameServerId, deploymentId, context, parent, cancellationToken)
            .ConfigureAwait(false);
        return entries.SingleOrDefault(q => q.RelativePath.Equals(normalized, comparison));
    }

    private async Task AuditMutationIntentAsync(
        Guid serverId,
        Guid? deploymentId,
        EHostingAuditEventKind kind,
        string summary,
        string path,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var actorId))
            throw new UnauthorizedAccessException();
        auditWriter.Add(
            dbContext,
            kind,
            summary,
            actorId,
            serverId,
            deploymentId,
            details: $"Path={SafeServerPath.Normalize(path, false)}");
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<GameServerDefinition> GetCapabilityAsync(
        Guid gameServerId,
        CancellationToken cancellationToken)
    {
        var gameIdentifier = await dbContext.GameServers
            .AsNoTracking()
            .Where(q => q.PrimaryKey == gameServerId)
            .Select(q => q.GameIdentifier)
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);
        return gameDefinitions.GetDefinitions().Single(q => string.Equals(
            q.GameIdentifier,
            gameIdentifier,
            StringComparison.OrdinalIgnoreCase));
    }

    private static HostingFileDto Project(ServerFileEntry entry, GameServerDefinition capability)
    {
        var gameRule = GameServerFileCapabilityPolicy.Resolve(
            capability,
            entry.RelativePath,
            entry.IsDirectory);
        return new HostingFileDto
        {
            Path = entry.RelativePath,
            Name = entry.Name,
            IsDirectory = entry.IsDirectory,
            Size = entry.Size,
            ModifiedAt = Instant.FromDateTimeOffset(entry.ModifiedAt),
            Context = entry.Context,
            Classification = gameRule?.Classification ?? entry.Classification,
            CanModify = entry.IsWritable
                        && GameServerFileCapabilityPolicy.CanMutate(
                            capability,
                            entry.RelativePath,
                            entry.IsDirectory,
                            entry.Context),
            Origin = entry.Context is EFileContext.Persistent
                ? EHostingFileOrigin.Persistent
                : EHostingFileOrigin.Live,
            ModificationMode = entry.IsWritable
                ? EHostingFileModificationMode.RawFile
                : EHostingFileModificationMode.None,
            ConfigurationStatus = EConfigurationContentStatus.NotApplicable,
        };
    }

    private static HostingFileDto[] ProjectEffectiveEntries(
        IReadOnlyList<ServerFileEntry> physicalEntries,
        GameServerDefinition capability,
        ResolvedGameConfiguration? configuration,
        string directoryPath,
        bool canModify)
    {
        var result = physicalEntries
            .Select(q => Project(q, capability))
            .ToDictionary(q => q.Name, StringComparer.OrdinalIgnoreCase);
        if (configuration is null)
            return result.Values.OrderByDescending(q => q.IsDirectory).ThenBy(q => q.Name).ToArray();

        var normalizedDirectory = directoryPath.Trim('/');
        foreach (var target in configuration.Implementation.Targets)
        {
            var normalizedTarget = SafeServerPath.Normalize(target.Path, false);
            var relative = normalizedDirectory.Length == 0
                ? normalizedTarget
                : normalizedTarget.StartsWith(normalizedDirectory + "/", StringComparison.OrdinalIgnoreCase)
                    ? normalizedTarget[(normalizedDirectory.Length + 1)..]
                    : string.Empty;
            if (relative.Length == 0)
                continue;
            var separator = relative.IndexOf('/');
            var childName = separator < 0 ? relative : relative[..separator];
            var childPath = normalizedDirectory.Length == 0 ? childName : $"{normalizedDirectory}/{childName}";
            var isDirectory = separator >= 0;

            if (result.TryGetValue(childName, out var existing))
            {
                if (!isDirectory && childPath.Equals(normalizedTarget, StringComparison.OrdinalIgnoreCase))
                {
                    result[childName] = existing with
                    {
                        IsGeneratedTarget = true,
                        OverridesGenerated = !existing.IsDirectory,
                        Origin = existing.IsDirectory
                            ? EHostingFileOrigin.Persistent
                            : EHostingFileOrigin.PersistentOverride,
                        ConfigurationStatus = existing.IsDirectory
                            ? EConfigurationContentStatus.Conflict
                            : EConfigurationContentStatus.Ready,
                    };
                }
                continue;
            }

            if (isDirectory)
            {
                result[childName] = new HostingFileDto
                {
                    Path = childPath,
                    Name = childName,
                    IsDirectory = true,
                    Context = EFileContext.Persistent,
                    Classification = EFileClassification.Configuration,
                    Origin = EHostingFileOrigin.Generated,
                    ConfigurationStatus = EConfigurationContentStatus.Ready,
                };
                continue;
            }

            var complete = IsTargetComplete(configuration, normalizedTarget);
            long? size = null;
            if (complete)
            {
                size = configuration.Implementation.Render(configuration.Values)
                    .Single(q => q.Path.Equals(normalizedTarget, StringComparison.OrdinalIgnoreCase))
                    .Content.LongLength;
            }
            result[childName] = new HostingFileDto
            {
                Path = normalizedTarget,
                Name = childName,
                IsDirectory = false,
                Size = size,
                Context = EFileContext.Persistent,
                Classification = EFileClassification.Configuration,
                CanModify = canModify,
                Origin = EHostingFileOrigin.Generated,
                ModificationMode = canModify
                    ? EHostingFileModificationMode.StructuredConfiguration
                    : EHostingFileModificationMode.None,
                IsGeneratedTarget = true,
                ConfigurationStatus = complete
                    ? EConfigurationContentStatus.Ready
                    : EConfigurationContentStatus.Incomplete,
            };
        }
        return result.Values.OrderByDescending(q => q.IsDirectory).ThenBy(q => q.Name).ToArray();
    }

    private static bool HasGeneratedDescendant(ResolvedGameConfiguration configuration, string directoryPath)
    {
        var normalized = directoryPath.Trim('/');
        return configuration.Implementation.Targets.Any(q => normalized.Length == 0
            || SafeServerPath.Normalize(q.Path, false)
                .StartsWith(normalized + "/", StringComparison.OrdinalIgnoreCase));
    }

    private static GeneratedConfigurationTarget? GetGeneratedTarget(string gameIdentifier, string path)
    {
        if (!GameConfigurationImplementations.TryGet(gameIdentifier, out var implementation))
            return null;
        var normalized = SafeServerPath.Normalize(path, false);
        return implementation.Targets.SingleOrDefault(q =>
            SafeServerPath.Normalize(q.Path, false).Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsTargetComplete(ResolvedGameConfiguration configuration, string targetPath)
        => !configuration.Entries.Any(q => q.Definition.TargetPath.Equals(targetPath, StringComparison.OrdinalIgnoreCase)
                                           && q.Definition.Required
                                           && (!q.Value.HasValue || !q.IsCompatible));

    private static string[] MissingTargetKeys(ResolvedGameConfiguration configuration, string targetPath)
        => configuration.Entries
            .Where(q => q.Definition.TargetPath.Equals(targetPath, StringComparison.OrdinalIgnoreCase)
                        && q.Definition.Required
                        && (!q.Value.HasValue || !q.IsCompatible))
            .Select(q => q.Definition.Key)
            .ToArray();

    private static bool TryParseContext(string context, out EFileContext parsed)
        => Enum.TryParse(context, true, out parsed) && Enum.IsDefined(parsed);

    private bool TryMapStorageException(Exception exception, out IActionResult result)
    {
        if (exception is IOException)
        {
            logger.LogWarning(
                "A game-server storage request failed: {Failure}",
                StorageFailureSanitizer.Sanitize(exception.Message));
        }
        result = exception switch
        {
            FileNotFoundException or DirectoryNotFoundException => new NotFoundObjectResult(new
            {
                error = "The requested file or directory does not exist.",
            }),
            ArgumentException => new BadRequestObjectResult(new { error = exception.Message }),
            UnauthorizedAccessException => new ForbidResult(),
            IOException => new ConflictObjectResult(new
            {
                error = "The storage operation could not be completed.",
            }),
            LiveWorkerFileAccessException => new ObjectResult(new
            {
                error = "Live files are available only through the authenticated cloud worker.",
            })
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable,
            },
            _ => null!,
        };
        return result is not null;
    }

    public sealed record DirectoryCreationPayload
    {
        public string Path { get; init; } = string.Empty;
    }
}

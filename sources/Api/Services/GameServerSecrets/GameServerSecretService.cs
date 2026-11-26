using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Database.Core;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.Util.DependencyInjection.Attributes;
using GameDefinition = X39.UnitedTacticalForces.Api.Services.GameServerDefinition.GameServerDefinition;

namespace X39.UnitedTacticalForces.Api.Services.GameServerSecrets;

/// <summary>
/// Stores, rotates, reveals and resolves encrypted game server secrets while keeping every change audited and
/// recorded in the pending-start signal.
/// </summary>
[Scoped<GameServerSecretService, IGameServerSecretService>]
internal sealed class GameServerSecretService(
    DatabaseContext dbContext,
    IGameServerDefinitionCatalog definitions,
    IDataProtectionProvider protectionProvider,
    IHostingAuditWriter auditWriter) : IGameServerSecretService
{
    private readonly IDataProtector _protector = protectionProvider.CreateProtector(
        "UnitedTacticalForces.Hosting.GameServerSecrets.v1");

    public void EnsureGeneratedSecrets(GameServer server, GameDefinition definition, Guid? actorId, Instant now)
    {
        foreach (var secret in definition.Secrets.Where(q => q.Generated))
        {
            AddVersion(server, secret.Key, Generate(), EGameServerSecretOrigin.Generated, actorId, now);
        }
    }

    public async Task<IReadOnlyCollection<GameServerSecretStatusDto>> GetStatusesAsync(
        Guid serverId,
        CancellationToken cancellationToken = default)
    {
        var server = await dbContext.GameServers.AsNoTracking()
            .SingleOrDefaultAsync(q => q.PrimaryKey == serverId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new HostingNotFoundException("The game server does not exist.");
        var definition = definitions.Find(server.GameIdentifier)
                         ?? throw new HostingValidationException("The server game is unsupported.");
        var current = await dbContext.GameServerSecrets.AsNoTracking()
            .Where(q => q.GameServerFk == serverId && q.IsCurrent)
            .ToDictionaryAsync(q => q.Key, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);
        return definition.Secrets.Select(q =>
        {
            current.TryGetValue(q.Key, out var value);
            return new GameServerSecretStatusDto(
                q.Key,
                q.DisplayName,
                q.RequiredForStart,
                q.Generated,
                q.Revealable,
                q.ExternalProvider,
                value is not null,
                value?.Version,
                value?.Origin,
                value?.TimeStampCreated,
                !q.Generated || q.AcceptsChosenValue,
                q.ValueFormat);
        }).ToArray();
    }

    /// <inheritdoc />
    public async Task SetAsync(
        Guid serverId,
        string key,
        string value,
        EGameServerSecretOrigin origin,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        if (origin is EGameServerSecretOrigin.Generated)
            throw new HostingValidationException("Generated secrets must be rotated rather than supplied.");
        value = value.Trim();
        if (value.Length is 0 or > 4096)
            throw new HostingValidationException("The secret value is empty or too long.");
        var (server, definition) = await LoadAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (server.IsArchived)
            throw new HostingConflictException("Archived servers cannot change their credentials.");
        var secretDefinition = FindSecret(definition, key);
        if (secretDefinition.Generated && !secretDefinition.AcceptsChosenValue)
            throw new HostingValidationException("This secret is managed by the platform.");
        if (secretDefinition.ExternalProvider != (origin is EGameServerSecretOrigin.ExternalProvider))
            throw new HostingValidationException(secretDefinition.ExternalProvider
                ? "This secret must be supplied by its external provider."
                : "This secret is user-provided rather than provider-issued.");
        ValidateValue(value, secretDefinition.ValueFormat);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        RetireCurrent(server, secretDefinition.Key, now);
        AddVersion(server, secretDefinition.Key, value, origin, actorId, now);
        PendingStartChanges.MarkChanged(server, EGameServerChangeArea.Credentials, now);
        auditWriter.Add(dbContext, EHostingAuditEventKind.ServerSecretUpdated, "Server secret updated.", actorId, serverId, details: $"Key={secretDefinition.Key}");
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<GameServerSecretValueDto> RevealAsync(
        Guid serverId,
        string key,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var (_, definition) = await LoadAsync(serverId, cancellationToken).ConfigureAwait(false);
        var secretDefinition = FindSecret(definition, key);
        if (!secretDefinition.Revealable)
            throw new HostingForbiddenException("This secret cannot be revealed.");
        var secret = await CurrentAsync(serverId, secretDefinition.Key, cancellationToken).ConfigureAwait(false)
                     ?? throw new HostingNotFoundException("The secret is not configured.");
        auditWriter.Add(dbContext, EHostingAuditEventKind.ServerSecretRevealed, "Server secret revealed.", actorId, serverId, details: $"Key={secretDefinition.Key}; Version={secret.Version}");
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new GameServerSecretValueDto(secret.Key, _protector.Unprotect(secret.ProtectedValue), secret.Version);
    }

    /// <inheritdoc />
    public async Task<GameServerSecretValueDto> RotateAsync(
        Guid serverId,
        string key,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var (server, definition) = await LoadAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (server.IsArchived)
            throw new HostingConflictException("Archived servers cannot change their credentials.");
        var secretDefinition = FindSecret(definition, key);
        if (!secretDefinition.Generated)
            throw new HostingValidationException("Only platform-generated secrets can be rotated.");
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var value = Generate();
        RetireCurrent(server, secretDefinition.Key, now);
        var version = AddVersion(server, secretDefinition.Key, value, EGameServerSecretOrigin.Generated, actorId, now);
        PendingStartChanges.MarkChanged(server, EGameServerChangeArea.Credentials, now);
        auditWriter.Add(
            dbContext,
            EHostingAuditEventKind.ServerSecretRotated,
            "Server secret rotated; the new value was returned once to the acting user.",
            actorId,
            serverId,
            details: $"Key={secretDefinition.Key}; Version={version}");
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new GameServerSecretValueDto(secretDefinition.Key, value, version);
    }

    public async Task<IReadOnlyDictionary<string, string>> ResolveRequiredAsync(
        Guid serverId,
        CancellationToken cancellationToken = default)
    {
        var (_, definition) = await LoadAsync(serverId, cancellationToken).ConfigureAwait(false);
        var current = await dbContext.GameServerSecrets.AsNoTracking()
            .Where(q => q.GameServerFk == serverId && q.IsCurrent)
            .ToDictionaryAsync(q => q.Key, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);
        var missing = definition.Secrets.Where(q => q.RequiredForStart && !current.ContainsKey(q.Key)).ToArray();
        if (missing.Length > 0)
            throw new HostingValidationException($"Required server secret '{missing[0].DisplayName}' is not configured.");
        return current.ToDictionary(q => q.Key, q => _protector.Unprotect(q.Value.ProtectedValue), StringComparer.Ordinal);
    }

    public async Task<string> CreateProtectedSnapshotAsync(
        Guid serverId,
        CancellationToken cancellationToken = default)
        => _protector.Protect(JsonSerializer.Serialize(
            await ResolveRequiredAsync(serverId, cancellationToken).ConfigureAwait(false)));

    public IReadOnlyDictionary<string, string> UnprotectSnapshot(string? protectedSnapshot)
        => string.IsNullOrWhiteSpace(protectedSnapshot)
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : JsonSerializer.Deserialize<Dictionary<string, string>>(_protector.Unprotect(protectedSnapshot))
              ?? new Dictionary<string, string>(StringComparer.Ordinal);

    public async Task MigrateLegacyConfigurationSecretsAsync(CancellationToken cancellationToken = default)
    {
        const string runId = "configuration-secrets-v1";
        if (await dbContext.ContentBackfillRuns.AnyAsync(q => q.Identifier == runId, cancellationToken)
                .ConfigureAwait(false))
            return;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var servers = await dbContext.GameServers.Include(q => q.Secrets)
            .Where(q => q.GameIdentifier == Arma3GameServerDefinitionRegistration.GameIdentifier)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var mappings = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["server/server.cfg:password"] = "join-password",
            ["server/server.cfg:passwordAdmin"] = "admin-password",
            ["server/server.cfg:serverCommandPassword"] = "server-command-password",
            ["server/battleye/beserver.cfg:RConPassword"] = "battleye-rcon-password",
        };
        foreach (var server in servers)
        {
            var profileRevisionId = server.PinnedConfigurationProfileRevisionFk;
            if (profileRevisionId is null && server.SelectedConfigurationProfileFk is { } profileId)
                profileRevisionId = await dbContext.ConfigurationProfileRevisions
                    .Where(q => q.ConfigurationProfileFk == profileId)
                    .OrderByDescending(q => q.RevisionNumber).Select(q => (Guid?)q.PrimaryKey)
                    .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            var profileValues = profileRevisionId is { } revisionId
                ? await dbContext.ConfigurationProfileValues.Where(q => q.ConfigurationProfileRevisionFk == revisionId)
                    .ToDictionaryAsync(q => q.DefinitionKey, q => q.ValueJson, StringComparer.Ordinal, cancellationToken)
                    .ConfigureAwait(false)
                : new Dictionary<string, string>(StringComparer.Ordinal);
            var serverValues = await dbContext.GameServerConfigurationValues.Where(q => q.GameServerFk == server.PrimaryKey)
                .ToDictionaryAsync(q => q.DefinitionKey, q => q.ValueJson, StringComparer.Ordinal, cancellationToken)
                .ConfigureAwait(false);
            var now = NodaSystemClock.Instance.GetCurrentInstant();
            foreach (var (configurationKey, secretKey) in mappings)
            {
                if (server.Secrets.Any(q => q.Key == secretKey && q.IsCurrent)) continue;
                var json = serverValues.GetValueOrDefault(configurationKey) ?? profileValues.GetValueOrDefault(configurationKey);
                string? value = null;
                if (json is not null)
                {
                    using var document = JsonDocument.Parse(json);
                    if (document.RootElement.ValueKind == JsonValueKind.String)
                        value = document.RootElement.GetString();
                }
                if (!string.IsNullOrEmpty(value))
                    AddVersion(server, secretKey, value, EGameServerSecretOrigin.UserProvided, server.CreatedByFk, now);
                else if (secretKey != "join-password")
                    AddVersion(server, secretKey, Generate(), EGameServerSecretOrigin.Generated, server.CreatedByFk, now);
            }
        }
        var sensitiveKeys = mappings.Keys.ToArray();
        await dbContext.GameServerConfigurationValues.Where(q => sensitiveKeys.Contains(q.DefinitionKey))
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.ConfigurationProfileValues.Where(q => sensitiveKeys.Contains(q.DefinitionKey))
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        dbContext.ContentBackfillRuns.Add(new ContentBackfillRun
        {
            Identifier = runId,
            ImplementationVersion = runId,
            TimeStampCompleted = NodaSystemClock.Instance.GetCurrentInstant(),
        });
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<(GameServer Server, GameDefinition Definition)> LoadAsync(
        Guid serverId,
        CancellationToken cancellationToken)
    {
        var server = await dbContext.GameServers.Include(q => q.Secrets)
            .SingleOrDefaultAsync(q => q.PrimaryKey == serverId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new HostingNotFoundException("The game server does not exist.");
        return (server, definitions.Find(server.GameIdentifier)
                        ?? throw new HostingValidationException("The server game is unsupported."));
    }

    private static GameSecretDefinition FindSecret(GameDefinition definition, string key)
        => definition.Secrets.SingleOrDefault(q => string.Equals(q.Key, key.Trim(), StringComparison.Ordinal))
           ?? throw new HostingValidationException("The secret key is not supported by this game.");

    private async Task<GameServerSecret?> CurrentAsync(Guid serverId, string key, CancellationToken cancellationToken)
        => await dbContext.GameServerSecrets.AsNoTracking()
            .SingleOrDefaultAsync(q => q.GameServerFk == serverId && q.Key == key && q.IsCurrent, cancellationToken)
            .ConfigureAwait(false);

    private void RetireCurrent(GameServer server, string key, Instant now)
    {
        var current = server.Secrets.SingleOrDefault(q => q.Key == key && q.IsCurrent);
        if (current is null)
            return;
        current.IsCurrent = false;
        current.TimeStampRetired = now;
    }

    private int AddVersion(GameServer server, string key, string value, EGameServerSecretOrigin origin, Guid? actorId, Instant now)
    {
        var version = server.Secrets.Where(q => q.Key == key).Select(q => q.Version).DefaultIfEmpty().Max() + 1;
        dbContext.GameServerSecrets.Add(new GameServerSecret
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = server.PrimaryKey,
            Key = key,
            Version = version,
            Origin = origin,
            IsCurrent = true,
            ProtectedValue = _protector.Protect(value),
            CreatedByFk = actorId,
            TimeStampCreated = now,
        });
        return version;
    }

    /// <summary>
    /// Verifies that a supplied value is usable for the game. The thrown messages are fixed and never contain the
    /// value.
    /// </summary>
    /// <param name="value">The trimmed value to validate.</param>
    /// <param name="format">The value format the game declares for the credential.</param>
    /// <exception cref="HostingValidationException">The value contains characters the game cannot use.</exception>
    private static void ValidateValue(string value, EGameSecretValueFormat format)
    {
        if (value.Any(char.IsControl))
            throw new HostingValidationException("The secret value must be a single line without control characters.");
        if (format is EGameSecretValueFormat.Token && value.Any(q => q is < '!' or > '~' or '"' or '\\'))
            throw new HostingValidationException(
                "This secret accepts only printable ASCII characters without spaces, quotes or backslashes.");
    }

    private static string Generate() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
}

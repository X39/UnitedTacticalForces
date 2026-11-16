using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.GameServerImplementations;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.GameServerConfiguration;

/// <summary>Resolves game configuration from defaults, profile revisions, and per-server overrides.</summary>
/// <param name="dbContext">The context containing the persisted configuration values.</param>
[Scoped<GameServerConfigurationService, IGameServerConfigurationService>]
public sealed class GameServerConfigurationService(DatabaseContext dbContext) : IGameServerConfigurationService
{
    /// <summary>Resolves settings and retains validation results for control-plane decisions.</summary>
    /// <param name="serverId">The server whose configuration to resolve.</param>
    /// <param name="cancellationToken">A token to cancel the database reads.</param>
    /// <returns>The effective entries and the profile revision that contributed to them.</returns>
    /// <exception cref="FileNotFoundException">The server is missing or archived.</exception>
    public async Task<ResolvedGameConfiguration> ResolveAsync(
        Guid serverId,
        CancellationToken cancellationToken = default)
    {
        var server = await dbContext.GameServers
            .AsNoTracking()
            .Include(q => q.ConfigurationValues)
            .SingleOrDefaultAsync(q => q.PrimaryKey == serverId && !q.IsArchived, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new FileNotFoundException("The game server does not exist.");
        var implementation = GameConfigurationImplementations.TryGet(server.GameIdentifier, out var configured)
            ? configured
            : new EmptyGameConfigurationImplementation(server.GameIdentifier);

        var revision = server.PinnedConfigurationProfileRevisionFk is { } pinnedRevisionId
            ? await dbContext.ConfigurationProfileRevisions
                .AsNoTracking()
                .Include(q => q.Values)
                .SingleOrDefaultAsync(
                    q => q.PrimaryKey == pinnedRevisionId
                         && q.ConfigurationProfileFk == server.SelectedConfigurationProfileFk,
                    cancellationToken)
                .ConfigureAwait(false)
            : server.SelectedConfigurationProfileFk is { } profileId
                ? await dbContext.ConfigurationProfileRevisions
                    .AsNoTracking()
                    .Include(q => q.Values)
                    .Where(q => q.ConfigurationProfileFk == profileId)
                    .OrderByDescending(q => q.RevisionNumber)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false)
                : null;

        var profileValues = revision?.Values.ToDictionary(q => q.DefinitionKey, StringComparer.Ordinal)
                            ?? new Dictionary<string, Database.Hosting.ConfigurationProfileValue>(StringComparer.Ordinal);
        var serverValues = server.ConfigurationValues.ToDictionary(q => q.DefinitionKey, StringComparer.Ordinal);
        var entries = new List<ResolvedConfigurationValue>(implementation.Definitions.Count);
        foreach (var definition in implementation.Definitions.OrderBy(q => q.Order))
        {
            if (definition.Sensitive)
                continue;
            JsonElement? value = definition.DefaultValue?.Clone();
            var source = value.HasValue ? "Default" : "Missing";
            var compatible = true;
            string? warning = null;

            if (profileValues.TryGetValue(definition.Key, out var profileValue))
            {
                var candidate = Parse(profileValue.ValueJson);
                if (GameConfigurationValueValidator.IsCompatible(definition, candidate, out var error))
                {
                    value = candidate;
                    source = "Profile";
                }
                else
                {
                    compatible = !definition.IsControlPlaneOnly;
                    warning = error;
                }
            }

            if (serverValues.TryGetValue(definition.Key, out var serverValue))
            {
                var candidate = Parse(serverValue.ValueJson);
                if (GameConfigurationValueValidator.IsCompatible(definition, candidate, out var error))
                {
                    value = candidate;
                    source = "Server";
                    compatible = true;
                    warning = null;
                }
                else
                {
                    compatible = false;
                    warning = error;
                }
            }

            entries.Add(new ResolvedConfigurationValue(definition, value, source, compatible, warning));
        }

        return new ResolvedGameConfiguration(
            server.PrimaryKey,
            implementation,
            server.SelectedConfigurationProfileFk,
            revision?.PrimaryKey,
            revision?.RevisionNumber,
            entries);
    }

    public IReadOnlyList<RenderedConfigurationFile> Render(ResolvedGameConfiguration configuration)
    {
        if (!configuration.IsComplete)
        {
            throw new InvalidOperationException(
                $"Required configuration is missing: {string.Join(", ", configuration.MissingRequired.Select(q => q.Definition.Key))}.");
        }
        return configuration.Implementation.Render(configuration.Values);
    }

    public JsonElement ValidateAndClone(string gameIdentifier, string key, JsonElement value)
    {
        var definition = GameConfigurationImplementations.Get(gameIdentifier).Definitions
            .SingleOrDefault(q => string.Equals(q.Key, key, StringComparison.Ordinal))
            ?? throw new ArgumentException($"Unknown configuration key '{key}'.", nameof(key));
        if (definition.Sensitive)
            throw new ArgumentException("Secrets must be managed through the server secret API.", nameof(key));
        if (!GameConfigurationValueValidator.IsCompatible(definition, value, out var error))
            throw new ArgumentException(error, nameof(value));
        return value.Clone();
    }

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}

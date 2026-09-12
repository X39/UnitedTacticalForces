using System.Text;
using System.Text.Json;

namespace X39.UnitedTacticalForces.GameServerImplementations;

/// <summary>
/// Defines and renders the public, non-secret ASKA dedicated-server settings.
/// Runtime-owned ports, credentials, and the mutable save identifier are added by the worker.
/// </summary>
public sealed class AskaConfigurationImplementation : IGameConfigurationImplementation
{
    /// <summary>Gets the stable ASKA game identifier.</summary>
    public const string Identifier = "aska";

    /// <summary>Gets the virtual configuration target used by configuration profiles.</summary>
    public const string ServerTarget = "server/aska-public-settings.txt";

    /// <inheritdoc />
    public string GameIdentifier => Identifier;

    /// <inheritdoc />
    public IReadOnlyList<GeneratedConfigurationTarget> Targets { get; } =
    [
        new(ServerTarget, "ASKA server settings", false, 10),
    ];

    /// <inheritdoc />
    public IReadOnlyList<GameConfigurationDefinition> Definitions { get; } = CreateDefinitions();

    /// <inheritdoc />
    public IReadOnlyList<RenderedConfigurationFile> Render(IReadOnlyDictionary<string, JsonElement> values)
    {
        var definitions = Definitions.ToDictionary(q => q.Key, StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            if (!definitions.TryGetValue(key, out var definition))
                throw new InvalidDataException($"Unknown ASKA configuration key '{key}'.");
            if (!GameConfigurationValueValidator.IsCompatible(definition, value, out var error))
                throw new InvalidDataException(error);
        }

        var lines = Definitions
            .Where(q => values.ContainsKey(q.Key))
            .OrderBy(q => q.Order)
            .Select(q => $"{q.Setting} = {Format(values[q.Key])}");
        return [new RenderedConfigurationFile(ServerTarget, Encoding.UTF8.GetBytes(string.Join('\n', lines) + "\n"), false)];
    }

    private static IReadOnlyList<GameConfigurationDefinition> CreateDefinitions()
        =>
        [
            Definition("display name", "World", "Display name", "Name shown for a newly created world.", EGameConfigurationValueKind.String, true, 10, Json("United Tactical Forces")),
            Definition("server name", "General", "Server name", "Host name shown in the public session list.", EGameConfigurationValueKind.String, true, 20, Json("United Tactical Forces ASKA")),
            Definition("seed", "World", "World seed", "Optional seed used when creating a new world.", EGameConfigurationValueKind.String, false, 30, Json(string.Empty)),
            Definition("region", "Network", "Region", "Backend region used for the hosted session.", EGameConfigurationValueKind.Selection, true, 40, Json("default"),
                ["default", "asia", "japan", "europe", "south america", "south korea", "usa east", "usa west", "australia", "canada east", "hong kong", "india", "turkey", "united arab emirates", "usa south central", "south africa"]),
            Definition("keep server world alive", "World", "Keep world alive", "Continue simulating the world while no players are connected.", EGameConfigurationValueKind.Boolean, true, 50, Json(false)),
            Definition("autosave style", "World", "Autosave style", "Controls the automatic world-save interval.", EGameConfigurationValueKind.Selection, true, 60, Json("every morning"),
                ["every morning", "disabled", "every 5 minutes", "every 10 minutes", "every 15 minutes", "every 20 minutes"]),
            Definition("mode", "World", "Game mode", "Uses standard rules or enables custom game settings.", EGameConfigurationValueKind.Selection, true, 70, Json("normal"), ["normal", "custom"]),
        ];

    private static GameConfigurationDefinition Definition(
        string setting,
        string group,
        string displayName,
        string description,
        EGameConfigurationValueKind kind,
        bool required,
        int order,
        JsonElement defaultValue,
        IReadOnlyList<string>? allowedValues = null)
        => new(
            $"{Path.GetFileName(ServerTarget).ToLowerInvariant()}@{setting}",
            ServerTarget,
            setting,
            group,
            displayName,
            description,
            kind,
            required,
            false,
            order,
            defaultValue,
            AllowedValues: allowedValues);

    private static string Format(JsonElement value)
    {
        var result = value.ValueKind switch
        {
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.String => value.GetString() ?? string.Empty,
            _ => throw new InvalidDataException("ASKA configuration values must be strings or booleans."),
        };
        return ValidatePropertyValue(result);
    }

    private static string ValidatePropertyValue(string value)
        => value.Length <= 256 && !value.Contains('\r') && !value.Contains('\n') && !value.Contains('=')
            ? value
            : throw new InvalidDataException("ASKA configuration values cannot contain line breaks or '=' and must not exceed 256 characters.");

    private static JsonElement Json(string value) => JsonSerializer.SerializeToElement(value);

    private static JsonElement Json(bool value) => JsonSerializer.SerializeToElement(value);
}

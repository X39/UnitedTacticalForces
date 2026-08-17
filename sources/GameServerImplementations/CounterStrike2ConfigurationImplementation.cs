using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace X39.UnitedTacticalForces.GameServerImplementations;

/// <summary>
/// Renders the allowlisted Counter-Strike 2 server configuration. Map, game mode, ports,
/// process paths, and credentials remain structural worker inputs.
/// </summary>
public sealed partial class CounterStrike2ConfigurationImplementation : IGameConfigurationImplementation
{
    public const string Identifier = "cs2";
    public const string ServerTarget = "game/csgo/cfg/utf-server.cfg";

    public string GameIdentifier => Identifier;

    public IReadOnlyList<GeneratedConfigurationTarget> Targets { get; } =
    [
        new(ServerTarget, "Server configuration", false, 10),
    ];

    public IReadOnlyList<GameConfigurationDefinition> Definitions { get; } = CreateDefinitions();

    public IReadOnlyList<RenderedConfigurationFile> Render(IReadOnlyDictionary<string, JsonElement> values)
    {
        var definitions = Definitions.ToDictionary(q => q.Key, StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            if (!definitions.TryGetValue(key, out var definition))
                throw new InvalidDataException($"Unknown Counter-Strike 2 configuration key '{key}'.");
            if (!GameConfigurationValueValidator.IsCompatible(definition, value, out var error))
                throw new InvalidDataException(error);
        }

        var lines = Definitions
            .Where(q => values.ContainsKey(q.Key))
            .OrderBy(q => q.Order)
            .Select(q => $"{ValidateSetting(q.Setting)} {Format(values[q.Key])}");
        var content = string.Join('\n', lines) + "\n";
        return [new RenderedConfigurationFile(ServerTarget, Encoding.UTF8.GetBytes(content), false)];
    }

    private static IReadOnlyList<GameConfigurationDefinition> CreateDefinitions()
        =>
        [
            Definition("hostname", "General", "Hostname", "Name shown in the server browser.", EGameConfigurationValueKind.String, true, 10, Json("United Tactical Forces")),
            Definition("maxplayers", "General", "Maximum players", "Maximum concurrent player count.", EGameConfigurationValueKind.Integer, true, 20, Json(12), 1, 64),
            Definition("sv_lan", "Network", "LAN only", "Restrict the server to the local network.", EGameConfigurationValueKind.Boolean, true, 30, Json(false)),
            Definition("sv_cheats", "Security", "Enable cheats", "Allow cheat-protected console commands.", EGameConfigurationValueKind.Boolean, true, 40, Json(false)),
            Definition("sv_pure", "Security", "Pure server policy", "Controls client content verification.", EGameConfigurationValueKind.Selection, true, 50, Json("1"), AllowedValues: ["0", "1", "2"]),
            Definition("mp_autoteambalance", "Teams", "Automatic team balance", "Balance teams automatically.", EGameConfigurationValueKind.Boolean, false, 60, Json(true)),
            Definition("mp_limitteams", "Teams", "Team imbalance limit", "Maximum permitted team-size difference.", EGameConfigurationValueKind.Integer, false, 70, Json(1), 0, 32),
            Definition("mp_friendlyfire", "Rules", "Friendly fire", "Allow damage to team members.", EGameConfigurationValueKind.Boolean, false, 80, Json(true)),
            Definition("mp_maxrounds", "Rules", "Maximum rounds", "Maximum rounds before the map ends.", EGameConfigurationValueKind.Integer, false, 90, Json(24), 1, 100),
            Definition("mp_roundtime", "Rules", "Round duration", "Round duration in minutes.", EGameConfigurationValueKind.Number, false, 100, Json(1.92m), 0.5m, 60m),
            Definition("bot_quota", "Bots", "Bot quota", "Number of managed bots.", EGameConfigurationValueKind.Integer, false, 110, Json(0), 0, 64),
            Definition("bot_quota_mode", "Bots", "Bot quota mode", "How the bot quota is maintained.", EGameConfigurationValueKind.Selection, false, 120, Json("fill"), AllowedValues: ["normal", "fill", "match"]),
        ];

    private static GameConfigurationDefinition Definition(
        string setting,
        string group,
        string displayName,
        string description,
        EGameConfigurationValueKind kind,
        bool required,
        int order,
        JsonElement? defaultValue = null,
        decimal? minimum = null,
        decimal? maximum = null,
        IReadOnlyList<string>? AllowedValues = null)
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
            minimum,
            maximum,
            AllowedValues);

    private static string Format(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.True => "1",
            JsonValueKind.False => "0",
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.String => Quote(value.GetString() ?? string.Empty),
            _ => throw new InvalidDataException("Counter-Strike 2 configuration value cannot be rendered."),
        };

    private static string Quote(string value)
        => $"\"{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal).Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal)}\"";

    private static string ValidateSetting(string setting)
        => SettingPattern().IsMatch(setting)
            ? setting
            : throw new InvalidDataException("Counter-Strike 2 configuration contains an invalid setting name.");

    private static JsonElement Json(string value) => JsonSerializer.SerializeToElement(value);
    private static JsonElement Json(int value) => JsonSerializer.SerializeToElement(value);
    private static JsonElement Json(decimal value) => JsonSerializer.SerializeToElement(value);
    private static JsonElement Json(bool value) => JsonSerializer.SerializeToElement(value);

    [GeneratedRegex("^[a-z][a-z0-9_]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex SettingPattern();
}

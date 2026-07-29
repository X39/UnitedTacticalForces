using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace X39.UnitedTacticalForces.GameServerImplementations;

public sealed partial class Arma3ConfigurationImplementation : IGameConfigurationImplementation
{
    public const string Identifier = "arma3";
    public const string ServerTarget = "server/server.cfg";
    public const string BasicTarget = "server/basic.cfg";
    public const string ProfileTarget = "server/profiles/UTFServer/UTFServer.Arma3Profile";
    public const string BattlEyeTarget = "server/battleye/beserver.cfg";

    public string GameIdentifier => Identifier;

    public IReadOnlyList<GeneratedConfigurationTarget> Targets { get; } =
    [
        new(ServerTarget, "Server configuration", true, 10),
        new(BasicTarget, "Network configuration", false, 20),
        new(ProfileTarget, "Server profile", false, 30),
        new(BattlEyeTarget, "BattlEye configuration", true, 40),
    ];

    public IReadOnlyList<GameConfigurationDefinition> Definitions { get; } = CreateDefinitions();

    public IReadOnlyList<RenderedConfigurationFile> Render(IReadOnlyDictionary<string, JsonElement> values)
    {
        var definitions = Definitions.ToDictionary(q => q.Key, StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            if (!definitions.TryGetValue(key, out var definition))
                throw new InvalidDataException($"Unknown Arma 3 configuration key '{key}'.");
            if (!GameConfigurationValueValidator.IsCompatible(definition, value, out var error))
                throw new InvalidDataException(error);
        }

        return
        [
            RenderArmaTarget(ServerTarget, true, values),
            RenderArmaTarget(BasicTarget, false, values),
            RenderArmaTarget(ProfileTarget, false, values),
            RenderBattlEye(values),
        ];
    }

    private RenderedConfigurationFile RenderArmaTarget(
        string target,
        bool sensitive,
        IReadOnlyDictionary<string, JsonElement> values)
    {
        var lines = Definitions
            .Where(q => q.TargetPath == target && values.ContainsKey(q.Key))
            .OrderBy(q => q.Order)
            .Select(q => $"{ValidateSetting(q.Setting)} = {FormatArma(values[q.Key])};");
        return new RenderedConfigurationFile(target, Encoding.UTF8.GetBytes(JoinLines(lines)), sensitive);
    }

    private RenderedConfigurationFile RenderBattlEye(IReadOnlyDictionary<string, JsonElement> values)
    {
        var lines = Definitions
            .Where(q => q.TargetPath == BattlEyeTarget && values.ContainsKey(q.Key))
            .OrderBy(q => q.Order)
            .Select(q => $"{ValidateSetting(q.Setting)} {FormatBattlEye(values[q.Key])}");
        return new RenderedConfigurationFile(BattlEyeTarget, Encoding.UTF8.GetBytes(JoinLines(lines)), true);
    }

    private static string JoinLines(IEnumerable<string> lines)
        => string.Join(Environment.NewLine, lines) + Environment.NewLine;

    private static string ValidateSetting(string setting)
        => SettingPattern().IsMatch(setting)
            ? setting
            : throw new InvalidDataException("Arma configuration contains an invalid setting name.");

    private static string FormatArma(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.True => "1",
            JsonValueKind.False => "0",
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.String => Quote(value.GetString() ?? string.Empty),
            JsonValueKind.Array => $"{{{string.Join(',', value.EnumerateArray().Select(FormatArma))}}}",
            _ => throw new InvalidDataException("Arma configuration value cannot be rendered."),
        };

    private static string FormatBattlEye(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.True => "1",
            JsonValueKind.False => "0",
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.String => SingleLine(value.GetString() ?? string.Empty),
            _ => throw new InvalidDataException("BattlEye configuration value cannot be rendered."),
        };

    private static string Quote(string value)
        => $"\"{SingleLine(value).Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static string SingleLine(string value)
        => value.Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);

    private static JsonElement Json(string value) => JsonSerializer.SerializeToElement(value);
    private static JsonElement Json(int value) => JsonSerializer.SerializeToElement(value);
    private static JsonElement Json(decimal value) => JsonSerializer.SerializeToElement(value);
    private static JsonElement Json(bool value) => JsonSerializer.SerializeToElement(value);

    private static IReadOnlyList<GameConfigurationDefinition> CreateDefinitions()
        =>
        [
            Definition(ServerTarget, "hostname", "General", "Hostname", "Name shown in the server browser.", EGameConfigurationValueKind.String, true, false, 10, Json("United Tactical Forces")),
            Definition(ServerTarget, "password", "General", "Join password", "Password required to join the server.", EGameConfigurationValueKind.String, false, true, 20),
            Definition(ServerTarget, "passwordAdmin", "General", "Administrator password", "Password used for in-game administration.", EGameConfigurationValueKind.String, false, true, 30),
            Definition(ServerTarget, "serverCommandPassword", "General", "Server command password", "Password used by server-command integrations.", EGameConfigurationValueKind.String, false, true, 40),
            Definition(ServerTarget, "maxPlayers", "General", "Maximum players", "Maximum concurrent player count.", EGameConfigurationValueKind.Integer, true, false, 50, Json(64), 1, 256),
            Definition(ServerTarget, "motd[]", "General", "Message of the day", "Messages displayed to connecting players.", EGameConfigurationValueKind.StringArray, false, false, 60, JsonSerializer.SerializeToElement(Array.Empty<string>())),
            Definition(ServerTarget, "motdInterval", "General", "Message interval", "Seconds between message-of-the-day lines.", EGameConfigurationValueKind.Integer, false, false, 70, Json(5), 1, 3600),
            Definition(ServerTarget, "voteThreshold", "Voting", "Vote threshold", "Fraction of players required for a vote.", EGameConfigurationValueKind.Number, false, false, 80, Json(0.33m), 0, 1),
            Definition(ServerTarget, "voteMissionPlayers", "Voting", "Vote mission players", "Minimum players required for mission voting.", EGameConfigurationValueKind.Integer, false, false, 90, Json(1), 0, 256),
            Definition(ServerTarget, "kickDuplicate", "Security", "Kick duplicate clients", "Reject duplicate client identities.", EGameConfigurationValueKind.Boolean, false, false, 100, Json(true)),
            Definition(ServerTarget, "verifySignatures", "Security", "Verify signatures", "Required addon signature verification level.", EGameConfigurationValueKind.Integer, false, false, 110, Json(2), 0, 2),
            Definition(ServerTarget, "allowedFilePatching", "Security", "Allowed file patching", "Controls file-patching access.", EGameConfigurationValueKind.Selection, false, false, 120, Json("0"), AllowedValues: ["0", "1", "2"]),
            Definition(ServerTarget, "BattlEye", "Security", "Enable BattlEye", "Enable BattlEye anti-cheat.", EGameConfigurationValueKind.Boolean, false, false, 130, Json(true)),
            Definition(ServerTarget, "persistent", "Lifecycle", "Persistent battlefield", "Retain mission state while the server is empty.", EGameConfigurationValueKind.Boolean, false, false, 140, Json(true)),
            Definition(ServerTarget, "disableVoN", "Voice", "Disable voice", "Disable built-in voice communication.", EGameConfigurationValueKind.Boolean, false, false, 150, Json(false)),
            Definition(ServerTarget, "vonCodecQuality", "Voice", "Voice quality", "Voice codec quality from 1 to 30.", EGameConfigurationValueKind.Integer, false, false, 160, Json(10), 1, 30),
            Definition(BasicTarget, "MaxMsgSend", "Network", "Maximum messages", "Maximum messages sent per simulation cycle.", EGameConfigurationValueKind.Integer, false, false, 10, Json(128), 1),
            Definition(BasicTarget, "MaxSizeGuaranteed", "Network", "Guaranteed packet size", "Maximum guaranteed packet size.", EGameConfigurationValueKind.Integer, false, false, 20, Json(512), 1),
            Definition(BasicTarget, "MaxSizeNonguaranteed", "Network", "Non-guaranteed packet size", "Maximum non-guaranteed packet size.", EGameConfigurationValueKind.Integer, false, false, 30, Json(256), 1),
            Definition(BasicTarget, "MinBandwidth", "Network", "Minimum bandwidth", "Reserved minimum bandwidth in bits per second.", EGameConfigurationValueKind.Integer, false, false, 40, Json(131072), 0),
            Definition(BasicTarget, "MaxBandwidth", "Network", "Maximum bandwidth", "Maximum bandwidth in bits per second.", EGameConfigurationValueKind.Integer, false, false, 50, Json(100000000), 0),
            Definition(BasicTarget, "MinErrorToSend", "Network", "Minimum error", "Minimum simulation error before sending an update.", EGameConfigurationValueKind.Number, false, false, 60, Json(0.001m), 0, 1),
            Definition(BasicTarget, "MinErrorToSendNear", "Network", "Minimum near error", "Minimum near-object error before sending an update.", EGameConfigurationValueKind.Number, false, false, 70, Json(0.01m), 0, 1),
            Definition(BasicTarget, "MaxCustomFileSize", "Network", "Maximum custom file size", "Maximum custom-file size in bytes.", EGameConfigurationValueKind.Integer, false, false, 80, Json(0), 0),
            Definition(ProfileTarget, "difficulty", "Difficulty", "Difficulty preset", "Difficulty preset selected for the server profile.", EGameConfigurationValueKind.Selection, false, false, 10, Json("Custom"), AllowedValues: ["Recruit", "Regular", "Veteran", "Custom"]),
            Definition(BattlEyeTarget, "RConIP", "RCon", "RCon address", "Address on which BattlEye RCon listens.", EGameConfigurationValueKind.String, false, false, 10, Json("0.0.0.0")),
            Definition(BattlEyeTarget, "RConPort", "RCon", "RCon port", "Port on which BattlEye RCon listens.", EGameConfigurationValueKind.Integer, false, false, 20, Json(2307), 1, 65535),
            Definition(BattlEyeTarget, "RConPassword", "RCon", "RCon password", "Password required for BattlEye RCon.", EGameConfigurationValueKind.String, false, true, 30),
            Definition(BattlEyeTarget, "RestrictRCon", "RCon", "Restrict RCon", "Restrict RCon clients according to BattlEye policy.", EGameConfigurationValueKind.Boolean, false, false, 40, Json(false)),
        ];

    private static GameConfigurationDefinition Definition(
        string target,
        string setting,
        string group,
        string displayName,
        string description,
        EGameConfigurationValueKind kind,
        bool required,
        bool sensitive,
        int order,
        JsonElement? defaultValue = null,
        decimal? minimum = null,
        decimal? maximum = null,
        IReadOnlyList<string>? AllowedValues = null)
        => new(
            $"{Path.GetFileName(target).ToLowerInvariant()}@{setting}",
            target,
            setting,
            group,
            displayName,
            description,
            kind,
            required,
            sensitive,
            order,
            defaultValue,
            minimum,
            maximum,
            AllowedValues);

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_.\\[\\]-]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex SettingPattern();
}

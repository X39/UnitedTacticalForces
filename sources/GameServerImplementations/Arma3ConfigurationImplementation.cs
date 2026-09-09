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
            RenderProfile(values),
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

    private RenderedConfigurationFile RenderProfile(IReadOnlyDictionary<string, JsonElement> values)
    {
        var profileValues = Definitions
            .Where(q => q.TargetPath == ProfileTarget && values.ContainsKey(q.Key))
            .ToDictionary(q => q.Setting, q => values[q.Key], StringComparer.Ordinal);
        if (profileValues.Count == 0)
            return new RenderedConfigurationFile(ProfileTarget, Encoding.UTF8.GetBytes(string.Empty), false);

        var optionNames = ProfileOptionNames;
        var lines = new List<string>
        {
            "class DifficultyPresets",
            "{",
            "    class CustomDifficulty",
            "    {",
            "        class Options",
            "        {",
        };
        foreach (var name in optionNames.Where(profileValues.ContainsKey))
            lines.Add($"            {ValidateSetting(name)} = {FormatArma(profileValues[name])};");
        lines.Add("        };");
        if (profileValues.TryGetValue("aiLevelPreset", out var aiLevelPreset))
            lines.Add($"        aiLevelPreset = {FormatArma(aiLevelPreset)};");
        lines.Add("    };");
        var hasSkillAi = profileValues.TryGetValue("skillAI", out var skillAi);
        var hasPrecisionAi = profileValues.TryGetValue("precisionAI", out var precisionAi);
        if (hasSkillAi || hasPrecisionAi)
        {
            lines.Add("    class CustomAILevel");
            lines.Add("    {");
            if (hasSkillAi)
                lines.Add($"        skillAI = {FormatArma(skillAi)};");
            if (hasPrecisionAi)
                lines.Add($"        precisionAI = {FormatArma(precisionAi)};");
            lines.Add("    };");
        }
        lines.Add("};");
        return new RenderedConfigurationFile(
            ProfileTarget,
            Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, lines) + Environment.NewLine),
            false);
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
            Definition(ServerTarget, "admins[]", "Administration", "Passwordless administrators", "Steam IDs allowed to log in as administrator without the administrator password.", EGameConfigurationValueKind.StringArray, false, false, 60),
            Definition(ServerTarget, "motd[]", "Message of the day", "Message lines", "Messages displayed to connecting players.", EGameConfigurationValueKind.StringArray, false, false, 70, JsonSerializer.SerializeToElement(Array.Empty<string>())),
            Definition(ServerTarget, "motdInterval", "Message of the day", "Message interval", "Seconds between message-of-the-day lines.", EGameConfigurationValueKind.Number, false, false, 80, Json(5), 0, 3600),
            Definition(ServerTarget, "voteThreshold", "Voting", "Vote threshold", "Fraction of players required for a vote.", EGameConfigurationValueKind.Number, false, false, 80, Json(0.33m), 0, 1),
            Definition(ServerTarget, "voteMissionPlayers", "Voting", "Vote mission players", "Minimum players required for mission voting.", EGameConfigurationValueKind.Integer, false, false, 90, Json(1), 0, 256),
            Definition(ServerTarget, "kickDuplicate", "Security", "Kick duplicate clients", "Reject duplicate client identities.", EGameConfigurationValueKind.Boolean, false, false, 100, Json(true)),
            Definition(ServerTarget, "verifySignatures", "Security", "Verify signatures", "Addon signature verification policy.", EGameConfigurationValueKind.Integer, false, false, 110, Json(2), 0, 2, Choices: [Choice(0, "Disabled"), Choice(1, "Accept v1; prefer v2"), Choice(2, "Require v2 only")]),
            Definition(ServerTarget, "allowedFilePatching", "Security", "File patching", "Controls which clients may connect while using file patching.", EGameConfigurationValueKind.Integer, false, false, 120, Json(0), 0, 2, Choices: [Choice(0, "Disabled"), Choice(1, "Headless clients only"), Choice(2, "All clients")]),
            Definition(ServerTarget, "filePatchingExceptions[]", "Security", "File-patching exceptions", "Steam IDs exempt from file-patching and signature restrictions.", EGameConfigurationValueKind.StringArray, false, false, 130),
            Definition(ServerTarget, "allowedLoadFileExtensions[]", "Security", "Allowed loadFile extensions", "Extensions available to the loadFile scripting command.", EGameConfigurationValueKind.StringArray, false, false, 140),
            Definition(ServerTarget, "allowedPreprocessFileExtensions[]", "Security", "Allowed preprocess extensions", "Extensions available to preprocessing scripting commands.", EGameConfigurationValueKind.StringArray, false, false, 150),
            Definition(ServerTarget, "allowedHTMLLoadExtensions[]", "Security", "Allowed HTML extensions", "Extensions available to htmlLoad.", EGameConfigurationValueKind.StringArray, false, false, 160),
            Definition(ServerTarget, "allowedHTMLLoadURIs[]", "Security", "Allowed HTML origins", "URI prefixes available to htmlLoad.", EGameConfigurationValueKind.StringArray, false, false, 170),
            Definition(ServerTarget, "requiredBuild", "Security", "Minimum client build", "Reject clients older than this Arma build number.", EGameConfigurationValueKind.Integer, false, false, 180, minimum: 0),
            Definition(ServerTarget, "BattlEye", "Security", "Enable BattlEye", "Enable BattlEye anti-cheat.", EGameConfigurationValueKind.Boolean, false, false, 190, Json(true)),
            Definition(ServerTarget, "disableVoN", "Voice", "Disable voice", "Disable built-in voice communication.", EGameConfigurationValueKind.Boolean, false, false, 200, Json(false)),
            Definition(ServerTarget, "vonCodec", "Voice", "Voice codec", "Codec used for voice communication.", EGameConfigurationValueKind.Integer, false, false, 210, Json(1), 0, 1, Choices: [Choice(0, "SPEEX"), Choice(1, "OPUS")]),
            Definition(ServerTarget, "vonCodecQuality", "Voice", "Voice quality", "Voice codec quality from 1 to 30.", EGameConfigurationValueKind.Integer, false, false, 220, Json(10), 1, 30),
            Definition(ServerTarget, "timeStampFormat", "Reporting", "Log timestamp format", "Timestamp format used in the server report file.", EGameConfigurationValueKind.Selection, false, false, 230, Json("short"), AllowedValues: ["none", "short", "full"]),
            Definition(ServerTarget, "timeStampFormatConsole", "Reporting", "Console timestamp format", "Timestamp format used in server-console output.", EGameConfigurationValueKind.Selection, false, false, 240, Json("short"), AllowedValues: ["none", "short", "full"]),
            Definition(ServerTarget, "statisticsEnabled", "Reporting", "Share analytics", "Allow Arma 3 server analytics.", EGameConfigurationValueKind.Boolean, false, false, 250, Json(true)),
            Definition(ServerTarget, "enablePlayerDiag", "Reporting", "Player network diagnostics", "Periodically log player bandwidth and desynchronization information.", EGameConfigurationValueKind.Boolean, false, false, 260, Json(false)),
            Definition(ServerTarget, "callExtReportLimit", "Reporting", "Extension warning threshold", "Milliseconds before a slow callExtension invocation is logged.", EGameConfigurationValueKind.Integer, false, false, 270, Json(1000), 0),
            Definition(ServerTarget, "MaxPing", "Client quality", "Maximum ping", "Maximum client ping before policy enforcement.", EGameConfigurationValueKind.Integer, false, false, 280, minimum: 0),
            Definition(ServerTarget, "MaxPacketLoss", "Client quality", "Maximum packet loss", "Maximum client packet loss before policy enforcement.", EGameConfigurationValueKind.Integer, false, false, 290, minimum: 0),
            Definition(ServerTarget, "MaxDesync", "Client quality", "Maximum desynchronization", "Maximum client desynchronization before policy enforcement.", EGameConfigurationValueKind.Integer, false, false, 300, minimum: 0),
            Definition(ServerTarget, "DisconnectTimeout", "Client quality", "Disconnect timeout", "Seconds to wait after active client traffic is lost.", EGameConfigurationValueKind.Integer, false, false, 310, Json(15), 1, 90),
            Definition(ServerTarget, "steamProtocolMaxDataSize", "Networking", "Steam query packet limit", "Maximum Steam query response size; values above the MTU may fragment.", EGameConfigurationValueKind.Integer, false, false, 320, Json(1024), 1),
            Definition(ServerTarget, "persistent", "Lifecycle", "Persistent battlefield", "Retain mission state while the server is empty.", EGameConfigurationValueKind.Boolean, false, false, 330, Json(true)),
            Definition(ServerTarget, "forcedDifficulty", "Lifecycle", "Forced difficulty", "Difficulty preset enforced unless a mission overrides it.", EGameConfigurationValueKind.Selection, false, false, 340, Json("Custom"), AllowedValues: ["Recruit", "Regular", "Veteran", "Custom"]),
            Definition(ServerTarget, "autoSelectMission", "Lifecycle", "Automatically select missions", "Automatically select the next mission in the rotation.", EGameConfigurationValueKind.Boolean, false, false, 350, Json(false)),
            Definition(ServerTarget, "randomMissionOrder", "Lifecycle", "Random mission order", "Choose the next mission randomly from the rotation.", EGameConfigurationValueKind.Boolean, false, false, 360, Json(false)),
            Definition(ServerTarget, "missionsToServerRestart", "Lifecycle", "Missions before restart", "Restart the server process after this many completed missions; zero disables it.", EGameConfigurationValueKind.Integer, false, false, 370, Json(0), 0),
            Definition(ServerTarget, "missionsToShutdown", "Lifecycle", "Missions before shutdown", "Shut down after this many completed missions; zero disables it.", EGameConfigurationValueKind.Integer, false, false, 380, Json(0), 0),
            Definition(ServerTarget, "lobbyIdleTimeout", "Lifecycle", "Lobby idle timeout", "Seconds before an unattended lobby is force-started.", EGameConfigurationValueKind.Integer, false, false, 390, Json(300), 0),
            Definition(ServerTarget, "missionWhitelist[]", "Lifecycle", "Mission whitelist", "Mission identifiers available to administrators; empty allows every mission.", EGameConfigurationValueKind.StringArray, false, false, 400),
            Definition(ServerTarget, "forceRotorLibSimulation", "Simulation", "Flight model policy", "Controls whether the advanced or standard helicopter flight model is enforced.", EGameConfigurationValueKind.Integer, false, false, 410, Json(0), 0, 2, Choices: [Choice(0, "Player choice"), Choice(1, "Advanced flight model"), Choice(2, "Standard flight model")]),
            Definition(BasicTarget, "MaxMsgSend", "Network", "Maximum messages", "Maximum messages sent per simulation cycle.", EGameConfigurationValueKind.Integer, false, false, 10, Json(128), 1),
            Definition(BasicTarget, "MaxSizeGuaranteed", "Network", "Guaranteed packet size", "Maximum guaranteed packet size.", EGameConfigurationValueKind.Integer, false, false, 20, Json(512), 1),
            Definition(BasicTarget, "MaxSizeNonguaranteed", "Network", "Non-guaranteed packet size", "Maximum non-guaranteed packet size.", EGameConfigurationValueKind.Integer, false, false, 30, Json(256), 1),
            Definition(BasicTarget, "MinBandwidth", "Network", "Minimum bandwidth", "Reserved minimum bandwidth in bits per second.", EGameConfigurationValueKind.Integer, false, false, 40, Json(131072), 0),
            Definition(BasicTarget, "MaxBandwidth", "Network", "Maximum bandwidth", "Maximum bandwidth in bits per second.", EGameConfigurationValueKind.Integer, false, false, 50, Json(100000000), 0),
            Definition(BasicTarget, "MinErrorToSend", "Network", "Minimum error", "Minimum simulation error before sending an update.", EGameConfigurationValueKind.Number, false, false, 60, Json(0.001m), 0, 1),
            Definition(BasicTarget, "MinErrorToSendNear", "Network", "Minimum near error", "Minimum near-object error before sending an update.", EGameConfigurationValueKind.Number, false, false, 70, Json(0.01m), 0, 1),
            Definition(BasicTarget, "MaxCustomFileSize", "Network", "Maximum custom file size", "Maximum custom-file size in bytes.", EGameConfigurationValueKind.Integer, false, false, 80, Json(0), 0),
            Definition(ProfileTarget, "reducedDamage", "Simulation", "Reduced damage", "Reduce damage to players and their group.", EGameConfigurationValueKind.Boolean, false, false, 10, Json(false)),
            Definition(ProfileTarget, "groupIndicators", "Situational awareness", "Group indicators", "Visibility of group-unit indicators.", EGameConfigurationValueKind.Integer, false, false, 20, Json(0), 0, 2, Choices: VisibilityChoices()),
            Definition(ProfileTarget, "friendlyTags", "Situational awareness", "Friendly name tags", "Visibility of friendly-unit name tags.", EGameConfigurationValueKind.Integer, false, false, 30, Json(0), 0, 2, Choices: VisibilityChoices()),
            Definition(ProfileTarget, "enemyTags", "Situational awareness", "Enemy name tags", "Visibility of enemy-unit name tags.", EGameConfigurationValueKind.Integer, false, false, 40, Json(0), 0, 2, Choices: VisibilityChoices()),
            Definition(ProfileTarget, "detectedMines", "Situational awareness", "Detected mines", "Visibility of detected mine markers.", EGameConfigurationValueKind.Integer, false, false, 50, Json(0), 0, 2, Choices: VisibilityChoices()),
            Definition(ProfileTarget, "commands", "Situational awareness", "Command indicators", "Visibility of command indicators.", EGameConfigurationValueKind.Integer, false, false, 60, Json(1), 0, 2, Choices: VisibilityChoices()),
            Definition(ProfileTarget, "waypoints", "Situational awareness", "Waypoints", "Visibility of waypoint markers.", EGameConfigurationValueKind.Integer, false, false, 70, Json(1), 0, 2, Choices: VisibilityChoices()),
            Definition(ProfileTarget, "tacticalPing", "Situational awareness", "Tactical ping", "Where tactical-ping markers are displayed.", EGameConfigurationValueKind.Integer, false, false, 80, Json(0), 0, 3, Choices: [Choice(0, "Disabled"), Choice(1, "3D scene"), Choice(2, "Map"), Choice(3, "3D scene and map")]),
            Definition(ProfileTarget, "weaponInfo", "Personal awareness", "Weapon information", "Visibility of the weapon information panel.", EGameConfigurationValueKind.Integer, false, false, 90, Json(1), 0, 2, Choices: VisibilityChoices()),
            Definition(ProfileTarget, "stanceIndicator", "Personal awareness", "Stance indicator", "Visibility of the stance indicator.", EGameConfigurationValueKind.Integer, false, false, 100, Json(0), 0, 2, Choices: VisibilityChoices()),
            Definition(ProfileTarget, "staminaBar", "Personal awareness", "Stamina bar", "Show the stamina indicator when stamina changes.", EGameConfigurationValueKind.Boolean, false, false, 110, Json(false)),
            Definition(ProfileTarget, "weaponCrosshair", "Personal awareness", "Weapon crosshair", "Show the weapon crosshair.", EGameConfigurationValueKind.Boolean, false, false, 120, Json(false)),
            Definition(ProfileTarget, "visionAid", "Personal awareness", "Vision aid", "Help identify nearby units.", EGameConfigurationValueKind.Boolean, false, false, 130, Json(false)),
            Definition(ProfileTarget, "thirdPersonView", "View", "Third-person view", "Availability of the third-person camera.", EGameConfigurationValueKind.Integer, false, false, 140, Json(0), 0, 2, Choices: [Choice(0, "Disabled"), Choice(1, "Enabled"), Choice(2, "Vehicles only")]),
            Definition(ProfileTarget, "cameraShake", "View", "Camera shake", "Apply camera shake near explosions and under vehicle load.", EGameConfigurationValueKind.Boolean, false, false, 150, Json(true)),
            Definition(ProfileTarget, "scoreTable", "Multiplayer", "Score table", "Show the multiplayer score table.", EGameConfigurationValueKind.Boolean, false, false, 160, Json(true)),
            Definition(ProfileTarget, "deathMessages", "Multiplayer", "Death messages", "Show who killed a player.", EGameConfigurationValueKind.Boolean, false, false, 170, Json(true)),
            Definition(ProfileTarget, "vonID", "Multiplayer", "Voice speaker identity", "Identify the player speaking through voice chat.", EGameConfigurationValueKind.Boolean, false, false, 180, Json(true)),
            Definition(ProfileTarget, "mapContentFriendly", "Map", "Friendly map markers", "Show friendly units on the map.", EGameConfigurationValueKind.Boolean, false, false, 190, Json(false)),
            Definition(ProfileTarget, "mapContentEnemy", "Map", "Enemy map markers", "Show enemy units on the map.", EGameConfigurationValueKind.Boolean, false, false, 200, Json(false)),
            Definition(ProfileTarget, "mapContentMines", "Map", "Mine map markers", "Show detected mines on the map.", EGameConfigurationValueKind.Boolean, false, false, 210, Json(false)),
            Definition(ProfileTarget, "autoReport", "Miscellaneous", "Automatic reporting", "Automatically report enemies spotted by players.", EGameConfigurationValueKind.Boolean, false, false, 220, Json(false)),
            Definition(ProfileTarget, "multipleSaves", "Miscellaneous", "Multiple saves", "Allow multiple mission saves.", EGameConfigurationValueKind.Boolean, false, false, 230, Json(false)),
            Definition(ProfileTarget, "aiLevelPreset", "AI", "AI level preset", "AI skill preset; custom uses the skill and precision values below.", EGameConfigurationValueKind.Integer, false, false, 240, Json(3), 0, 3, Choices: [Choice(0, "Low"), Choice(1, "Normal"), Choice(2, "High"), Choice(3, "Custom")]),
            Definition(ProfileTarget, "skillAI", "AI", "Custom AI skill", "General AI skill used by the custom AI preset.", EGameConfigurationValueKind.Number, false, false, 250, Json(0.5m), 0, 1),
            Definition(ProfileTarget, "precisionAI", "AI", "Custom AI precision", "AI aiming precision used by the custom AI preset.", EGameConfigurationValueKind.Number, false, false, 260, Json(0.5m), 0, 1),
            Definition(BattlEyeTarget, "RConIP", "RCon", "RCon address", "Address on which BattlEye RCon listens.", EGameConfigurationValueKind.String, false, false, 10, Json("0.0.0.0")),
            Definition(BattlEyeTarget, "RConPort", "RCon", "RCon port", "Port on which BattlEye RCon listens.", EGameConfigurationValueKind.Integer, false, false, 20, Json(2307), 1, 65535),
            Definition(BattlEyeTarget, "RConPassword", "RCon", "RCon password", "Password required for BattlEye RCon.", EGameConfigurationValueKind.String, false, true, 30),
            Definition(BattlEyeTarget, "RestrictRCon", "RCon", "Restrict RCon", "Restrict RCon clients according to BattlEye policy.", EGameConfigurationValueKind.Boolean, false, false, 40, Json(false)),
            Definition(BattlEyeTarget, "MaxPing", "RCon", "Maximum BattlEye ping", "Kick players whose ping exceeds this value; omit to disable.", EGameConfigurationValueKind.Integer, false, false, 50, minimum: 0),
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
        IReadOnlyList<string>? AllowedValues = null,
        IReadOnlyList<GameConfigurationChoice>? Choices = null)
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
            AllowedValues,
            Choices);

    private static GameConfigurationChoice Choice(int value, string label, string? description = null)
        => new(Json(value), label, description);

    private static IReadOnlyList<GameConfigurationChoice> VisibilityChoices()
        => [Choice(0, "Never"), Choice(1, "Limited or fade out"), Choice(2, "Always")];

    private static readonly string[] ProfileOptionNames =
    [
        "reducedDamage",
        "groupIndicators",
        "friendlyTags",
        "enemyTags",
        "detectedMines",
        "commands",
        "waypoints",
        "tacticalPing",
        "weaponInfo",
        "stanceIndicator",
        "staminaBar",
        "weaponCrosshair",
        "visionAid",
        "thirdPersonView",
        "cameraShake",
        "scoreTable",
        "deathMessages",
        "vonID",
        "mapContentFriendly",
        "mapContentEnemy",
        "mapContentMines",
        "autoReport",
        "multipleSaves",
    ];

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_.\\[\\]-]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex SettingPattern();
}

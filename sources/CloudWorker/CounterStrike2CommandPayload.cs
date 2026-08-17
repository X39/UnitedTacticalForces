using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.RegularExpressions;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.CloudWorker;

public sealed partial record CounterStrike2CommandPayload
{
    public WorkerCommandPayload Command { get; init; } = new();

    public static CounterStrike2CommandPayload Parse(string payload)
    {
        try
        {
            return new CounterStrike2CommandPayload
            {
                Command = JsonSerializer.Deserialize<WorkerCommandPayload>(payload, CloudWorkerJson.Options)
                          ?? throw new InvalidDataException("CS2 command payload is empty."),
            };
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("CS2 command payload is invalid JSON.", exception);
        }
    }

    public void Validate(CloudWorkerConnectionConfig connection, WorkerCommandClaimResponse envelope)
    {
        if (Command.ServerId != connection.ServerId || Command.DeploymentId != connection.DeploymentId)
            throw new InvalidDataException("CS2 command attribution does not match this worker.");
        if (!string.Equals(Command.GameIdentifier, "cs2", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Command.GameIdentifier, connection.GameIdentifier, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("CS2 command game attribution is invalid.");
        if (!string.Equals(Command.ActionIdentifier, envelope.ActionIdentifier, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("CS2 action attribution is invalid.");
        if (Command.GracefulStopAttempts is < 1 or > 16 || Command.GracefulStopTimeoutSeconds is < 1 or > 900)
            throw new InvalidDataException("CS2 graceful-stop policy is outside worker safety limits.");
        if (Command.Port is not (null or >= 1024 and <= 65535))
            throw new InvalidDataException("CS2 port is outside the allowed range.");
        foreach (var content in Command.Content)
        {
            if (!Validator.TryValidateObject(content, new ValidationContext(content), null, true))
                throw new InvalidDataException("CS2 content contains an invalid reference.");
            if (!AllowedInstallerPattern().IsMatch(content.InstallerIdentifier))
                throw new InvalidDataException("CS2 content uses an unsupported installer.");
        }
    }

    public (string MapArgument, string MapIdentifier) GetMap()
    {
        var terrain = Command.Terrain ?? throw new InvalidDataException("A CS2 map Terrain is required.");
        if (!MapPattern().IsMatch(terrain.Identifier))
            throw new InvalidDataException("The CS2 map identifier is invalid.");
        return string.Equals(terrain.Source, "SteamWorkshop", StringComparison.OrdinalIgnoreCase)
            ? ("+host_workshop_map", terrain.Identifier)
            : ("+map", terrain.Identifier);
    }

    public (int GameType, int GameMode) GetMode()
    {
        var scenario = Command.Scenario ?? throw new InvalidDataException("A CS2 game-mode Scenario is required.");
        using var document = JsonDocument.Parse(scenario.Metadata);
        if (!document.RootElement.TryGetProperty("gameType", out var type)
            || !document.RootElement.TryGetProperty("gameMode", out var mode)
            || !type.TryGetInt32(out var gameType)
            || !mode.TryGetInt32(out var gameMode)
            || !AllowedModes.Contains((gameType, gameMode)))
            throw new InvalidDataException("The CS2 game-mode metadata is invalid.");
        return (gameType, gameMode);
    }

    private static readonly HashSet<(int, int)> AllowedModes = [(0, 1), (0, 0), (0, 2), (1, 2), (1, 0)];

    [GeneratedRegex("^(?:[a-z][a-z0-9_]{0,63}|[1-9][0-9]{0,19})$", RegexOptions.CultureInvariant)]
    private static partial Regex MapPattern();

    [GeneratedRegex("^(?:|archive-overlay|cs2-metamod|cs2-counterstrikesharp|cs2-plugin)$", RegexOptions.CultureInvariant)]
    private static partial Regex AllowedInstallerPattern();
}

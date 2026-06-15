using System.Text.Json;
using System.Text.Json.Serialization;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>
/// Allowlisted Arma preparation values. Structural filesystem paths and executable arguments remain worker-owned.
/// </summary>
public sealed record Arma3CommandPayload
{
    [JsonIgnore]
    public WorkerCommandPayload Command { get; init; } = new();

    public bool Validate { get; init; }
    public bool Force { get; init; }
    public string? BindAddress { get; init; }
    public string? ProfileName { get; init; }
    public Dictionary<string, JsonElement> ServerConfiguration { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, JsonElement> BasicConfiguration { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> BattlEyeConfiguration { get; init; } = new(StringComparer.Ordinal);

    public static Arma3CommandPayload Parse(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload) || payload.Trim() == "{}")
            return new Arma3CommandPayload();
        try
        {
            var command = JsonSerializer.Deserialize<WorkerCommandPayload>(payload, CloudWorkerJson.Options)
                          ?? new WorkerCommandPayload();
            var arma = JsonSerializer.Deserialize<Arma3CommandPayload>(payload, CloudWorkerJson.Options)
                       ?? new Arma3CommandPayload();
            return arma with { Command = command };
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Arma worker command payload is invalid JSON.", ex);
        }
    }

    public void ValidateAttribution(
        CloudWorkerConnectionConfig connection,
        WorkerCommandClaimResponse command)
    {
        if (Command.ServerId != connection.ServerId || Command.DeploymentId != connection.DeploymentId)
            throw new InvalidDataException("Arma command payload attribution does not match the worker deployment.");
        if (!string.Equals(Command.GameIdentifier, connection.GameIdentifier, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Arma command payload game identifier does not match worker bootstrap.");
        if (!string.Equals(Command.ActionIdentifier, command.ActionIdentifier, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Arma command payload action does not match its command envelope.");
        var isFaultStop = string.Equals(Command.ActionIdentifier, "fault-stop", StringComparison.OrdinalIgnoreCase);
        if (isFaultStop != Command.FaultEvidenceDeadline.HasValue)
        {
            throw new InvalidDataException(
                "Only a fault-stop command may carry its required fault-evidence deadline.");
        }
        if (Command.GracefulStopAttempts is < 1 or > 16
            || Command.GracefulStopTimeoutSeconds is < 1 or > 900)
        {
            throw new InvalidDataException("Arma graceful-stop policy is outside worker safety limits.");
        }
    }
}

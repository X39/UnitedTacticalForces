using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>
/// Allowlisted Arma preparation values. Structural filesystem paths and executable arguments remain worker-owned.
/// </summary>
public sealed record Arma3CommandPayload
{
    public const string SteamWorkshopContentSource = "steam-workshop";
    private const int MaximumContentReferences = 256;

    [JsonIgnore]
    public WorkerCommandPayload Command { get; init; } = new();

    public bool Validate { get; init; }
    public bool Force { get; init; }
    public string? BindAddress { get; init; }
    public string? ProfileName { get; init; } = "UTFServer";
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
        if (!string.Equals(Command.ServiceIdentifier, connection.ServiceIdentifier, StringComparison.Ordinal)
            || Command.ServiceOrdinal != connection.ServiceOrdinal)
        {
            throw new InvalidDataException("Arma command payload service identity does not match worker bootstrap.");
        }
        var topology = Command.Topology
                       ?? throw new InvalidDataException("Arma command topology is missing.");
        var isLegacySinglePrimary = topology.Count == 0 && (Command.IsPrimaryService
            || Command.ActionIdentifier is "stop" or "fault-stop" or "terminate");
        if (!isLegacySinglePrimary
            && (topology.Count is < 1 or > 32
            || topology.Count(q => q.IsPrimary) != 1
            || topology.GroupBy(q => (q.ServiceIdentifier, q.ServiceOrdinal)).Any(q => q.Count() != 1)
            || topology.Any(q => string.IsNullOrWhiteSpace(q.ServiceIdentifier)
                                 || q.ServiceOrdinal is < 0 or > 255
                                 || !IPAddress.TryParse(q.Address, out _)
                                 || q.Port is < 1 or > 65535)))
        {
            throw new InvalidDataException("Arma command topology is invalid.");
        }
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
        _ = GetSteamWorkshopItemIds();
    }

    public IReadOnlyList<ulong> GetSteamWorkshopItemIds()
    {
        var content = Command.Content
                      ?? throw new InvalidDataException("Arma command content references are missing.");
        if (content.Count > MaximumContentReferences)
            throw new InvalidDataException("Arma command contains too many content references.");

        var result = new List<ulong>(content.Count);
        var seen = new HashSet<ulong>();
        foreach (var reference in content)
        {
            if (reference is null
                || !Validator.TryValidateObject(
                    reference,
                    new ValidationContext(reference),
                    null,
                    true))
            {
                throw new InvalidDataException("Arma command contains an invalid content reference.");
            }
            if (!string.Equals(reference.Source, SteamWorkshopContentSource, StringComparison.Ordinal))
            {
                if (reference.Source is not ("uploaded-artifact" or "git" or "built-in"))
                    throw new InvalidDataException("Arma command contains an unsupported content source.");
                if (reference.Source is "uploaded-artifact" or "git"
                    && reference.InstallerIdentifier != "archive-overlay")
                {
                    throw new InvalidDataException("Arma command contains an incompatible content installer.");
                }
                continue;
            }
            if (!ulong.TryParse(
                    reference.Identifier,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var workshopItemId)
                || workshopItemId == 0
                || !string.Equals(
                    reference.Identifier,
                    workshopItemId.ToString(CultureInfo.InvariantCulture),
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException("Arma Workshop content identifier must be a positive integer.");
            }
            if (Command.IsPrimaryService
                    && string.Equals(reference.UsageRole, "ClientOnly", StringComparison.OrdinalIgnoreCase)
                || !Command.IsPrimaryService
                    && string.Equals(reference.UsageRole, "ServerOnly", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (!seen.Add(workshopItemId))
                throw new InvalidDataException("Arma command contains duplicate Workshop content references.");
            result.Add(workshopItemId);
        }
        return result;
    }
}

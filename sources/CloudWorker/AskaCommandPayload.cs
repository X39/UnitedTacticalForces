using System.Text.Json;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>Validates an ASKA command payload against its worker delivery envelope.</summary>
public sealed record AskaCommandPayload
{
    /// <summary>Gets the shared worker command body.</summary>
    public WorkerCommandPayload Command { get; init; } = new();

    /// <summary>Parses an ASKA command payload.</summary>
    /// <param name="payload">Serialized command body.</param>
    /// <returns>The parsed ASKA payload.</returns>
    /// <exception cref="InvalidDataException">Thrown when the payload is malformed.</exception>
    public static AskaCommandPayload Parse(string payload)
    {
        try
        {
            return new AskaCommandPayload
            {
                Command = JsonSerializer.Deserialize<WorkerCommandPayload>(payload, CloudWorkerJson.Options)
                          ?? throw new InvalidDataException("ASKA command payload is empty."),
            };
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("ASKA command payload is invalid JSON.", exception);
        }
    }

    /// <summary>Validates deployment, game, action, and lifecycle attribution.</summary>
    /// <param name="connection">Deployment-scoped worker connection.</param>
    /// <param name="envelope">Claimed command envelope.</param>
    /// <exception cref="InvalidDataException">Thrown when attribution or bounds are invalid.</exception>
    public void Validate(CloudWorkerConnectionConfig connection, WorkerCommandClaimResponse envelope)
    {
        if (Command.ServerId != connection.ServerId || Command.DeploymentId != connection.DeploymentId)
            throw new InvalidDataException("ASKA command attribution does not match this worker.");
        if (!string.Equals(Command.GameIdentifier, "aska", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Command.GameIdentifier, connection.GameIdentifier, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("ASKA command game attribution is invalid.");
        if (!string.Equals(Command.ActionIdentifier, envelope.ActionIdentifier, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("ASKA action attribution is invalid.");
        if (Command.GracefulStopAttempts is < 1 or > 16 || Command.GracefulStopTimeoutSeconds is < 1 or > 900)
            throw new InvalidDataException("ASKA graceful-stop policy is outside worker safety limits.");
        if (Command.Port is not (null or 27015))
            throw new InvalidDataException("ASKA V1 requires gameplay port 27015.");
    }
}

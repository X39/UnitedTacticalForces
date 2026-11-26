using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

/// <summary>
/// The single wire envelope exchanged by the external worker and the control plane over the persistent worker
/// channel.
/// </summary>
/// <remarks>
/// A request frame carries a fresh <see cref="Id"/>; the answering <see cref="EWorkerChannelFrameType.Ack"/> echoes it.
/// <see cref="Payload"/> holds the per-type body: a <see cref="WorkerHeartbeatRequest"/> for heartbeats, a
/// <see cref="WorkerCommandClaimRequest"/> for claims, a <see cref="WorkerCommandStageRequest"/> for received reports,
/// a <see cref="WorkerCommandProcessRequest"/> for running and succeeded reports and a
/// <see cref="WorkerCommandFailureRequest"/> for failure reports. Enums inside payloads travel as string names.
/// </remarks>
public sealed record WorkerChannelFrame
{
    /// <summary>Gets the exchange this frame carries.</summary>
    /// <value>The frame type that selects the route executing the frame.</value>
    public EWorkerChannelFrameType Type { get; init; }

    /// <summary>Gets the correlation id of a request and of the ack answering it.</summary>
    /// <value>A fresh id on a request frame, the echoed request id on an ack, or null for uncorrelated frames.</value>
    public Guid? Id { get; init; }

    /// <summary>Gets the command a report frame refers to.</summary>
    /// <value>The command id for report frames, otherwise null.</value>
    public Guid? CommandId { get; init; }

    /// <summary>Gets how the control plane resolved the request this ack answers.</summary>
    /// <value>The outcome on an ack frame, otherwise null.</value>
    public EWorkerChannelOutcome? Outcome { get; init; }

    /// <summary>Gets the bounded, redacted reason a request was not accepted.</summary>
    /// <value>An error text of at most 512 characters, or null when there is nothing to report.</value>
    [MaxLength(512)]
    public string? Error { get; init; }

    /// <summary>Gets the per-type JSON body of the frame.</summary>
    /// <value>The payload element, or null when the frame type carries no payload.</value>
    public JsonElement? Payload { get; init; }
}

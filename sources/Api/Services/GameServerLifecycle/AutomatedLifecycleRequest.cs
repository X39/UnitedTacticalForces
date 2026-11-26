using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Describes a lifecycle job that the control plane queues on its own, without a requesting user.
/// </summary>
/// <param name="GameServerId">The server the job belongs to.</param>
/// <param name="ActionId">
/// The action to queue. Only <see cref="GameServerActions.Start" /> and <see cref="GameServerActions.Stop" /> are
/// accepted; every other action is refused so an automatic path can never create a terminate job.
/// </param>
/// <param name="Trigger">What caused the job. It must not be <see cref="ELifecycleJobTrigger.Manual" />.</param>
/// <param name="TriggerLimit">
/// The automatic limit that applied: minutes for an idle trigger, hours for a maximum runtime trigger and
/// <see langword="null" /> otherwise.
/// </param>
/// <param name="Now">The instant the request is evaluated at.</param>
/// <param name="AuditNote">
/// An optional note, truncated to 256 characters and sanitized by the audit writer, that is appended to the queue
/// audit details, for example that a due start could not be warned about in advance.
/// </param>
public sealed record AutomatedLifecycleRequest(
    Guid GameServerId,
    string ActionId,
    ELifecycleJobTrigger Trigger,
    int? TriggerLimit,
    Instant Now,
    string? AuditNote = null);

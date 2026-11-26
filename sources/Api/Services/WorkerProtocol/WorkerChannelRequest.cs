using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

/// <summary>
/// One received frame together with the authenticated worker it must be attributed to.
/// </summary>
/// <param name="Worker">The worker authenticated for the channel the frame arrived on.</param>
/// <param name="Credential">
/// The worker credential held for the channel. Routes pass it on so the protocol service re-verifies it on every call.
/// It must never be logged.
/// </param>
/// <param name="Frame">The received frame.</param>
internal sealed record WorkerChannelRequest(AuthenticatedWorker Worker, string Credential, WorkerChannelFrame Frame);

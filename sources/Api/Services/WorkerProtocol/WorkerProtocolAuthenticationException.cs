namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

internal sealed class WorkerProtocolAuthenticationException()
    : UnauthorizedAccessException("The worker credential is invalid or no longer active.");

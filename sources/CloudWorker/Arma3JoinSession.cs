namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>Binds addon evidence to a prepared primary launch outside preserved game data.</summary>
/// <param name="Nonce">The random correlation marker embedded in this launch's server-only addon.</param>
/// <param name="VerifiedProcessId">The process whose engine evidence passed the readiness command.</param>
/// <param name="VerifiedStartedAt">The verified process creation time, preventing PID reuse.</param>
internal sealed record Arma3JoinSession(
    string Nonce, int? VerifiedProcessId = null, DateTimeOffset? VerifiedStartedAt = null);

using System.Diagnostics;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>Represents verified ASKA process state.</summary>
/// <param name="IsRunning">Whether the attributed process remains alive.</param>
/// <param name="Identity">Persisted attributed identity.</param>
/// <param name="Process">Verified process handle.</param>
/// <param name="Error">Safe observation error.</param>
public sealed record AskaProcessObservation(
    bool IsRunning,
    AskaProcessIdentity? Identity,
    Process? Process,
    string? Error) : IDisposable
{
    /// <inheritdoc />
    public void Dispose() => Process?.Dispose();
}

using System.Diagnostics;

namespace X39.UnitedTacticalForces.CloudWorker;

public sealed record Arma3ProcessObservation(
    bool IsRunning,
    Arma3ProcessIdentity? Identity,
    Process? Process,
    string? Error) : IDisposable
{
    public void Dispose() => Process?.Dispose();
}
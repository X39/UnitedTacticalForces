using System.Diagnostics;

namespace X39.UnitedTacticalForces.CloudWorker;

public sealed record CounterStrike2ProcessObservation(
    bool IsRunning,
    CounterStrike2ProcessIdentity? Identity,
    Process? Process,
    string? Error) : IDisposable
{
    public void Dispose() => Process?.Dispose();
}

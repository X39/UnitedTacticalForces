namespace X39.UnitedTacticalForces.CloudWorker;

public interface ICounterStrike2RconClient
{
    Task<bool> RequestQuitAsync(int port, string password, TimeSpan timeout, CancellationToken cancellationToken);
}

namespace X39.UnitedTacticalForces.CloudWorker;

public sealed class WorkerGameExecutorRegistry
{
    private readonly IReadOnlyDictionary<string, Func<IWorkerCommandExecutor>> _factories;

    public WorkerGameExecutorRegistry(IEnumerable<KeyValuePair<string, Func<IWorkerCommandExecutor>>> factories)
    {
        _factories = factories.ToDictionary(
            q => q.Key.Trim().ToLowerInvariant(),
            q => q.Value,
            StringComparer.Ordinal);
    }

    public IWorkerCommandExecutor Create(string gameIdentifier)
        => _factories.TryGetValue(gameIdentifier.Trim().ToLowerInvariant(), out var factory)
            ? factory()
            : throw new InvalidOperationException(
                $"The worker image does not register game implementation '{gameIdentifier}'.");

    public IReadOnlyCollection<string> GameIdentifiers => _factories.Keys.OrderBy(q => q).ToArray();
}

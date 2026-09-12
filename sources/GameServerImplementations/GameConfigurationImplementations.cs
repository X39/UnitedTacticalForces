namespace X39.UnitedTacticalForces.GameServerImplementations;

public static class GameConfigurationImplementations
{
    private static readonly IReadOnlyDictionary<string, IGameConfigurationImplementation> Implementations =
        new IGameConfigurationImplementation[]
        {
            new Arma3ConfigurationImplementation(),
            new AskaConfigurationImplementation(),
            new CounterStrike2ConfigurationImplementation(),
        }.ToDictionary(q => q.GameIdentifier, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyCollection<IGameConfigurationImplementation> All => [.. Implementations.Values];

    public static IGameConfigurationImplementation Get(string gameIdentifier)
        => Implementations.TryGetValue(gameIdentifier, out var implementation)
            ? implementation
            : throw new KeyNotFoundException($"No configuration implementation is registered for '{gameIdentifier}'.");

    public static bool TryGet(string gameIdentifier, out IGameConfigurationImplementation implementation)
        => Implementations.TryGetValue(gameIdentifier, out implementation!);
}

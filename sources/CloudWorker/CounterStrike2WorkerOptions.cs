namespace X39.UnitedTacticalForces.CloudWorker;

public sealed record CounterStrike2WorkerOptions(string ExecutableRelativePath)
{
    internal static CounterStrike2WorkerOptions Create(
        IReadOnlyDictionary<string, string> values,
        Func<string, string?> getEnvironmentVariable)
    {
        const string fallback = "game/bin/linuxsteamrt64/cs2";
        var value = values.GetValueOrDefault("cs2-executable")
                    ?? getEnvironmentVariable("UTF_CS2_EXECUTABLE")
                    ?? fallback;
        if (Path.IsPathFullyQualified(value) || value.Split('/', '\\').Any(q => q is "" or "." or ".."))
            throw new InvalidOperationException("The Counter-Strike 2 executable must be a safe relative path.");
        return new CounterStrike2WorkerOptions(value);
    }
}

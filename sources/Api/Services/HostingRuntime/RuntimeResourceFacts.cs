using X39.UnitedTacticalForces.Database.Hosting;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

/// <summary>
/// Copies bounded, provider-observed facts into the durable deployment-resource inventory.
/// </summary>
internal static class RuntimeResourceFacts
{
    public static void Apply(DeploymentResource target, RuntimeResource observed)
    {
        if (!string.IsNullOrWhiteSpace(observed.ProviderState))
            target.ProviderState = Limit(observed.ProviderState, 128);
        if (!string.IsNullOrWhiteSpace(observed.PublicAddress))
            target.PublicAddress = Limit(observed.PublicAddress, 256);
    }

    private static string Limit(string value, int maximumLength)
    {
        var normalized = value.Trim();
        return normalized.Length <= maximumLength ? normalized : normalized[..maximumLength];
    }
}

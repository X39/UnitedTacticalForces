namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

/// <summary>
/// API-owned durable storage that survives deployment teardown.
/// It is not a game runtime and never contains a running game process.
/// </summary>
public sealed class PersistentServerStorageOptions
{
    public const string SectionName = "Hosting:PersistentStorage";

    /// <summary>
    /// Filesystem-backed storage root. Relative paths are resolved against the API content root.
    /// </summary>
    public string BasePath { get; set; } = ".local/hosting/persistent";
}

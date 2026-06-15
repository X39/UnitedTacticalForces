namespace X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

/// <summary>
/// Fixed protocol limits for deployment-bound persistent file transfers.
/// </summary>
public static class WorkerStorageProtocol
{
    public const int Version = 1;
    public const string ArchiveMediaType = "application/vnd.utf.worker-storage+zip";
    public const string ManifestEntryName = ".utf-transfer-manifest.json";
    public const string FileEntryPrefix = "files/";
    public const int MaximumArchiveBytes = 32 * 1024 * 1024;
    public const int MaximumExpandedBytes = 64 * 1024 * 1024;
    public const int MaximumFileBytes = 16 * 1024 * 1024;
    public const int MaximumEntryCount = 2048;
    public const int MaximumSelectionCount = 256;
}

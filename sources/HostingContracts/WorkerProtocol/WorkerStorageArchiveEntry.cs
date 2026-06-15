namespace X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

public sealed record WorkerStorageArchiveEntry
{
    public string RelativePath { get; init; } = string.Empty;

    public bool IsDirectory { get; init; }

    public long Length { get; init; }

    /// <summary>
    /// Upper-case hexadecimal SHA-256 for files; null for directories.
    /// </summary>
    public string? Sha256 { get; init; }
}
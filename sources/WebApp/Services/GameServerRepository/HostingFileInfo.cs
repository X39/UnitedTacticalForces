namespace X39.UnitedTacticalForces.WebApp.Services.GameServerRepository;

/// <summary>
/// One safe, server-relative entry in either the persistent or live file context.
/// </summary>
public sealed record HostingFileInfo
{
    public string Path { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsDirectory { get; init; }
    public long? Size { get; init; }
    public DateTimeOffset? ModifiedAt { get; init; }
    public string Context { get; init; } = string.Empty;
    public string Classification { get; init; } = string.Empty;
    public bool CanModify { get; init; }
}

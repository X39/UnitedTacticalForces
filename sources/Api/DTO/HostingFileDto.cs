using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Safe server-relative file-system entry.
/// </summary>
public sealed record HostingFileDto
{
    public string Path { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsDirectory { get; init; }
    public long? Size { get; init; }
    public Instant? ModifiedAt { get; init; }
    public EFileContext Context { get; init; }
    public EFileClassification Classification { get; init; }
    public bool CanModify { get; init; }
}

namespace X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

public sealed record WorkerSelectedContent
{
    public string Source { get; init; } = string.Empty;
    public string Identifier { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string VersionIdentifier { get; init; } = string.Empty;
    public string Metadata { get; init; } = "{}";
    public string? ArtifactIdentifier { get; init; }
    public string? ChecksumSha256 { get; init; }
}

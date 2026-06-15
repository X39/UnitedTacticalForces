namespace X39.UnitedTacticalForces.WebApp.Services.GameServerRepository;

public sealed record PreservationSelectionInfoRequest
{
    public string Path { get; init; } = string.Empty;
    public string Scope { get; init; } = "File";
    public string Mode { get; init; } = "CreateOrUpdate";
}

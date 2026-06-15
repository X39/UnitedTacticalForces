using X39.UnitedTacticalForces.WebApp.UtfApi.Models;
using X39.UnitedTacticalForces.WebApp.UtfApi.Models;

namespace X39.UnitedTacticalForces.WebApp.Services.CommandCenterRepository;

public record CommandCenterAuthorityClaimInfo
{
    public PlainClaimDto Claim { get; init; } = new();

    public int DirectMemberCount { get; init; }

    public int RoleCount { get; init; }

    public string[] RoleTitles { get; init; } = [];
}

using X39.UnitedTacticalForces.WebApp.UtfApi.Models;
using X39.UnitedTacticalForces.WebApp.UtfApi.Models;

namespace X39.UnitedTacticalForces.WebApp.Services.CommandCenterRepository;

public record CommandCenterAuthorityRoleInfo
{
    public PlainRoleDto Role { get; init; } = new();

    public PlainClaimDto[] Claims { get; init; } = [];

    public int MemberCount { get; init; }
}

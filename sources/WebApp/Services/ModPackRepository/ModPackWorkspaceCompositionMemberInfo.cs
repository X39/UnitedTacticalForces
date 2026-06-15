using X39.UnitedTacticalForces.WebApp.UtfApi.Models;

namespace X39.UnitedTacticalForces.WebApp.Services.ModPackRepository;

public record ModPackWorkspaceCompositionMemberInfo
{
    public PlainModPackDefinitionDto Definition { get; init; } = new();

    public PlainUserDto? Owner { get; init; }

    public PlainModPackRevisionDto Revision { get; init; } = new();
}

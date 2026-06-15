using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

public sealed record GameServerActionDefinition(
    string ActionId,
    string Label,
    string Description,
    string Icon,
    string Group,
    int Order,
    bool IsDangerous,
    bool RequiresConfirmation,
    TimeSpan? ExpectedDuration,
    ECostImpact CostImpact,
    string CostEffectDescription);
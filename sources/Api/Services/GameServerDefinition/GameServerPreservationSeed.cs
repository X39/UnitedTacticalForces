using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

public sealed record GameServerPreservationSeed(
    string RelativePath,
    EPreservationScope Scope,
    EPreservationMode Mode);
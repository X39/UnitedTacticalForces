using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

public sealed record GameServerFileDefinition(
    string RelativePath,
    EFileClassification Classification,
    bool Recursive,
    bool MutableWhileStopped,
    bool MutableWhileRunning,
    bool Sensitive);
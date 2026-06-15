using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Hosting;

namespace Api.Tests.GameServerHosting;

internal sealed record HostingTestSeed(
    User User,
    CostOwner CostOwner,
    GameServer Server,
    GameServerDeployment? Deployment);
using JetBrains.Annotations;
using X39.UnitedTacticalForces.Api.Authorization.Abstraction;
using X39.UnitedTacticalForces.Api.Data.Authority;

namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requirement for any server permission.
/// </summary>
[MeansImplicitUse(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
public sealed class ServerAnyRequirement : ServerIdRequirement
{
    /// <summary>
    /// Creates a new instance of <see cref="ServerIdRequirement"/>.
    /// </summary>
    public ServerAnyRequirement()
        : base(
            $"{Claims.ResourceBased.Server.All}:*",
            new[]
            {
                Claims.ResourceBased.Server.StartStop,
                Claims.ResourceBased.Server.ModPack,
                Claims.ResourceBased.Server.Configuration,
                Claims.ResourceBased.Server.Upgrade,
                Claims.ResourceBased.Server.Files,
                Claims.ResourceBased.Server.AccessLogs,
                Claims.ResourceBased.Server.DeleteLogs,
            })
    {
    }
}
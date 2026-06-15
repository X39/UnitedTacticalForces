using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Meta;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Database.Hosting;

/// <summary>
/// Provider/runtime resource allocated or adopted for a deployment.
/// </summary>
[Index(nameof(DeploymentFk), nameof(State))]
public class DeploymentResource : IPrimaryKey<Guid>, ITimeStampCreated
{
    public const string UniqueProviderResourceIndexName = "IX_DeploymentResources_ProviderIdentity_Unique";
    public const string UniqueProviderResourceIndexFilter = "\"ProviderResourceIdentifier\" IS NOT NULL";

    [Key]
    public Guid PrimaryKey { get; set; }

    [ForeignKey(nameof(DeploymentFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public GameServerDeployment? Deployment { get; set; }

    public Guid DeploymentFk { get; set; }

    [MaxLength(128)]
    public string ProviderIdentifier { get; set; } = string.Empty;

    [MaxLength(256)]
    public string ProvisioningIntentIdentifier { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? ProviderResourceIdentifier { get; set; }

    [MaxLength(128)]
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// Last provider-reported state for this resource. Retained after release as audit evidence.
    /// </summary>
    [MaxLength(128)]
    public string? ProviderState { get; set; }

    /// <summary>
    /// Last provider-reported public address, when applicable. Retained after release as audit evidence.
    /// </summary>
    [MaxLength(256)]
    public string? PublicAddress { get; set; }

    public EDeploymentResourceState State { get; set; }

    public bool PossibleCost { get; set; }

    public Instant TimeStampCreated { get; set; }

    public Instant TimeStampUpdated { get; set; }

    /// <summary>
    /// Time at which the control plane durably recorded that a provider create request was about to be issued.
    /// </summary>
    public Instant? TimeStampProvisioningRequested { get; set; }

    /// <summary>
    /// Time at which provider discovery or a definitive response resolved that create attempt.
    /// </summary>
    public Instant? TimeStampProvisioningResolved { get; set; }

    public Instant? TimeStampObserved { get; set; }

    public Instant? TimeStampReleased { get; set; }
}

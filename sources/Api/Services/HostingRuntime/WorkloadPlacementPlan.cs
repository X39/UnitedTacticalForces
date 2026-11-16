using System.Text.Json;
using System.Text.Json.Serialization;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

/// <summary>Captures the accepted placement and allocation for a deployment.</summary>
/// <param name="Version">The persistence format version.</param>
/// <param name="PrimaryHostKey">The host that owns the main endpoint and persistent data.</param>
/// <param name="PrimaryConnection">The advertised connection on the primary host, when one exists.</param>
/// <param name="Hosts">The distinct provider allocations for the deployment.</param>
/// <param name="Workloads">The workloads assigned to those allocations.</param>
internal sealed record WorkloadPlacementPlan(
    int Version,
    WorkloadPlacementKey PrimaryHostKey,
    WorkloadPlacementConnection? PrimaryConnection,
    IReadOnlyList<WorkloadPlacementHost> Hosts,
    IReadOnlyList<WorkloadPlacementAssignment> Workloads)
{
    /// <summary>The snapshot format written by this application version.</summary>
    internal const int CurrentVersion = 1;

    /// <summary>Serialization settings for the persisted snapshot.</summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Gets the allocation that owns the primary workload.</summary>
    /// <value>The host matching the stored primary key.</value>
    [JsonIgnore]
    public WorkloadPlacementHost PrimaryHost => Hosts.Single(host => host.Key == PrimaryHostKey);

    /// <summary>Gets the aggregate estimated hourly compute rate.</summary>
    /// <value>The sum of the host compute rates in EUR.</value>
    [JsonIgnore]
    public decimal EstimatedComputeHourlyCostEur => Hosts.Sum(host => host.ComputeHourlyCostEur);

    /// <summary>Gets the aggregate estimated hourly storage rate.</summary>
    /// <value>The sum of the host storage rates in EUR.</value>
    [JsonIgnore]
    public decimal EstimatedStorageHourlyCostEur => Hosts.Sum(host => host.StoragePlan.EstimatedHourlyCostEur);

    /// <summary>Gets the estimated total hourly rate used for deployment affordability.</summary>
    /// <value>The combined hourly compute and storage rate in EUR.</value>
    [JsonIgnore]
    public decimal EstimatedHourlyCostEur => EstimatedComputeHourlyCostEur + EstimatedStorageHourlyCostEur;

    /// <summary>Gets the estimated total monthly rate for the deployment.</summary>
    /// <value>The combined compute and storage rate in EUR, or null when monthly compute pricing is unavailable.</value>
    [JsonIgnore]
    public decimal? EstimatedMonthlyCostEur => Hosts.All(host => host.ComputeMonthlyCostEur.HasValue)
        ? Hosts.Sum(host => host.ComputeMonthlyCostEur) + EstimatedStorageHourlyCostEur * 730m
        : null;

    /// <summary>Validates and serializes the placement for durable storage.</summary>
    /// <returns>The JSON placement snapshot.</returns>
    /// <exception cref="InvalidOperationException">The placement is invalid.</exception>
    public string Serialize()
    {
        Validate();
        return JsonSerializer.Serialize(this, JsonOptions);
    }

    /// <summary>Reads a placement snapshot before any operation that depends on its allocation.</summary>
    /// <param name="snapshot">The persisted JSON document.</param>
    /// <returns>The validated placement and allocation.</returns>
    /// <exception cref="InvalidOperationException">The snapshot is malformed, unsupported, or invalid.</exception>
    public static WorkloadPlacementPlan Deserialize(string snapshot)
    {
        try
        {
            var plan = JsonSerializer.Deserialize<WorkloadPlacementPlan>(snapshot, JsonOptions)
                       ?? throw new InvalidOperationException("The deployment placement snapshot is empty.");
            plan.Validate();
            return plan;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("The deployment placement snapshot is malformed.", ex);
        }
    }

    /// <summary>Reads placement metadata for displays or cleanup paths that can use historical attribution.</summary>
    /// <param name="snapshot">The persisted JSON document, if available.</param>
    /// <returns>The validated placement, or null when the document cannot be used.</returns>
    public static WorkloadPlacementPlan? TryDeserialize(string? snapshot)
    {
        if (snapshot is null)
            return null;
        try
        {
            return Deserialize(snapshot);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Validates the shape, prices, and references needed to use this snapshot safely.</summary>
    /// <exception cref="InvalidOperationException">The plan has invalid or unsupported data.</exception>
    internal void Validate()
    {
        if (Version != CurrentVersion)
            throw new InvalidOperationException("The deployment placement snapshot version is unsupported.");
        if (Hosts is not { Count: > 0 }
            || Hosts.Any(host => host is null
                                 || !IsValidKey(host.Key)
                                 || host.InboundNetworkRequirements is null
                                 || string.IsNullOrWhiteSpace(host.RuntimeProfileIdentifier)
                                 || !IsValidStoragePlan(host.StoragePlan)
                                 || host.ComputeHourlyCostEur <= 0
                                 || host.ComputeMonthlyCostEur is < 0
                                 || host.ProfileSystemReserveBytes < 0))
        {
            throw new InvalidOperationException("The deployment placement has an invalid host allocation.");
        }

        var hostKeys = Hosts.Select(host => host.Key).ToHashSet();
        if (hostKeys.Count != Hosts.Count || !hostKeys.Contains(PrimaryHostKey))
            throw new InvalidOperationException("The deployment placement has missing or duplicate host identities.");
        if (Workloads is not { Count: > 0 }
            || Workloads.Any(workload => workload is null
                                         || !IsValidKey(workload.WorkloadKey)
                                         || !hostKeys.Contains(workload.HostKey))
            || Workloads.Select(workload => workload.WorkloadKey).ToHashSet().Count != Workloads.Count
            || !hostKeys.SetEquals(Workloads.Select(workload => workload.HostKey)))
        {
            throw new InvalidOperationException("The deployment placement has invalid workload assignments.");
        }

        foreach (var host in Hosts.Where(host => host.InboundNetworkRequirements.Count > 0))
        {
            if (RuntimeInboundNetworkPlanValidator.Validate(host.InboundNetworkRequirements) is { } failure)
                throw new InvalidOperationException(failure);
        }
        if (PrimaryConnection is { } connection
            && !PrimaryHost.InboundNetworkRequirements.Any(requirement =>
                requirement.Protocol == connection.Protocol
                && connection.Port >= requirement.StartPort
                && connection.Port <= requirement.EndPort))
        {
            throw new InvalidOperationException("The primary connection is outside the host's ingress policy.");
        }
    }

    /// <summary>Checks that an identity can be stored in the worker attribution fields.</summary>
    /// <param name="key">The identity under validation.</param>
    /// <returns>Whether the key can identify a deployment instance.</returns>
    private static bool IsValidKey(WorkloadPlacementKey? key)
        => key is { ServiceOrdinal: >= 0 }
           && !string.IsNullOrWhiteSpace(key.ServiceIdentifier)
           && key.ServiceIdentifier.Length <= 128;

    /// <summary>Checks that the storage allocation can meet its recorded demand.</summary>
    /// <param name="storage">The storage allocation under validation.</param>
    /// <returns>Whether the storage strategy, size, root, and rate are usable.</returns>
    private static bool IsValidStoragePlan(RuntimeStoragePlan? storage)
        => storage is { RequiredCapacityBytes: >= 0, EstimatedHourlyCostEur: >= 0 }
           && Enum.IsDefined(storage.Strategy)
           && storage.SelectedCapacityBytes >= storage.RequiredCapacityBytes
           && HasValidCapacityEvidence(storage)
           && !string.IsNullOrWhiteSpace(storage.DeploymentRoot);

    /// <summary>Validates forecast evidence while retaining historical snapshots for discovery and cleanup.</summary>
    /// <param name="storage">The frozen storage allocation.</param>
    /// <returns>Whether evidence is absent in a legacy plan or matches the immutable supported policy.</returns>
    private static bool HasValidCapacityEvidence(RuntimeStoragePlan storage)
    {
        if (storage.CapacityPolicyVersion is null && storage.EstimatedUsableCapacityBytes is null)
            return true;
        return storage.UsesExternalStorage
               && storage.CapacityPolicyVersion == RuntimeStorageCapacityPolicy.CurrentVersion
               && storage.EstimatedUsableCapacityBytes
               == RuntimeStorageCapacityPolicy.EstimateUsableBytes(storage.SelectedCapacityBytes)
               && storage.EstimatedUsableCapacityBytes >= storage.RequiredCapacityBytes;
    }
}

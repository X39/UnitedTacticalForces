using System.Text.Json;
using X39.UnitedTacticalForces.Api.Services.GameServerConfiguration;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.GameServerImplementations;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>Verifies the workload, allocation, and pricing invariants of the persisted placement plan.</summary>
public sealed class WorkloadPlacementResolverTests
{
    /// <summary>Prices each distinct host and round-trips the allocations and assignments.</summary>
    /// <param name="selection">The selected Headless Client placement, or null for historical placement.</param>
    /// <param name="expectedHosts">The number of compute and external storage allocations.</param>
    [Theory]
    [InlineData(null, 2)]
    [InlineData(Arma3ConfigurationImplementation.SeparateHostPlacement, 2)]
    [InlineData(Arma3ConfigurationImplementation.SameHostPlacement, 1)]
    public void ArmaPlacement_RoundTripsAssignmentsAndHostRates(string? selection, int expectedHosts)
    {
        var definition = new Arma3GameServerDefinitionRegistration().Definition;
        var profile = new RuntimeProfile("general", "General", "Compute profile", EstimatedMonthlyCostEur: 59m);
        var storage = new RuntimeStoragePlan(
            ERuntimeStorageStrategy.FixedExternal,
            25 * RuntimeStoragePlanResolver.GiB,
            30 * RuntimeStoragePlanResolver.GiB,
            "/srv/volume",
            0.02m);
        var plan = WorkloadPlacementResolver.Resolve(
            definition,
            selection is null ? null : CreateConfiguration(selection, true),
            profile,
            storage,
            fallbackHourlyCostEur: 0.1m,
            profileSystemReserveBytes: 12);

        var persisted = WorkloadPlacementPlan.Deserialize(plan.Serialize());

        Assert.Equal(expectedHosts, persisted.Hosts.Count);
        Assert.Equal(2, persisted.Workloads.Count);
        Assert.Equal(new WorkloadPlacementKey("game-server", 0), persisted.PrimaryHostKey);
        Assert.Equal(2302, persisted.PrimaryConnection?.Port);
        Assert.Single(persisted.PrimaryHost.InboundNetworkRequirements);
        var headless = Assert.Single(persisted.Workloads, workload => workload.WorkloadKey.ServiceIdentifier
            == Arma3GameServerDefinitionRegistration.HeadlessClientServiceIdentifier);
        Assert.Equal(0, headless.HostKey.ServiceOrdinal);
        if (expectedHosts == 1)
        {
            Assert.Equal(persisted.PrimaryHostKey, headless.HostKey);
        }
        else
        {
            Assert.Equal(headless.WorkloadKey, headless.HostKey);
            Assert.Empty(Assert.Single(persisted.Hosts, host => host.Key == headless.HostKey)
                .InboundNetworkRequirements);
        }
        Assert.All(persisted.Hosts, host =>
        {
            Assert.Equal("general", host.RuntimeProfileIdentifier);
            Assert.Equal(storage, host.StoragePlan);
            Assert.Equal(12, host.ProfileSystemReserveBytes);
        });
        Assert.Equal(0.1m * expectedHosts, persisted.EstimatedComputeHourlyCostEur);
        Assert.Equal(0.02m * expectedHosts, persisted.EstimatedStorageHourlyCostEur);
        Assert.Equal(0.12m * expectedHosts, persisted.EstimatedHourlyCostEur);
        Assert.Equal((59m + 730 * 0.02m) * expectedHosts, persisted.EstimatedMonthlyCostEur);
    }

    /// <summary>Rejects an invalid effective placement even when a lower-priority value survived resolution.</summary>
    [Fact]
    public void ArmaPlacement_RejectsIncompatibleEffectiveSelection()
    {
        var definition = new Arma3GameServerDefinitionRegistration().Definition;
        var profile = new RuntimeProfile("general", "General", "Compute profile", EstimatedHourlyCostEur: 0.1m);
        var storage = new RuntimeStoragePlan(ERuntimeStorageStrategy.IncludedDisk, 0, 0, "/srv/runtime", 0m);
        var configuration = CreateConfiguration(Arma3ConfigurationImplementation.SeparateHostPlacement, false);

        Assert.Throws<InvalidOperationException>(
            () => WorkloadPlacementResolver.Resolve(definition, configuration, profile, storage));
    }

    /// <summary>Uses the existing primary ingress and one host for a game without a placement policy.</summary>
    [Fact]
    public void DefaultPlacement_PreservesPrimaryIngressAndIncludedDisk()
    {
        var definition = new AskaGameServerDefinitionRegistration().Definition;
        var profile = new RuntimeProfile("general", "General", "Compute profile", EstimatedHourlyCostEur: 0.1m);
        var storage = new RuntimeStoragePlan(ERuntimeStorageStrategy.IncludedDisk, 0, 0, "/srv/runtime", 0m);
        var plan = WorkloadPlacementResolver.Resolve(definition, null, profile, storage);

        var host = Assert.Single(plan.Hosts);
        Assert.Equal(host.Key, Assert.Single(plan.Workloads).HostKey);
        Assert.Equal(definition.InboundNetworkRequirements, host.InboundNetworkRequirements);
        Assert.Equal(0.1m, plan.EstimatedHourlyCostEur);
        Assert.Equal(0m, plan.EstimatedStorageHourlyCostEur);
    }

    /// <summary>Allows projection and cleanup callers to fall back when persisted metadata is unreadable.</summary>
    /// <param name="snapshot">Missing, malformed, or unsupported snapshot content.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("{")]
    [InlineData("{\"version\":2}")]
    public void TryDeserialize_ReturnsMissingForUnreadableSnapshot(string? snapshot)
        => Assert.Null(WorkloadPlacementPlan.TryDeserialize(snapshot));

    /// <summary>Creates an effective placement entry without relying on a server or profile database.</summary>
    /// <param name="selection">The effective string selection.</param>
    /// <param name="isCompatible">Whether the highest-priority value passed typed validation.</param>
    /// <returns>The resolved configuration used by the placement policy.</returns>
    private static ResolvedGameConfiguration CreateConfiguration(string selection, bool isCompatible)
    {
        var implementation = new Arma3ConfigurationImplementation();
        var definition = implementation.Definitions.Single(definition =>
            definition.Key == Arma3ConfigurationImplementation.HeadlessClientPlacementKey);
        return new ResolvedGameConfiguration(
            Guid.NewGuid(),
            implementation,
            null,
            null,
            null,
            [new(definition, JsonSerializer.SerializeToElement(selection), "Server", isCompatible, null)]);
    }
}

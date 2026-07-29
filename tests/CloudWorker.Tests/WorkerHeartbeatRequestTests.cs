using System.ComponentModel.DataAnnotations;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using Xunit;

namespace CloudWorker.Tests;

public class WorkerHeartbeatRequestTests
{
    [Fact]
    public void StorageTelemetry_AcceptsCoherentValuesOrPairedNulls()
    {
        Assert.Empty(Validate(new WorkerHeartbeatRequest()));
        Assert.Empty(Validate(new WorkerHeartbeatRequest
        {
            DeploymentStorageTotalBytes = 1_000,
            DeploymentStorageAvailableBytes = 400,
            ControlledDeploymentBytes = 300,
        }));
    }

    [Theory]
    [InlineData(-1L, 0L)]
    [InlineData(100L, -1L)]
    [InlineData(100L, 101L)]
    [InlineData(100L, null)]
    [InlineData(null, 50L)]
    public void StorageTelemetry_RejectsNegativeIncompleteOrIncoherentValues(
        long? totalBytes,
        long? availableBytes)
    {
        var request = new WorkerHeartbeatRequest
        {
            DeploymentStorageTotalBytes = totalBytes,
            DeploymentStorageAvailableBytes = availableBytes,
            ControlledDeploymentBytes = totalBytes is null || availableBytes is null ? null : 0,
        };

        Assert.NotEmpty(Validate(request));
    }

    [Fact]
    public void StorageTelemetry_RequiresControlledRootUsageWithDiskTelemetry()
    {
        var request = new WorkerHeartbeatRequest
        {
            DeploymentStorageTotalBytes = 1_000,
            DeploymentStorageAvailableBytes = 400,
        };

        Assert.NotEmpty(Validate(request));
    }

    private static IReadOnlyCollection<ValidationResult> Validate(WorkerHeartbeatRequest request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, true);
        return results;
    }
}

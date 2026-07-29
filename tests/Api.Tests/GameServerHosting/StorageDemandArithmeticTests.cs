using X39.UnitedTacticalForces.Api.Services.StorageDemand;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

public sealed class StorageDemandArithmeticTests
{
    [Fact]
    public void Aggregation_SaturatesInsteadOfWrapping()
    {
        Assert.Equal(long.MaxValue, StorageDemandService.AddSaturated(long.MaxValue - 4, 5));
        Assert.Equal(long.MaxValue, StorageDemandService.SumSaturated([long.MaxValue - 4, 5, 10]));
        Assert.Equal(long.MaxValue, StorageDemandService.AddSaturated(-1, 1));
    }

    [Fact]
    public void PeakPolicy_AppliesWorkspaceAndMargin()
    {
        var result = StorageDemandService.CalculatePeakPolicy(100, 80, 1m, 20, 20m);

        Assert.Equal(100, result.Workspace);
        Assert.Equal(40, result.Margin);
        Assert.Equal(240, result.Peak);
        Assert.False(result.Overflowed);
    }

    [Fact]
    public void EvidencePrecedence_UsesFreshMatchingMeasurementThenSteamThenFallback()
    {
        var now = Instant.FromUtc(2026, 7, 29, 10, 0);
        var options = new StorageDemandOptions();
        var observation = new ContentStorageObservation
        {
            ReportedBytes = 100,
            MeasuredBytes = 120,
            ExternalVersion = "7",
            MeasuredVersion = "7",
            ReportedAt = now - Duration.FromHours(1),
            MeasuredAt = now - Duration.FromDays(1),
            ObservationSource = "steam-web-api",
            ObservationStatus = "available",
        };
        var missing = new List<string>();

        var measured = StorageDemandService.SelectComponent(
            observation, "workshop:1", "Item", "workshop", 1_000, now, options, missing);
        observation.MeasuredVersion = "6";
        var estimated = StorageDemandService.SelectComponent(
            observation, "workshop:1", "Item", "workshop", 1_000, now, options, missing);
        observation.ReportedAt = now - Duration.FromDays(2);
        var fallback = StorageDemandService.SelectComponent(
            observation, "workshop:1", "Item", "workshop", 1_000, now, options, missing);

        Assert.Equal(120, measured.RequiredBytes);
        Assert.Equal(EStorageEstimateStatus.Measured, measured.Status);
        Assert.Equal(100, estimated.RequiredBytes);
        Assert.Equal(EStorageEstimateStatus.Estimated, estimated.Status);
        Assert.Equal(1_000, fallback.RequiredBytes);
        Assert.Equal(EStorageEstimateStatus.Incomplete, fallback.Status);
        Assert.Contains("workshop:1", missing);
    }

    [Fact]
    public void ControlledTreeMeasurement_CountsFilesAndExcludesMetadata()
    {
        var root = Path.Combine(Path.GetTempPath(), "utf-storage-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "nested"));
        try
        {
            File.WriteAllBytes(Path.Combine(root, "runtime.bin"), new byte[17]);
            File.WriteAllBytes(Path.Combine(root, "nested", "content.bin"), new byte[23]);
            File.WriteAllBytes(Path.Combine(root, ".utf-metadata"), new byte[100]);

            Assert.Equal(40, StorageDemandService.MeasureTree(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

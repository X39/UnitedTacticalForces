namespace X39.UnitedTacticalForces.Api.Services.StorageDemand;

public sealed class StorageDemandOptions
{
    public const string SectionName = "Hosting:StorageDemand";
    public const long GiB = 1024L * 1024 * 1024;

    public TimeSpan SteamMetadataFreshness { get; set; } = TimeSpan.FromHours(24);
    public TimeSpan MeasurementFreshness { get; set; } = TimeSpan.FromDays(30);
    public long UnknownArmaRuntimeBytes { get; set; } = 40L * GiB;
    public long UnknownWorkshopItemBytes { get; set; } = 10L * GiB;
    public decimal PreparationWorkspaceMultiplier { get; set; } = 1m;
    public long PreparationWorkspaceFixedBytes { get; set; } = 5L * GiB;
    public decimal SafetyMarginPercent { get; set; } = 20m;
    public long RuntimeProfileSystemReserveBytes { get; set; } = 5L * GiB;
}

namespace X39.UnitedTacticalForces.WebApp.Services.CommandCenterRepository;

public record CommandCenterSettingsSnapshotInfo
{
    public bool ApiBaseUrlConfigured { get; init; }

    public bool ClientBaseUrlConfigured { get; init; }

    public bool BasePathConfigured { get; init; }

    public bool AutoVerifyNewUsers { get; init; }

    public CommandCenterSettingsFeatureInfo[] Features { get; init; } = [];
}

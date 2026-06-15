namespace X39.UnitedTacticalForces.WebApp.Services.CommandCenterRepository;

public record CommandCenterSettingsFeatureInfo
{
    public string Area { get; init; } = string.Empty;

    public bool IsEnabled { get; init; }

    public bool IsConfigured { get; init; }

    public string Detail { get; init; } = string.Empty;
}

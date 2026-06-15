namespace X39.UnitedTacticalForces.WebApp.Components;

public sealed record UtfCreationStepItem(
    string Key,
    string Label,
    bool IsActive,
    bool IsComplete,
    bool IsEnabled
);

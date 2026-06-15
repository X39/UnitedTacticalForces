namespace X39.UnitedTacticalForces.Common.PublicPages;

public sealed class GdprTimelineSection
{
    public string Kicker { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public List<GdprTimelineStep> Steps { get; set; } = [];
}
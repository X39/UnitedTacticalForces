namespace X39.UnitedTacticalForces.Common.PublicPages;

public sealed class GdprDataMapSection
{
    public string Kicker { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public List<GdprDataMapCard> Cards { get; set; } = [];
}
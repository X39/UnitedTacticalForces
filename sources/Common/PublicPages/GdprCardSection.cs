namespace X39.UnitedTacticalForces.Common.PublicPages;

public sealed class GdprCardSection
{
    public string Kicker { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public List<GdprSimpleCard> Cards { get; set; } = [];
}
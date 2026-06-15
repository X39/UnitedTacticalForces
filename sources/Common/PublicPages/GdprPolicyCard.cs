namespace X39.UnitedTacticalForces.Common.PublicPages;

public sealed class GdprPolicyCard
{
    public string Kicker { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public List<string> Paragraphs { get; set; } = [];
}
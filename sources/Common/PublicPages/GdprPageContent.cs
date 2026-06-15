namespace X39.UnitedTacticalForces.Common.PublicPages;

public sealed class GdprPageContent
{
    public GdprIntroSection Intro { get; set; } = new();

    public List<GdprPolicyCard> OpeningCards { get; set; } = [];

    public GdprTimelineSection Lifecycle { get; set; } = new();

    public GdprDataMapSection DataMap { get; set; } = new();

    public GdprCardSection Visibility { get; set; } = new();

    public GdprCardSection Partners { get; set; } = new();

    public List<GdprPolicyCard> PolicyCards { get; set; } = [];
}
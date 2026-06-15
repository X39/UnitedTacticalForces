namespace X39.UnitedTacticalForces.Common.PublicPages;

public static class GdprPageContentNormalizer
{
    public static GdprPageContent Normalize(GdprPageContent? content)
    {
        content              ??= new GdprPageContent();
        content.Intro        =   NormalizeIntro(content.Intro);
        content.OpeningCards =   NormalizeItems(content.OpeningCards, NormalizePolicyCard);
        content.Lifecycle    =   NormalizeTimeline(content.Lifecycle);
        content.DataMap      =   NormalizeDataMap(content.DataMap);
        content.Visibility   =   NormalizeCardSection(content.Visibility);
        content.Partners     =   NormalizeCardSection(content.Partners);
        content.PolicyCards  =   NormalizeItems(content.PolicyCards, NormalizePolicyCard);

        return content;
    }

    private static GdprIntroSection NormalizeIntro(GdprIntroSection? intro)
    {
        intro                   ??= new GdprIntroSection();
        intro.Kicker            =   NormalizeString(intro.Kicker);
        intro.Title             =   NormalizeString(intro.Title);
        intro.Description       =   NormalizeString(intro.Description);
        intro.LastReviewedLabel =   NormalizeString(intro.LastReviewedLabel);
        intro.LastReviewedDate  =   NormalizeString(intro.LastReviewedDate);
        intro.ProcessNote       =   NormalizeString(intro.ProcessNote);
        return intro;
    }

    private static void NormalizePolicyCard(GdprPolicyCard card)
    {
        card.Kicker     = NormalizeString(card.Kicker);
        card.Title      = NormalizeString(card.Title);
        card.Paragraphs = NormalizeStrings(card.Paragraphs);
    }

    private static GdprTimelineSection NormalizeTimeline(GdprTimelineSection? lifecycle)
    {
        lifecycle        ??= new GdprTimelineSection();
        lifecycle.Kicker =   NormalizeString(lifecycle.Kicker);
        lifecycle.Title  =   NormalizeString(lifecycle.Title);
        lifecycle.Steps  =   NormalizeItems(lifecycle.Steps, NormalizeTimelineStep);
        return lifecycle;
    }

    private static void NormalizeTimelineStep(GdprTimelineStep step)
    {
        step.Title      = NormalizeString(step.Title);
        step.Paragraphs = NormalizeStrings(step.Paragraphs);
    }

    private static GdprDataMapSection NormalizeDataMap(GdprDataMapSection? dataMap)
    {
        dataMap        ??= new GdprDataMapSection();
        dataMap.Kicker =   NormalizeString(dataMap.Kicker);
        dataMap.Title  =   NormalizeString(dataMap.Title);
        dataMap.Cards  =   NormalizeItems(dataMap.Cards, NormalizeDataMapCard);
        return dataMap;
    }

    private static void NormalizeDataMapCard(GdprDataMapCard card)
    {
        card.Title        = NormalizeString(card.Title);
        card.DataLabel    = NormalizeString(card.DataLabel);
        card.Data         = NormalizeString(card.Data);
        card.PurposeLabel = NormalizeString(card.PurposeLabel);
        card.Purpose      = NormalizeString(card.Purpose);
        card.WhereLabel   = NormalizeString(card.WhereLabel);
        card.Where        = NormalizeString(card.Where);
    }

    private static GdprCardSection NormalizeCardSection(GdprCardSection? section)
    {
        section        ??= new GdprCardSection();
        section.Kicker =   NormalizeString(section.Kicker);
        section.Title  =   NormalizeString(section.Title);
        section.Cards  =   NormalizeItems(section.Cards, NormalizeSimpleCard);
        return section;
    }

    private static void NormalizeSimpleCard(GdprSimpleCard card)
    {
        card.Title = NormalizeString(card.Title);
        card.Body  = NormalizeString(card.Body);
    }

    private static List<T> NormalizeItems<T>(List<T>? values, Action<T> normalize)
        where T : class
    {
        if (values is null)
            return [];

        var normalized = new List<T>(values.Count);
        foreach (var value in values)
        {
            if (value is null)
                continue;

            normalize(value);
            normalized.Add(value);
        }

        return normalized;
    }

    private static List<string> NormalizeStrings(List<string>? values)
    {
        if (values is null)
            return [];

        for (var i = 0; i < values.Count; i++)
            values[i] = values[i] ?? string.Empty;

        return values;
    }

    private static string NormalizeString(string? value)
        => value ?? string.Empty;
}
using System.Text;

namespace X39.UnitedTacticalForces.Common.PublicPages;

public static class GdprPageContentMarkdown
{
    public static string ToMarkdown(GdprPageContent content)
    {
        content = GdprPageContentNormalizer.Normalize(content);
        var builder = new StringBuilder();
        AppendHeading(builder, 1, content.Intro.Title);
        AppendParagraph(builder, content.Intro.Description);

        foreach (var card in content.OpeningCards)
            AppendPolicyCard(builder, card);

        AppendHeading(builder, 2, content.Lifecycle.Title);
        foreach (var step in content.Lifecycle.Steps)
        {
            AppendHeading(builder, 3, step.Title);
            foreach (var paragraph in step.Paragraphs)
                AppendParagraph(builder, paragraph);
        }

        AppendHeading(builder, 2, content.DataMap.Title);
        foreach (var card in content.DataMap.Cards)
        {
            AppendHeading(builder, 3, card.Title);
            AppendListItem(builder, card.DataLabel, card.Data);
            AppendListItem(builder, card.PurposeLabel, card.Purpose);
            AppendListItem(builder, card.WhereLabel, card.Where);
            builder.AppendLine();
        }

        AppendCardSection(builder, content.Visibility);
        AppendCardSection(builder, content.Partners);

        foreach (var card in content.PolicyCards)
            AppendPolicyCard(builder, card);

        return builder.ToString().Trim();
    }

    private static void AppendPolicyCard(StringBuilder builder, GdprPolicyCard card)
    {
        AppendHeading(builder, 2, card.Title);
        foreach (var paragraph in card.Paragraphs)
            AppendParagraph(builder, paragraph);
    }

    private static void AppendCardSection(StringBuilder builder, GdprCardSection section)
    {
        AppendHeading(builder, 2, section.Title);
        foreach (var card in section.Cards)
        {
            AppendHeading(builder, 3, card.Title);
            AppendParagraph(builder, card.Body);
        }
    }

    private static void AppendHeading(StringBuilder builder, int level, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        builder.AppendLine($"{new string('#', level)} {text.Trim()}");
        builder.AppendLine();
    }

    private static void AppendParagraph(StringBuilder builder, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        builder.AppendLine(text.Trim());
        builder.AppendLine();
    }

    private static void AppendListItem(StringBuilder builder, string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        var safeLabel = string.IsNullOrWhiteSpace(label) ? "Value" : label.Trim();
        builder.AppendLine($"- **{safeLabel}:** {value.Trim()}");
    }
}
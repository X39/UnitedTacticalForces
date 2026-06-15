namespace X39.UnitedTacticalForces.WebApp.Data;

public sealed class EventObjectiveDraftModel
{
    private static readonly string[] DefaultIcons =
    [
        "objective",
        "warning",
        "radio",
    ];

    public Guid DraftKey { get; init; } = Guid.NewGuid();

    public string Icon { get; set; } = "objective";

    public string Text { get; set; } = string.Empty;

    public bool HasText
        => !string.IsNullOrWhiteSpace(Text);

    public static EventObjectiveDraftModel CreateDefault(int index)
        => new()
        {
            Icon = DefaultIcons[Math.Clamp(index, 0, DefaultIcons.Length - 1)],
        };

    public string ToMarkdownLine()
        => $"- :{Icon}: {Text.Trim()}";
}

namespace X39.UnitedTacticalForces.WebApp.Services.VereinRepository;

public record VereinDocumentRevisionUpsertInfo
{
    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string Markdown { get; init; } = string.Empty;

    public string? Comment { get; init; }

    public bool IsPublished { get; init; }
}

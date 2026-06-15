namespace X39.UnitedTacticalForces.WebApp.Services.VereinRepository;

public record VereinDocumentRevisionInfo
{
    public long PrimaryKey { get; init; }

    public long DocumentFk { get; init; }

    public int Version { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string Markdown { get; init; } = string.Empty;

    public string Comment { get; init; } = string.Empty;

    public bool IsPublished { get; init; }

    public DateTimeOffset? TimeStampPublished { get; init; }

    public DateTimeOffset TimeStampCreated { get; init; }

    public Guid? AuthorFk { get; init; }

    public string? AuthorName { get; init; }
}

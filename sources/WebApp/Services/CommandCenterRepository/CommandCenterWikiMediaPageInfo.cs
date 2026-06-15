namespace X39.UnitedTacticalForces.WebApp.Services.CommandCenterRepository;

public record CommandCenterWikiMediaPageInfo
{
    public Guid PrimaryKey { get; init; }

    public string Title { get; init; } = string.Empty;

    public bool IsDeleted { get; init; }

    public DateTimeOffset TimeStampCreated { get; init; }

    public int RevisionCount { get; init; }

    public DateTimeOffset? LatestRevisionTimeStamp { get; init; }

    public string? LatestRevisionAuthorName { get; init; }

    public string? LatestRevisionComment { get; init; }
}

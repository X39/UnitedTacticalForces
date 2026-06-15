using System.ComponentModel.DataAnnotations;
using X39.UnitedTacticalForces.WebApp.UtfApi.Models;

namespace X39.UnitedTacticalForces.WebApp.Data;

public sealed class EventAuthoringModel
{
    [Required]
    public string? Title { get; set; }

    [Required]
    public string? Description { get; set; }

    [Required]
    public DateTime? ScheduledForLocal { get; set; }

    [Required]
    public Guid? HostedByFk { get; set; }

    [Required]
    public long? TerrainFk { get; set; }

    [Required]
    public long? ModPackRevisionFk { get; set; }

    [Required]
    public int? MinimumAccepted { get; set; }

    public bool IsVisible { get; set; } = true;

    public Guid? ImageAssetFk { get; set; }

    public string? ImagePath { get; set; }

    public static EventAuthoringModel CreateDefault()
    {
        return new EventAuthoringModel
        {
            ScheduledForLocal = GetDefaultScheduledFor(),
            Description       = string.Empty,
            IsVisible         = true,
        };
    }

    public static EventAuthoringModel FromFullEvent(FullEventDto eventItem)
    {
        return new EventAuthoringModel
        {
            Title             = eventItem.Title,
            Description       = eventItem.Description,
            ScheduledForLocal = eventItem.ScheduledFor?.ToLocalTime().DateTime,
            HostedByFk        = eventItem.HostedBy?.PrimaryKey,
            TerrainFk         = eventItem.TerrainPrimaryKey,
            ModPackRevisionFk = eventItem.ModPackRevisionPrimaryKey,
            MinimumAccepted   = eventItem.MinimumAccepted,
            IsVisible         = eventItem.IsVisible ?? true,
            ImageAssetFk      = eventItem.ImageAssetFk,
            ImagePath         = eventItem.ImagePath,
        };
    }

    public EventCreationPayload ToCreationPayload()
    {
        return new EventCreationPayload
        {
            Title             = Title?.Trim(),
            Description       = Description?.Trim(),
            ScheduledFor      = ToScheduledFor(),
            HostedByFk        = HostedByFk,
            TerrainFk         = TerrainFk,
            ModPackRevisionFk = ModPackRevisionFk,
            MinimumAccepted   = MinimumAccepted,
            IsVisible         = IsVisible,
            ImageAssetFk      = ImageAssetFk ?? default,
        };
    }

    public EventUpdate ToUpdatePayload()
    {
        var payload = new EventUpdate
        {
            Title             = Title?.Trim(),
            Description       = Description?.Trim(),
            ScheduledFor      = ToScheduledFor(),
            HostedByFk        = HostedByFk,
            TerrainFk         = TerrainFk,
            ModPackRevisionFk = ModPackRevisionFk,
            MinimumAccepted   = MinimumAccepted,
            IsVisible         = IsVisible,
            ImageAssetFk      = ImageAssetFk,
        };

        return payload;
    }

    public IReadOnlyList<string> GetValidationErrors(bool requireImage)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Title))
            errors.Add("Title is required.");
        if (ScheduledForLocal is null)
            errors.Add("Schedule is required.");
        if (HostedByFk is null)
            errors.Add("Host is required.");
        if (TerrainFk is null)
            errors.Add("Terrain is required.");
        if (ModPackRevisionFk is null)
            errors.Add("Mod pack revision is required.");
        if (MinimumAccepted is null or < 0)
            errors.Add("Minimum accepted must be zero or greater.");
        if (string.IsNullOrWhiteSpace(Description))
            errors.Add("Briefing is required.");
        if (requireImage && ImageAssetFk is null)
            errors.Add("Mission image is required.");

        return errors;
    }

    private DateTimeOffset? ToScheduledFor()
        => ScheduledForLocal is null ? null : new DateTimeOffset(ScheduledForLocal.Value);

    private static DateTime GetDefaultScheduledFor()
    {
        var todayAtTwenty = DateTime.Today.AddHours(20);
        return todayAtTwenty > DateTime.Now
            ? todayAtTwenty
            : todayAtTwenty.AddDays(1);
    }
}

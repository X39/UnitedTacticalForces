namespace X39.UnitedTacticalForces.WebSockets.Contracts.Eventing;

public class EventChangedMessage
{
    public Guid EventId { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public long? TerrainFk { get; set; }
    public long? ModPackRevisionId { get; set; }
    public Guid? ImageAssetFk { get; set; }
    public string? ImagePath { get; set; }
    public DateTimeOffset? ScheduledFor { get; set; }
    public bool? IsVisible { get; set; }
    public int? MinimumAccepted { get; set; }
    public Guid? HostedById { get; set; }
}

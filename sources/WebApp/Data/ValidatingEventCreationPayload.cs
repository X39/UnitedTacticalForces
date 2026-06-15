using System.ComponentModel.DataAnnotations;
using X39.UnitedTacticalForces.WebApp.UtfApi.Models;

namespace X39.UnitedTacticalForces.WebApp.Data;

public sealed class ValidatingEventCreationPayload
{
    public class Part1Impl
    {
        public Guid? ImageAssetFk { get; set; }

        [Required]
        public DateTime? ScheduledFor { get; set; }

        [Required]
        public string? Title { get; set; }
    }

    public sealed class Part2Impl
    {
        [Required]
        public long? ModPackRevisionFk { get; set; }

        [Required]
        public long? TerrainFk { get; set; }
    }

    public sealed class Part3Impl
    {
        [Required]
        public string? Description { get; set; }
    }

    public sealed class Part4Impl
    {
        [Required]
        public Guid? HostedByFk { get; set; }

        [Required]
        public bool? IsVisible { get; set; }

        [Required]
        public int? MinimumAccepted { get; set; }
    }

    public Part1Impl Part1 { get; set; } = new();
    public Part2Impl Part2 { get; set; } = new();
    public Part3Impl Part3 { get; set; } = new();
    public Part4Impl Part4 { get; set; } = new();

    public static implicit operator EventCreationPayload(ValidatingEventCreationPayload payload)
        => new EventCreationPayload
        {
            Description       = payload.Part3.Description,
            HostedByFk        = payload.Part4.HostedByFk,
            ImageAssetFk      = payload.Part1.ImageAssetFk ?? default,
            IsVisible         = payload.Part4.IsVisible,
            MinimumAccepted   = payload.Part4.MinimumAccepted,
            ModPackRevisionFk = payload.Part2.ModPackRevisionFk,
            ScheduledFor      = payload.Part1.ScheduledFor,
            TerrainFk         = payload.Part2.TerrainFk,
            Title             = payload.Part1.Title,
        };
}

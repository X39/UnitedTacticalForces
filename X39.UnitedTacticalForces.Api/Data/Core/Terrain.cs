using System.ComponentModel.DataAnnotations;

namespace X39.UnitedTacticalForces.Api.Data.Core;

public class Terrain
{
    [Key]
    public long Id { get; set; }

    public string Title { get; set; } = string.Empty;
    public byte[] Image { get; set; } = Array.Empty<byte>();
    public string ImageMimeType { get; set; } = string.Empty;
}
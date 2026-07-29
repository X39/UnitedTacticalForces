using System.Text.Json;
using System.Text.Json.Serialization;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal sealed class HetznerServerTypeDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal Cores { get; set; }
    public decimal Memory { get; set; }
    public decimal Disk { get; set; }

    [JsonPropertyName("cpu_type")]
    public string? CpuType { get; set; }

    public string? Architecture { get; set; }
    public IReadOnlyList<HetznerServerTypeLocationDto>? Locations { get; set; }
    public IReadOnlyList<HetznerServerTypePriceDto>? Prices { get; set; }
}

internal sealed class HetznerServerTypeLocationDto
{
    public required long Id { get; set; }
    public required string? Name { get; set; }
    public required bool Available { get; set; }
    public required bool Recommended { get; set; }
    public required JsonElement Deprecation { get; set; }
}

internal sealed class HetznerLocationDto
{
    public long? Id { get; set; }
    public string? Name { get; set; }
}

internal sealed class HetznerServerTypePriceDto
{
    public string? Location { get; set; }

    [JsonPropertyName("price_hourly")]
    public HetznerPriceDto? PriceHourly { get; set; }

    [JsonPropertyName("price_monthly")]
    public HetznerPriceDto? PriceMonthly { get; set; }
}

internal sealed class HetznerPriceDto
{
    public string? Gross { get; set; }
}

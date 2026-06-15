using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Api.Tests;

public sealed class InstantToLongConverter() : ValueConverter<Instant, long>(
    instant => instant.ToUnixTimeTicks(),
    ticks => Instant.FromUnixTimeTicks(ticks));

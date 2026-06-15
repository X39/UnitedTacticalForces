using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Api.Tests;

public sealed class LocalDateToIntConverter() : ValueConverter<LocalDate, int>(
    localDate => Period.Between(new LocalDate(1970, 1, 1), localDate, PeriodUnits.Days).Days,
    days => new LocalDate(1970, 1, 1).PlusDays(days));

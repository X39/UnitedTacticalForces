namespace X39.UnitedTacticalForces.Api.DTO.Updates;

/// <summary>
/// The language a signed-in user reads messages in.
/// </summary>
public sealed record UserLocaleUpdate
{
    /// <summary>
    /// Gets the locale to record.
    /// </summary>
    /// <value>A locale such as <c>de</c> or <c>en-GB</c>; only English and German are accepted.</value>
    public string Locale { get; init; } = string.Empty;
}

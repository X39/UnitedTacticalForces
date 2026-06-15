namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Public, secret-free authentication provider availability.
/// </summary>
public sealed record AuthenticationProvidersDto
{
    /// <summary>
    /// Whether Steam authentication is registered and available.
    /// </summary>
    public bool Steam { get; init; }

    /// <summary>
    /// Whether Discord authentication is registered and available.
    /// </summary>
    public bool Discord { get; init; }
}

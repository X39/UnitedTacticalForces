namespace X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

/// <summary>Detects, validates, and canonicalizes one external mod source kind.</summary>
public interface IModSourceProvider
{
    /// <summary>Gets the stable provider identifier.</summary>
    string Identifier { get; }

    /// <summary>Returns whether this provider recognizes an input.</summary>
    /// <param name="input">User-supplied source input.</param>
    /// <returns><see langword="true"/> when recognized.</returns>
    bool CanResolve(string input);

    /// <summary>Resolves canonical source metadata.</summary>
    /// <param name="input">User-supplied source input.</param>
    /// <param name="versionSelector">Optional provider version selector.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Canonical source metadata.</returns>
    /// <exception cref="ModSourceUnavailableException">Thrown when provider metadata is temporarily unavailable.</exception>
    Task<ModSourceResolution> ResolveAsync(
        string input,
        string? versionSelector,
        CancellationToken cancellationToken = default);
}

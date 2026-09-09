using System.Text.RegularExpressions;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

/// <summary>Validates reusable uploaded-artifact identities.</summary>
[Scoped<UploadedArtifactModSourceProvider, IModSourceProvider>]
public sealed partial class UploadedArtifactModSourceProvider : IModSourceProvider
{
    /// <inheritdoc />
    public string Identifier => ModSourceProviderIdentifiers.UploadedArtifact;

    /// <inheritdoc />
    public bool CanResolve(string input) => ArtifactIdentifierRegex().IsMatch(input.Trim());

    /// <inheritdoc />
    public Task<ModSourceResolution> ResolveAsync(
        string input,
        string? versionSelector,
        CancellationToken cancellationToken = default)
    {
        var identifier = input.Trim().ToLowerInvariant();
        if (!ArtifactIdentifierRegex().IsMatch(identifier))
            throw new ArgumentException("The uploaded artifact identifier is invalid.", nameof(input));
        return Task.FromResult(new ModSourceResolution(Identifier, identifier, identifier, string.Empty));
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex ArtifactIdentifierRegex();
}

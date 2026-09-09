using System.Net;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

/// <summary>Validates public HTTPS Git repository sources against the configured host allowlist.</summary>
[Scoped<GitModSourceProvider, IModSourceProvider>]
public sealed class GitModSourceProvider(IConfiguration configuration) : IModSourceProvider
{
    private readonly HashSet<string> _allowedHosts = configuration
        .GetSection("Hosting:ModSources:Git:AllowedHosts")
        .Get<string[]>()?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

    /// <inheritdoc />
    public string Identifier => ModSourceProviderIdentifiers.Git;

    /// <inheritdoc />
    public bool CanResolve(string input)
        => Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri)
           && uri.Scheme == Uri.UriSchemeHttps
           && (uri.AbsolutePath.EndsWith(".git", StringComparison.OrdinalIgnoreCase)
               || _allowedHosts.Contains(uri.Host));

    /// <inheritdoc />
    public Task<ModSourceResolution> ResolveAsync(
        string input,
        string? versionSelector,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !uri.IsDefaultPort
            || !_allowedHosts.Contains(uri.IdnHost)
            || IPAddress.TryParse(uri.IdnHost, out _))
            throw new ArgumentException("Git sources require an allowlisted public HTTPS repository URL without credentials or a custom port.", nameof(input));
        var selector = versionSelector?.Trim() ?? string.Empty;
        if (selector.Length > 256 || selector.Any(char.IsControl) || selector.StartsWith("-", StringComparison.Ordinal))
            throw new ArgumentException("The Git ref is invalid.", nameof(versionSelector));
        var builder = new UriBuilder(uri) { Fragment = string.Empty, Query = string.Empty };
        var canonical = builder.Uri.AbsoluteUri.TrimEnd('/');
        return Task.FromResult(new ModSourceResolution(Identifier, canonical, Path.GetFileNameWithoutExtension(uri.AbsolutePath.TrimEnd('/')), selector));
    }
}

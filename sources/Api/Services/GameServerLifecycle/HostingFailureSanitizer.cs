using System.Text.RegularExpressions;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Produces bounded, secret-safe failure text suitable for persistence and user projections.
/// </summary>
internal static partial class HostingFailureSanitizer
{
    private const int DefaultMaximumLength = 4096;
    private const int AbsoluteMaximumLength = 16384;

    public static string Sanitize(Exception exception) => Sanitize(exception.Message);

    public static string Sanitize(string? value, int maximumLength = DefaultMaximumLength)
    {
        if (maximumLength <= 0 || maximumLength > AbsoluteMaximumLength)
            throw new ArgumentOutOfRangeException(nameof(maximumLength));
        if (string.IsNullOrWhiteSpace(value))
            return "The operation failed without additional details.";

        var sanitized = CredentialAssignmentRegex().Replace(value, "$1=<redacted>");
        sanitized = AuthorizationHeaderRegex().Replace(sanitized, "$1<redacted>");
        sanitized = BearerTokenRegex().Replace(sanitized, "$1<redacted>");
        sanitized = UriCredentialRegex().Replace(sanitized, "$1<redacted>@");
        return sanitized.Length <= maximumLength
            ? sanitized
            : sanitized[..maximumLength];
    }

    [GeneratedRegex("(?i)(password|passwd|pwd|token|secret|credential|api[-_]?key)\\s*[:=]\\s*([^\\s;,]+)")]
    private static partial Regex CredentialAssignmentRegex();

    [GeneratedRegex("(?i)(authorization\\s*[:=]?\\s*(?:bearer)?[-\\s]+)[^\\s;,]+")]
    private static partial Regex AuthorizationHeaderRegex();

    [GeneratedRegex("(?i)(bearer\\s+)[a-z0-9._~+/-]+=*")]
    private static partial Regex BearerTokenRegex();

    [GeneratedRegex("(https?://[^:/\\s]+:)[^@/\\s]+@", RegexOptions.IgnoreCase)]
    private static partial Regex UriCredentialRegex();
}

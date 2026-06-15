using System.Text.RegularExpressions;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

/// <summary>
/// Produces bounded, display-safe infrastructure failures without credential-shaped values.
/// </summary>
public static partial class HostingFailureSanitizer
{
    private const int MaximumLength = 1024;

    public static string Sanitize(string? value, params string?[] knownSecrets)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "The hosting operation failed.";

        var sanitized = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        foreach (var knownSecret in knownSecrets.Where(q => !string.IsNullOrEmpty(q)))
            sanitized = sanitized.Replace(knownSecret!, "[redacted]", StringComparison.Ordinal);

        sanitized = CredentialAssignmentRegex().Replace(sanitized, "$1=[redacted]");
        sanitized = BearerTokenRegex().Replace(sanitized, "$1 [redacted]");
        sanitized = LoginCommandRegex().Replace(sanitized, "$1 [redacted] [redacted]");
        sanitized = CommandCredentialRegex().Replace(sanitized, "$1 [redacted]");
        sanitized = UriCredentialRegex().Replace(sanitized, "$1[redacted]@");
        return sanitized.Length <= MaximumLength ? sanitized : sanitized[..MaximumLength];
    }

    [GeneratedRegex("(?i)\\b(password|passwd|pwd|secret|token|api[-_]?key|client[-_]?secret|"
                    + "rcon[-_]?password)\\s*[:=]\\s*([^\\s;,]+)")]
    private static partial Regex CredentialAssignmentRegex();

    [GeneratedRegex("(?i)\\b(bearer|authorization)\\s+[^\\s;,]+")]
    private static partial Regex BearerTokenRegex();

    [GeneratedRegex("(?i)(\\+login|--password|-password|--token)\\s+[^\\s;,]+")]
    private static partial Regex CommandCredentialRegex();

    [GeneratedRegex("(?i)(\\+?login)\\s+[^\\s;,]+\\s+[^\\s;,]+")]
    private static partial Regex LoginCommandRegex();

    [GeneratedRegex("(?i)(https?://[^:/\\s]+:)[^@/\\s]+@")]
    private static partial Regex UriCredentialRegex();
}

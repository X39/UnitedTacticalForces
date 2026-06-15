using System.Text.RegularExpressions;

namespace X39.UnitedTacticalForces.CloudWorker;

internal static class WorkerSecretRedactor
{
    private const string RedactedLine  = "[redacted sensitive worker output]";
    private const string RedactedValue = "****************";
    private static readonly Regex CredentialShapePattern = new(
        "(?:\\bauthorization\\b[\"']?\\s*[:=]|\\bbearer\\s+\\S|"
        + "\\b(?:bootstrapcredential|workercredential|password|secret|token)\\b[\"']?\\s*[:=])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string? Redact(string? value, IEnumerable<string>? exactSecrets = null)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var sanitized = value;
        if (exactSecrets is not null)
        {
            foreach (var secret in exactSecrets
                         .Where(q => !string.IsNullOrEmpty(q))
                         .Distinct(StringComparer.Ordinal)
                         .OrderByDescending(q => q.Length))
            {
                sanitized = sanitized.Replace(secret, RedactedValue, StringComparison.Ordinal);
            }
        }
        var lines = sanitized.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        return string.Join(Environment.NewLine, lines.Select(line =>
            CredentialShapePattern.IsMatch(line)
                ? RedactedLine
                : line));
    }
}
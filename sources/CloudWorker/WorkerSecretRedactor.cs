using System.Text.RegularExpressions;

using System.Net;
using System.Text;

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
                foreach (var representation in Representations(secret))
                    sanitized = sanitized.Replace(representation, RedactedValue, StringComparison.Ordinal);
            }
        }
        var lines = sanitized.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        return string.Join(Environment.NewLine, lines.Select(line =>
            CredentialShapePattern.IsMatch(line)
                ? RedactedLine
                : line));
    }

    private static IEnumerable<string> Representations(string secret)
    {
        yield return secret;
        if (secret.Length < 4) yield break;
        var bytes = Encoding.UTF8.GetBytes(secret);
        var base64 = Convert.ToBase64String(bytes);
        yield return base64;
        yield return base64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
        yield return WebUtility.UrlEncode(secret);
        yield return Convert.ToHexString(bytes);
        yield return Convert.ToHexStringLower(bytes);
    }
}

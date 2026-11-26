using System.Globalization;
using System.Text.RegularExpressions;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>
/// Parses only the versioned, bounded player count records emitted by the managed server addon. The count covers
/// connected human users and excludes headless clients and the server itself.
/// </summary>
/// <param name="Sequence">The strictly increasing server-process sequence of player records.</param>
/// <param name="Humans">The number of connected human users, from zero to <see cref="MaximumHumans" />.</param>
internal sealed partial record Arma3PlayerEvidence(long Sequence, int Humans)
{
    /// <summary>The largest human count the worker accepts as an observation.</summary>
    internal const int MaximumHumans = 1024;

    /// <summary>The longest diagnostic line the parser inspects.</summary>
    private const int MaximumLineLength = 256;

    /// <summary>Reads a complete diagnostic line without accepting arbitrary embedded marker text.</summary>
    /// <param name="line">One complete output line, without its newline.</param>
    /// <param name="nonce">The current launch's exact correlation marker.</param>
    /// <returns>The validated observation, or null for unrelated, malformed or obsolete output.</returns>
    internal static Arma3PlayerEvidence? Parse(string line, string nonce)
    {
        if (line.Length > MaximumLineLength)
            return null;
        if (line.Count(c => c == '"') is not (0 or 2))
            return null;
        var match = RecordPattern().Match(line);
        if (!match.Success || match.Groups[1].Value != nonce
            || !long.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var sequence)
            || sequence <= 0
            || !int.TryParse(match.Groups[3].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var humans)
            || humans > MaximumHumans)
            return null;
        return new Arma3PlayerEvidence(sequence, humans);
    }

    /// <summary>Accepts only the documented diagnostic timestamp decoration and numeric protocol fields.</summary>
    /// <returns>The bounded, non-backtracking protocol parser.</returns>
    [GeneratedRegex("^\\s*(?:(?:[0-9]{4}/[0-9]{2}/[0-9]{2},\\s+)?[0-9]{1,2}:[0-9]{2}:[0-9]{2}(?:\\.[0-9]+)?\\s+)?\"?UTFPL\\|1\\|([0-9a-f]{32})\\|([0-9]{1,16})\\|([0-9]{1,5})\"?\\s*$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex RecordPattern();
}

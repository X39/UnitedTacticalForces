using System.Globalization;
using System.Text.RegularExpressions;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>Parses only the versioned, bounded records emitted by the managed server addon.</summary>
/// <param name="Sequence">The strictly increasing server-process sequence, retained across missions.</param>
/// <param name="Owner">The engine's network owner, or zero when the expected HC is absent.</param>
/// <param name="Ready">Whether exactly one expected engine-classified HC is in mission state ten.</param>
internal sealed partial record Arma3JoinEvidence(long Sequence, int Owner, bool Ready)
{
    /// <summary>Reads a complete diagnostic line without accepting arbitrary embedded marker text.</summary>
    /// <param name="line">One complete output line, without its newline.</param>
    /// <param name="nonce">The current launch's exact correlation marker.</param>
    /// <returns>The validated observation, or null for unrelated, malformed or obsolete output.</returns>
    internal static Arma3JoinEvidence? Parse(string line, string nonce)
    {
        if (line.Length > 256)
            return null;
        if (line.Count(c => c == '"') is not (0 or 2))
            return null;
        var match = RecordPattern().Match(line);
        if (!match.Success || match.Groups[1].Value != nonce
            || !long.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var sequence)
            || sequence <= 0
            || !int.TryParse(match.Groups[3].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var owner))
            return null;
        var ready = match.Groups[4].Value == "1";
        return (ready && owner <= 2) || (!ready && owner != 0)
            ? null : new Arma3JoinEvidence(sequence, owner, ready);
    }

    /// <summary>Accepts only the documented diagnostic timestamp decoration and numeric protocol fields.</summary>
    /// <returns>The bounded, non-backtracking protocol parser.</returns>
    [GeneratedRegex("^\\s*(?:(?:[0-9]{4}/[0-9]{2}/[0-9]{2},\\s+)?[0-9]{1,2}:[0-9]{2}:[0-9]{2}(?:\\.[0-9]+)?\\s+)?\"?UTFHC\\|1\\|([0-9a-f]{32})\\|([0-9]{1,16})\\|([0-9]{1,10})\\|([01])\"?\\s*$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex RecordPattern();
}

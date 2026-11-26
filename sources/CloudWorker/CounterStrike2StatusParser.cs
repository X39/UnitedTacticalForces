using System.Globalization;
using System.Text.RegularExpressions;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>
/// Reads the human player count from the response of the Counter-Strike 2 RCON <c>status</c> command.
/// </summary>
/// <remarks>
/// The Counter-Strike 2 status format is not formally documented. The parser only accepts the players summary line
/// (for example <c>players  : 3 humans, 1 bots (16 max) (not hibernating)</c>); any other shape returns
/// <see langword="false" /> and therefore never yields a guessed number. The live format is confirmed by the
/// end-of-phase UAT against a real server.
/// </remarks>
internal static partial class CounterStrike2StatusParser
{
    /// <summary>The largest human count or capacity the parser accepts.</summary>
    private const int MaximumValue = 1024;

    /// <summary>Tries to read the human player count and the capacity from a status response.</summary>
    /// <param name="statusText">The decoded status response, or <see langword="null" />.</param>
    /// <param name="humans">The human player count when the method returns <see langword="true" />, otherwise 0.</param>
    /// <param name="capacity">
    /// The maximum player capacity when the line reports it, otherwise <see langword="null" />.
    /// </param>
    /// <returns>
    /// <see langword="true" /> when a players summary line with values from 0 to 1024 was found; otherwise
    /// <see langword="false" />.
    /// </returns>
    public static bool TryParse(string? statusText, out int humans, out int? capacity)
    {
        humans = 0;
        capacity = null;
        if (string.IsNullOrEmpty(statusText)) return false;
        Match match;
        try
        {
            match = PlayersLinePattern().Match(statusText);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
        if (!match.Success) return false;
        if (!TryReadValue(match.Groups["humans"], out var parsedHumans)) return false;
        int? parsedCapacity = null;
        if (match.Groups["max"].Success)
        {
            if (!TryReadValue(match.Groups["max"], out var max)) return false;
            parsedCapacity = max;
        }
        humans = parsedHumans;
        capacity = parsedCapacity;
        return true;
    }

    /// <summary>Parses one captured digit group and applies the 0 to 1024 bound.</summary>
    /// <param name="group">The captured digits.</param>
    /// <param name="value">The parsed value when the method returns <see langword="true" />.</param>
    /// <returns><see langword="true" /> when the digits are a number from 0 to 1024.</returns>
    private static bool TryReadValue(Group group, out int value)
        => int.TryParse(group.ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out value)
           && value is >= 0 and <= MaximumValue;

    /// <summary>Matches the players summary line with its humans, optional bots and optional max parts.</summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(
        @"players[ \t]*:[ \t]*(?<humans>[0-9]{1,5})[ \t]+humans?(?:[ \t]*,[ \t]*[0-9]{1,5}[ \t]+bots?)?(?:[ \t]*\((?<max>[0-9]{1,5})[ \t]+max\))?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex PlayersLinePattern();
}

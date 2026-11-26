namespace X39.UnitedTacticalForces.Api.Helpers;

/// <summary>
/// Builds escaped patterns for case-insensitive <c>ILIKE</c> searches so that user supplied search text is matched
/// literally instead of being interpreted as wildcard syntax.
/// </summary>
/// <remarks>
/// Pass the backslash character as the escape argument of <c>EF.Functions.ILike</c> together with a pattern built by
/// this class.
/// </remarks>
internal static class LikePattern
{
    /// <summary>
    /// Trims the value and escapes the backslash, percent sign, underscore, square brackets and caret so that each of
    /// them is matched literally.
    /// </summary>
    /// <param name="value">The raw search text.</param>
    /// <returns>The trimmed and escaped search text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value" /> is <see langword="null" />.</exception>
    public static string Escape(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Trim()
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal)
            .Replace("]", "\\]", StringComparison.Ordinal)
            .Replace("^", "\\^", StringComparison.Ordinal);
    }

    /// <summary>
    /// Builds a contains pattern: the escaped value wrapped in percent signs.
    /// </summary>
    /// <param name="value">The raw search text.</param>
    /// <returns>The pattern that matches every text containing the search text literally.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value" /> is <see langword="null" />.</exception>
    public static string Contains(string value)
        => $"%{Escape(value)}%";
}

using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// Proves that the English and German API resource files define the same <c>CostAlert_*</c> keys with the same
/// format placeholders. The check is scoped to cost alert keys because other German keys are known to be missing.
/// </summary>
public sealed partial class CostAlertResourceTests
{
    private const string KeyPrefix = "CostAlert_";

    private const string AutoStopKeyPrefix = "AutoStop_";

    /// <summary>
    /// Every English cost alert key exists in German with a non-empty value and the same placeholder set.
    /// </summary>
    [Fact]
    public void CostAlertKeys_HaveGermanTranslationWithSamePlaceholders()
    {
        var english = ReadCostAlertEntries("Language.resx");
        var german = ReadCostAlertEntries("Language.de.resx");

        Assert.True(english.Count >= 5, "Language.resx should define at least five CostAlert_* keys.");
        Assert.Equal(english.Keys.OrderBy(k => k), german.Keys.OrderBy(k => k));
        foreach (var (key, englishValue) in english)
        {
            var germanValue = german[key];
            Assert.False(string.IsNullOrWhiteSpace(englishValue), $"{key} is empty in English.");
            Assert.False(string.IsNullOrWhiteSpace(germanValue), $"{key} is empty in German.");
            Assert.Equal(Placeholders(englishValue), Placeholders(germanValue));
        }
    }

    /// <summary>
    /// Every English automatic stop key exists in German with a non-empty value and the same placeholder set, and
    /// all seven keys of the renderer are defined.
    /// </summary>
    [Fact]
    public void AutoStopKeys_HaveGermanTranslationWithSamePlaceholders()
    {
        var english = ReadCostAlertEntries("Language.resx", AutoStopKeyPrefix);
        var german = ReadCostAlertEntries("Language.de.resx", AutoStopKeyPrefix);

        Assert.Equal(7, english.Count);
        Assert.Equal(english.Keys.OrderBy(k => k), german.Keys.OrderBy(k => k));
        foreach (var (key, englishValue) in english)
        {
            var germanValue = german[key];
            Assert.False(string.IsNullOrWhiteSpace(englishValue), $"{key} is empty in English.");
            Assert.False(string.IsNullOrWhiteSpace(germanValue), $"{key} is empty in German.");
            Assert.Equal(Placeholders(englishValue), Placeholders(germanValue));
        }
    }

    private static Dictionary<string, string> ReadCostAlertEntries(string fileName, string keyPrefix = KeyPrefix)
    {
        var path = Path.Combine(FindRepositoryRoot(), "sources", "Api", "Properties", fileName);
        return XDocument.Load(path)
            .Descendants("data")
            .Select(element => (
                Name: (string?) element.Attribute("name") ?? string.Empty,
                Value: (string?) element.Element("value") ?? string.Empty))
            .Where(entry => entry.Name.StartsWith(keyPrefix, StringComparison.Ordinal))
            .ToDictionary(entry => entry.Name, entry => entry.Value);
    }

    private static string[] Placeholders(string value)
        => PlaceholderRegex()
            .Matches(value)
            .Select(match => match.Value)
            .Distinct()
            .OrderBy(placeholder => placeholder, StringComparer.Ordinal)
            .ToArray();

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "X39.UnitedTacticalForces.slnx")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("The repository root containing the solution file was not found.");
    }

    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex PlaceholderRegex();
}

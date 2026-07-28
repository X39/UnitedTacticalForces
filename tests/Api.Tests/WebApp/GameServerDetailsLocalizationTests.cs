using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Api.Tests.WebApp;

public partial class GameServerDetailsLocalizationTests
{
    private const string DetailsPath = "sources/WebApp/Pages/Servers/Details.razor";
    private const string EnglishResourcePath = "sources/WebApp/Properties/Language.resx";
    private const string GermanResourcePath = "sources/WebApp/Properties/Language.de.resx";
    private static readonly string[] DynamicLocalizationPrefixes =
    [
        "Hosting_Action_",
        "Hosting_ActionGroup_",
        "Hosting_CostImpact_",
        "Hosting_DeploymentStatus_",
        "Hosting_FileClassification_",
        "Hosting_LifecycleJobKind_",
        "Hosting_LifecycleJobStatus_",
        "Hosting_ServerMessage_",
        "Hosting_WorkerCommandStatus_",
        "Hosting_WorkerStatus_",
    ];

    [Fact]
    public void ServerDetails_LiteralLocalizationKeys_ExistInEnglishAndGerman()
    {
        var literalKeys = ReadLiteralLocalizationKeys();
        var english = ReadResource(EnglishResourcePath);
        var german = ReadResource(GermanResourcePath);

        var missingEnglish = literalKeys.Except(english.Keys, StringComparer.Ordinal).ToArray();
        var missingGerman = literalKeys.Except(german.Keys, StringComparer.Ordinal).ToArray();

        Assert.True(
            missingEnglish.Length == 0,
            $"Missing English server-detail resources:{Environment.NewLine}{string.Join(Environment.NewLine, missingEnglish)}");
        Assert.True(
            missingGerman.Length == 0,
            $"Missing German server-detail resources:{Environment.NewLine}{string.Join(Environment.NewLine, missingGerman)}");
    }

    [Fact]
    public void ServerDetails_EnglishAndGermanResources_UseMatchingPlaceholderIndices()
    {
        var literalKeys = ReadLiteralLocalizationKeys();
        var english = ReadResource(EnglishResourcePath);
        var german = ReadResource(GermanResourcePath);
        var mismatches = new List<string>();

        foreach (var key in literalKeys.Where(key => english.ContainsKey(key) && german.ContainsKey(key)))
        {
            var englishIndices = ReadPlaceholderIndices(english[key]);
            var germanIndices = ReadPlaceholderIndices(german[key]);
            if (!englishIndices.SequenceEqual(germanIndices))
            {
                mismatches.Add(
                    $"{key}: English [{string.Join(", ", englishIndices)}], "
                    + $"German [{string.Join(", ", germanIndices)}]");
            }
        }

        Assert.True(mismatches.Count == 0, string.Join(Environment.NewLine, mismatches));
    }

    [Fact]
    public void ServerDetails_DynamicLocalizationResources_ExistInBothLanguages()
    {
        var english = ReadResource(EnglishResourcePath);
        var german = ReadResource(GermanResourcePath);
        var englishKeys = english.Keys
            .Where(IsDynamicLocalizationKey)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var germanKeys = german.Keys
            .Where(IsDynamicLocalizationKey)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(englishKeys);
        Assert.Equal(englishKeys, germanKeys);
        Assert.All(englishKeys, key => Assert.False(string.IsNullOrWhiteSpace(english[key]), key));
        Assert.All(germanKeys, key => Assert.False(string.IsNullOrWhiteSpace(german[key]), key));
    }

    private static bool IsDynamicLocalizationKey(string key)
        => DynamicLocalizationPrefixes.Any(prefix => key.StartsWith(prefix, StringComparison.Ordinal));

    private static IReadOnlyCollection<string> ReadLiteralLocalizationKeys()
    {
        var details = File.ReadAllText(Path.Combine(RepositoryRoot, DetailsPath));
        return LiteralLocalizerKeyRegex()
            .Matches(details)
            .Select(match => match.Groups["key"].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, string> ReadResource(string resourcePath)
        => XDocument.Load(Path.Combine(RepositoryRoot, resourcePath))
            .Descendants("data")
            .ToDictionary(
                element => element.Attribute("name")?.Value
                           ?? throw new InvalidOperationException($"Unnamed resource in {resourcePath}."),
                element => element.Element("value")?.Value ?? string.Empty,
                StringComparer.Ordinal);

    private static int[] ReadPlaceholderIndices(string value)
        => CompositeFormatPlaceholderRegex()
            .Matches(value)
            .Select(match => int.Parse(match.Groups["index"].Value))
            .Distinct()
            .Order()
            .ToArray();

    [GeneratedRegex("Localizer\\s*\\[\\s*\"(?<key>[^\"]+)\"", RegexOptions.CultureInvariant)]
    private static partial Regex LiteralLocalizerKeyRegex();

    [GeneratedRegex("(?<!\\{)\\{(?<index>\\d+)(?:,[^}:]+)?(?::[^}]+)?\\}(?!\\})", RegexOptions.CultureInvariant)]
    private static partial Regex CompositeFormatPlaceholderRegex();

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null
                   && !File.Exists(Path.Combine(directory.FullName, "X39.UnitedTacticalForces.slnx")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName
                   ?? throw new InvalidOperationException("Could not find repository root.");
        }
    }
}

using System.Globalization;
using System.Xml.Linq;
using Xunit;

namespace Api.Tests.WebApp;

public class LocalizationFormatBoundaryTests
{
    private static readonly object[] FormatArguments =
    [
        ..Enumerable.Range(0, 32).Select((index) => $"arg{index}"),
    ];

    [Theory]
    [InlineData("sources/WebApp/Properties/Language.resx")]
    [InlineData("sources/WebApp/Properties/Language.de.resx")]
    public void WebAppResourceValues_WithFormatBraces_AreValidCompositeFormatStrings(string resourcePath)
    {
        var failures = new List<string>();
        foreach (var dataElement in XDocument.Load(Path.Combine(RepositoryRoot, resourcePath)).Descendants("data"))
        {
            var name = dataElement.Attribute("name")?.Value ?? "<unnamed>";
            var value = dataElement.Element("value")?.Value;
            if (string.IsNullOrEmpty(value) || !value.Contains('{', StringComparison.Ordinal) && !value.Contains('}', StringComparison.Ordinal))
                continue;

            try
            {
                string.Format(CultureInfo.InvariantCulture, value, FormatArguments);
            }
            catch (FormatException ex)
            {
                failures.Add($"{resourcePath}:{name}: {ex.Message}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Theory]
    [InlineData("sources/WebApp/Properties/Language.de.resx")]
    public void WebAppResourceValues_DoNotContainUtf8Mojibake(string resourcePath)
    {
        var failures = new List<string>();
        foreach (var dataElement in XDocument.Load(Path.Combine(RepositoryRoot, resourcePath)).Descendants("data"))
        {
            var name = dataElement.Attribute("name")?.Value ?? "<unnamed>";
            var value = dataElement.Element("value")?.Value;
            if (string.IsNullOrEmpty(value) || !ContainsUtf8Mojibake(value))
                continue;

            failures.Add($"{resourcePath}:{name}: {value}");
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static bool ContainsUtf8Mojibake(string value)
    {
        return value.Contains('Ã', StringComparison.Ordinal)
               || value.Contains('Â', StringComparison.Ordinal)
               || value.Contains('â', StringComparison.Ordinal);
    }

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "X39.UnitedTacticalForces.slnx")))
                directory = directory.Parent;

            return directory?.FullName
                   ?? throw new InvalidOperationException("Could not find repository root.");
        }
    }
}

using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Api.Tests.WebApp;

public partial class CloudRuntimeProfileUiBoundaryTests
{
    private const string CreatePath = "sources/WebApp/Pages/Servers/Create.razor";
    private const string DetailsPath = "sources/WebApp/Pages/Servers/Details.razor";
    private const string EditorPath = "sources/WebApp/Components/RuntimeProfileEditor.razor";
    private const string SummaryPath = "sources/WebApp/Components/RuntimeProfileSummary.razor";
    private const string EnglishResourcePath = "sources/WebApp/Properties/Language.resx";
    private const string GermanResourcePath = "sources/WebApp/Properties/Language.de.resx";

    private static readonly string[] CloudProfileRazorPaths =
    [
        CreatePath,
        DetailsPath,
        EditorPath,
        SummaryPath,
    ];

    [Fact]
    public void Details_ProfileEditor_RemainsMountedRegardlessOfCatalogCountOrActiveDeployment()
    {
        var details = ReadRepositoryFile(DetailsPath);
        var markup = ReadSection(details, "<div class=\"hosting-page", "@code {");

        Assert.Equal(2, CountOccurrences(markup, "<RuntimeProfileEditor "));
        Assert.Equal(
            2,
            CountOccurrences(markup, "HasActiveDeployment=\"@(_server.Deployment is not null)\""));
        Assert.Equal(
            2,
            CountOccurrences(markup, "CanModify=\"@(_server.CanModify && !_server.IsArchived)\""));
        Assert.DoesNotContain("_availableProfiles.Count", markup, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "@if (_server.CanModify && _server.Deployment is null",
            markup,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Details_ProfileCatalogLoading_IsSeparateFromRuntimePolling()
    {
        var details = ReadRepositoryFile(DetailsPath);
        var runtimeRefresh = ReadSection(
            details,
            "private async Task RefreshRuntimeAsync(",
            "private async Task RefreshAllAsync()");
        var explicitRefresh = ReadSection(
            details,
            "private async Task RefreshAllAsync()",
            "private Task ReloadRuntimeProfilesAsync()");
        var profileCatalogLoad = ReadSection(
            details,
            "private async Task LoadRuntimeProfilesAsync(",
            "private void SynchronizeRuntimeProfileSelection()");
        var polling = ReadSection(
            details,
            "private async Task PollAsync(",
            "private void SelectWorkspaceSection(");

        Assert.DoesNotContain("GetCapabilitiesAsync", runtimeRefresh, StringComparison.Ordinal);
        Assert.DoesNotContain("LoadRuntimeProfilesAsync", runtimeRefresh, StringComparison.Ordinal);
        Assert.Contains("GetCapabilitiesAsync", profileCatalogLoad, StringComparison.Ordinal);

        Assert.Contains("RefreshRuntimeAsync", polling, StringComparison.Ordinal);
        Assert.DoesNotContain("LoadRuntimeProfilesAsync", polling, StringComparison.Ordinal);

        Assert.Contains("RefreshRuntimeAsync", explicitRefresh, StringComparison.Ordinal);
        Assert.Contains("LoadRuntimeProfilesAsync", explicitRefresh, StringComparison.Ordinal);
    }

    [Fact]
    public void ProfileEditor_ProvidesExplicitCatalogAvailabilityStates()
    {
        var editor = ReadRepositoryFile(EditorPath);

        Assert.Contains("@if (IsLoading)", editor, StringComparison.Ordinal);
        Assert.Contains("else if (!string.IsNullOrWhiteSpace(LoadError))", editor, StringComparison.Ordinal);
        Assert.Contains("else if (Provider is null)", editor, StringComparison.Ordinal);
        Assert.Contains("else if (!ProviderIsAvailable)", editor, StringComparison.Ordinal);
        Assert.Contains("@if (CanModify)", editor, StringComparison.Ordinal);
        Assert.Contains("provider.Profiles.Count > 0", editor, StringComparison.Ordinal);

        Assert.Contains("\"available\"", editor, StringComparison.Ordinal);
        Assert.Contains("\"configuration-required\"", editor, StringComparison.Ordinal);
        Assert.Contains("\"catalog-unavailable\"", editor, StringComparison.Ordinal);
        Assert.Contains("Hosting_ProfileCatalog_ConfigurationRequiredTitle", editor, StringComparison.Ordinal);
        Assert.Contains("Hosting_ProfileCatalog_UnavailableTitle", editor, StringComparison.Ordinal);
        Assert.Contains("Hosting_ProfileCatalog_EmptyTitle", editor, StringComparison.Ordinal);
        Assert.Contains("Hosting_ProfileCatalog_EmptyDescription", editor, StringComparison.Ordinal);
        Assert.Contains("Hosting_ProfileCatalog_MissingTitle", editor, StringComparison.Ordinal);
        Assert.Contains("Hosting_ProfileCatalog_ReadOnlyTitle", editor, StringComparison.Ordinal);
        Assert.Contains("Hosting_ProfileCatalog_ReadOnlyDescription", editor, StringComparison.Ordinal);
    }

    [Fact]
    public void ProfileSummary_PreviewsComputeStorageAndHourlyAndMonthlyCosts()
    {
        var summary = ReadRepositoryFile(SummaryPath);

        AssertProfileMetric(summary, "Hosting_Profile_VirtualCpu", "Profile.VirtualCpuCount");
        AssertProfileMetric(summary, "Hosting_Profile_Memory", "Profile.MemoryGiB");
        AssertProfileMetric(summary, "Hosting_Profile_Disk", "Profile.DiskGiB");
        AssertProfileMetric(summary, "Hosting_Profile_HourlyEstimate", "Profile.EstimatedHourlyCostEur");
        AssertProfileMetric(summary, "Hosting_Profile_MonthlyEstimate", "Profile.EstimatedMonthlyCostEur");
        Assert.Contains("Hosting_Profile_NotReported", summary, StringComparison.Ordinal);

        var editor = ReadRepositoryFile(EditorPath);
        var create = ReadRepositoryFile(CreatePath);
        Assert.Contains("<RuntimeProfileSummary Profile=\"selectedProfile\"/>", editor, StringComparison.Ordinal);
        Assert.Contains("<RuntimeProfileSummary Profile=\"selectedProfile\"/>", create, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_SurfacesUnavailableProvidersAndOnlySelectsAvailableProfiles()
    {
        var create = ReadRepositoryFile(CreatePath);
        var availableProviders = ReadSection(
            create,
            "private IReadOnlyList<RuntimeProviderInfo> AvailableProviders",
            "private IReadOnlyList<RuntimeProviderInfo> UnavailableProviders");
        var unavailableProviders = ReadSection(
            create,
            "private IReadOnlyList<RuntimeProviderInfo> UnavailableProviders",
            "private RuntimeProviderInfo? SelectedProvider");
        var submitGuard = ReadSection(
            create,
            "private bool CanSubmit",
            "protected override async Task OnInitializedAsync()");
        var initialSelection = ReadSection(
            create,
            "private void SelectFirstAvailableProvider()",
            "private async Task CreateServerAsync()");

        Assert.Contains("@foreach (var provider in AvailableProviders)", create, StringComparison.Ordinal);
        Assert.Contains("@foreach (var profile in selectedProvider.Profiles)", create, StringComparison.Ordinal);
        Assert.Contains("@if (UnavailableProviders.Count > 0)", create, StringComparison.Ordinal);
        Assert.Contains("@foreach (var provider in UnavailableProviders)", create, StringComparison.Ordinal);
        Assert.Contains("Hosting_Create_ProviderAvailability", create, StringComparison.Ordinal);
        Assert.Contains("Hosting_ProfileCatalog_NoSelectableProvider", create, StringComparison.Ordinal);
        Assert.Contains("ProviderUnavailableDescription(provider)", create, StringComparison.Ordinal);
        Assert.Contains("Hosting_ProfileCatalog_ConfigurationRequiredDescription", create, StringComparison.Ordinal);
        Assert.Contains("Hosting_ProfileCatalog_UnavailableDescription", create, StringComparison.Ordinal);
        Assert.Contains("Hosting_ProfileCatalog_EmptyDescription", create, StringComparison.Ordinal);

        Assert.Contains(".Where(IsProviderAvailable)", availableProviders, StringComparison.Ordinal);
        Assert.Contains("!IsProviderAvailable(provider)", unavailableProviders, StringComparison.Ordinal);
        Assert.Contains("IsProviderAvailable(SelectedProvider)", submitGuard, StringComparison.Ordinal);
        Assert.Contains("AvailableProviders.FirstOrDefault()", initialSelection, StringComparison.Ordinal);
    }

    [Fact]
    public void CloudProfileUi_LiteralLocalizationKeysExistAndHaveMatchingPlaceholders()
    {
        var keys = CloudProfileRazorPaths
            .SelectMany(ReadLiteralLocalizationKeys)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var english = ReadResource(EnglishResourcePath);
        var german = ReadResource(GermanResourcePath);

        var missingEnglish = keys.Except(english.Keys, StringComparer.Ordinal).ToArray();
        var missingGerman = keys.Except(german.Keys, StringComparer.Ordinal).ToArray();
        var placeholderMismatches = keys
            .Where(key => english.ContainsKey(key) && german.ContainsKey(key))
            .Where(key => !ReadPlaceholderIndices(english[key]).SequenceEqual(ReadPlaceholderIndices(german[key])))
            .Select(key =>
                $"{key}: English [{string.Join(", ", ReadPlaceholderIndices(english[key]))}], "
                + $"German [{string.Join(", ", ReadPlaceholderIndices(german[key]))}]")
            .ToArray();

        Assert.NotEmpty(keys);
        Assert.True(
            missingEnglish.Length == 0,
            "Missing English cloud-profile UI resources:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, missingEnglish));
        Assert.True(
            missingGerman.Length == 0,
            "Missing German cloud-profile UI resources:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, missingGerman));
        Assert.True(placeholderMismatches.Length == 0, string.Join(Environment.NewLine, placeholderMismatches));
        Assert.All(keys, key => Assert.False(string.IsNullOrWhiteSpace(english[key]), $"English: {key}"));
        Assert.All(keys, key => Assert.False(string.IsNullOrWhiteSpace(german[key]), $"German: {key}"));
    }

    private static void AssertProfileMetric(string summary, string localizationKey, string profileProperty)
    {
        Assert.Contains(localizationKey, summary, StringComparison.Ordinal);
        Assert.Contains(profileProperty, summary, StringComparison.Ordinal);
    }

    private static IReadOnlyCollection<string> ReadLiteralLocalizationKeys(string razorPath)
    {
        var razor = ReadRepositoryFile(razorPath);
        return LiteralLocalizerKeyRegex()
            .Matches(razor)
            .Select(match => match.Groups["key"].Value)
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

    private static string ReadSection(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find start marker '{startMarker}'.");
        var end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(end >= 0, $"Could not find end marker '{endMarker}'.");
        return source[start..end];
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var searchIndex = 0;
        while ((searchIndex = source.IndexOf(value, searchIndex, StringComparison.Ordinal)) >= 0)
        {
            count++;
            searchIndex += value.Length;
        }

        return count;
    }

    private static string ReadRepositoryFile(string repositoryRelativePath)
        => File.ReadAllText(Path.Combine(RepositoryRoot, repositoryRelativePath));

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

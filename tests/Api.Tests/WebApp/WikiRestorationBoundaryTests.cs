using Xunit;

namespace Api.Tests.WebApp;

public class WikiRestorationBoundaryTests
{
    [Fact]
    public void CommunityWikiPage_UsesCommunityShellAndFluentRenderer()
    {
        const string file = "sources/WebApp/Pages/Wiki/Index.razor";
        var source = ReadRepositoryFile(file);
        var failures = new List<string>();

        foreach (var marker in new[]
                 {
                     "@page \"/wiki\"",
                     "@page \"/wiki/{PageId:guid}\"",
                     "@layout CommunityLayout",
                     "FluentMarkdown",
                     "WikiTreeNode",
                     "wiki-mobile-tabs",
                     "WikiMobileView.Pages",
                     "WikiMobileView.Article",
                     "WikiMobileView.History",
                 })
        {
            AssertContains(source, marker, file, "community wiki surface", failures);
        }

        AssertDoesNotContain(source, "Mud", file, "community wiki surface", failures);
        AssertNoFailures(failures);
    }

    [Fact]
    public void CommunityNavigation_IncludesTopLevelWikiLink()
    {
        const string file = "sources/WebApp/Shared/CommunityLayout.razor";
        var source = ReadRepositoryFile(file);
        var failures = new List<string>();

        AssertContains(source, "href=\"@BaseUrl.ResolveSelfUrl(\"wiki\")\"", file, "desktop wiki nav", failures);
        AssertContains(source, "Wiki", file, "wiki nav label", failures);

        var desktopWikiIndex = source.IndexOf("ResolveSelfUrl(\"wiki\")", StringComparison.Ordinal);
        var desktopReportsIndex = source.IndexOf("ResolveSelfUrl(\"field-reports\")", StringComparison.Ordinal);
        if (desktopWikiIndex < 0 || desktopReportsIndex < 0 || desktopWikiIndex > desktopReportsIndex)
            failures.Add($"{file}: Wiki should be a top-level Community link before Field Reports.");

        AssertNoFailures(failures);
    }

    [Fact]
    public void WikiRepository_ExposesTypedClientBoundary()
    {
        const string interfaceFile = "sources/WebApp/Services/WikiRepository/IWikiRepository.cs";
        const string repositoryFile = "sources/WebApp/Services/WikiRepository/WikiRepositoryImpl.cs";
        var interfaceSource = ReadRepositoryFile(interfaceFile);
        var repositorySource = ReadRepositoryFile(repositoryFile);
        var failures = new List<string>();

        foreach (var marker in new[]
                 {
                     "GetWikiPageHeadersAsync",
                     "showDeleted",
                     "GetWikiPageRevisionHeadersAsync",
                     "GetWikiPageRevisionAsync",
                     "CreatePageAsync",
                     "CreateRevisionAsync",
                     "RenameAsync",
                     "DeleteWikiPageAsync",
                     "RestoreWikiPageAsync",
                 })
        {
            AssertContains(interfaceSource, marker, interfaceFile, "wiki repository contract", failures);
            AssertContains(repositorySource, marker, repositoryFile, "wiki repository implementation", failures);
        }

        AssertContains(repositorySource, "Client.Wiki", repositoryFile, "generated wiki client boundary", failures);
        AssertDoesNotContain(repositorySource, "GetFromJsonAsync", repositoryFile, "generated wiki client boundary", failures);
        AssertDoesNotContain(repositorySource, "PostAsJsonAsync", repositoryFile, "generated wiki client boundary", failures);
        AssertNoFailures(failures);
    }

    [Fact]
    public void WikiMutations_RemainClaimGated()
    {
        const string pageFile = "sources/WebApp/Pages/Wiki/Index.razor";
        const string controllerFile = "sources/Api/Controllers/WikiController.cs";
        var pageSource = ReadRepositoryFile(pageFile);
        var controllerSource = ReadRepositoryFile(controllerFile);
        var failures = new List<string>();

        foreach (var marker in new[]
                 {
                     "Claims.Creation.Wiki",
                     "Claims.Wiki.Modify",
                     "Claims.Wiki.Rename",
                     "Claims.Wiki.Delete",
                     "HasClaimOrWikiAdmin()",
                 })
        {
            AssertContains(pageSource, marker, pageFile, "wiki UI claim gates", failures);
        }

        foreach (var marker in new[]
                 {
                     "[Authorize(Claims.Creation.Wiki)]",
                     "[Authorize(Claims.Wiki.Modify)]",
                     "[Authorize(Claims.Wiki.Rename)]",
                     "[Authorize(Claims.Wiki.Delete)]",
                     "[Authorize(Claims.Administrative.Wiki)]",
                 })
        {
            AssertContains(controllerSource, marker, controllerFile, "wiki API claim gates", failures);
        }

        AssertNoFailures(failures);
    }

    private static string ReadRepositoryFile(string repositoryRelativePath)
        => File.ReadAllText(Path.Combine(RepositoryRoot, repositoryRelativePath));

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

    private static void AssertContains(
        string source,
        string marker,
        string file,
        string memberName,
        ICollection<string> failures)
    {
        if (!source.Contains(marker, StringComparison.Ordinal))
            failures.Add($"{file}: member '{memberName}' does not contain '{marker}'.");
    }

    private static void AssertDoesNotContain(
        string source,
        string marker,
        string file,
        string memberName,
        ICollection<string> failures)
    {
        if (source.Contains(marker, StringComparison.Ordinal))
            failures.Add($"{file}: member '{memberName}' should not contain '{marker}'.");
    }

    private static void AssertNoFailures(IEnumerable<string> failures)
    {
        var failureList = failures.ToArray();
        Assert.True(failureList.Length == 0, string.Join(Environment.NewLine, failureList));
    }
}

using Xunit;

namespace Api.Tests.WebApp;

public class GameServerNavigationBoundaryTests
{
    [Fact]
    public void GameServerNavigation_RequiresHostingOrServerAccess()
    {
        var meService = ReadRepositoryFile("sources/WebApp/Services/MeService.cs");
        var communityLayout = ReadRepositoryFile("sources/WebApp/Shared/CommunityLayout.razor");
        var commandLayout = ReadRepositoryFile("sources/WebApp/Shared/CommandCenterLayout.razor");
        var commandIndex = ReadRepositoryFile("sources/WebApp/Pages/Command/Index.razor");

        Assert.Contains("public bool CanAccessGameServers()", meService, StringComparison.Ordinal);
        Assert.Contains("Claims.ResourceBased.Server.View", meService, StringComparison.Ordinal);
        Assert.DoesNotContain("ResolveSelfUrl(\"servers\")", communityLayout, StringComparison.Ordinal);
        Assert.Contains("|| Me.CanAccessGameServers()", communityLayout, StringComparison.Ordinal);
        Assert.Contains("@if (Me.CanAccessGameServers())", commandLayout, StringComparison.Ordinal);
        Assert.Contains("ResolveSelfUrl(\"command/servers\")", commandLayout, StringComparison.Ordinal);
        Assert.Contains("@if (Me.CanAccessGameServers())", commandIndex, StringComparison.Ordinal);
    }

    [Fact]
    public void GameServerOverview_DescribesAccessibleServersWithoutCreationBoundaryNote()
    {
        var serverIndex = ReadRepositoryFile("sources/WebApp/Pages/Servers/Index.razor");

        Assert.DoesNotContain("Hosting_Servers_CreateStartTitle", serverIndex, StringComparison.Ordinal);
        Assert.DoesNotContain("Hosting_Servers_CreateStartDescription", serverIndex, StringComparison.Ordinal);
        Assert.Contains("Hosting_Servers_EmptyAccessDescription", serverIndex, StringComparison.Ordinal);
        Assert.Contains("@page \"/command/servers\"", serverIndex, StringComparison.Ordinal);
        Assert.Contains("@layout CommandCenterLayout", serverIndex, StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(string repositoryRelativePath)
        => File.ReadAllText(Path.Combine(RepositoryRoot, repositoryRelativePath));

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null
                   && !File.Exists(Path.Combine(directory.FullName, "X39.UnitedTacticalForces.slnx")))
                directory = directory.Parent;

            return directory?.FullName
                   ?? throw new InvalidOperationException("Could not find repository root.");
        }
    }
}

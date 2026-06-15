using Xunit;

namespace Api.Tests.WebApp;

public class AuthStateBoundaryTests
{
    [Fact]
    public void SteamAuthProvider_NotifiesWhenMeServiceCurrentUserChanges()
    {
        var meService = ReadRepositoryFile("sources/WebApp/Services/MeService.cs");
        var steamAuthProvider = ReadRepositoryFile("sources/WebApp/Services/SteamAuthProvider.cs");
        var failures = new List<string>();

        AssertContains(meService, "public event EventHandler? CurrentUserChanged", "MeService", failures);
        AssertContains(meService, "NotifyCurrentUserChanged();", "MeService", failures);
        AssertContains(meService, "CurrentUserChanged?.Invoke(this, EventArgs.Empty)", "MeService", failures);
        AssertContains(steamAuthProvider, "CurrentUserChanged += OnCurrentUserChanged", "SteamAuthProvider", failures);
        AssertContains(steamAuthProvider, "NotifyAuthenticationStateChanged(GetAuthenticationStateAsync())", "SteamAuthProvider", failures);
        AssertContains(steamAuthProvider, "CurrentUserChanged -= OnCurrentUserChanged", "SteamAuthProvider", failures);

        AssertNoFailures(failures);
    }

    private static string ReadRepositoryFile(string repositoryRelativePath)
        => File.ReadAllText(Path.Combine(RepositoryRoot, repositoryRelativePath));

    private static void AssertContains(
        string source,
        string marker,
        string file,
        ICollection<string> failures)
    {
        if (!source.Contains(marker, StringComparison.Ordinal))
            failures.Add($"{file}: expected to contain '{marker}'.");
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

    private static void AssertNoFailures(IEnumerable<string> failures)
    {
        var failureList = failures.ToArray();
        Assert.True(failureList.Length == 0, string.Join(Environment.NewLine, failureList));
    }
}

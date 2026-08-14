using Xunit;

namespace Api.Tests.WebApp;

public class MemberManagementBoundaryTests
{
    [Fact]
    public void ClassicMemberDetail_ProtectsAccessRevokingChangesWithTypedChallenges()
    {
        var source = ReadRepoFile("sources/WebApp/Pages/Command/Members/Details.razor");

        Assert.Contains("@page \"/command/members/{UserId:guid}\"", source, StringComparison.Ordinal);
        Assert.Contains("ConfirmProtectedChangeAsync", source, StringComparison.Ordinal);
        Assert.Contains("Pages_Command_Member_ConfirmBanTitle", source, StringComparison.Ordinal);
        Assert.Contains("Pages_Command_Member_ConfirmEndMembershipTitle", source, StringComparison.Ordinal);
        Assert.Contains("ShowUtfChallengeConfirmationAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ReactMemberDetail_UsesDomainServicesForEveryClassicWorkflow()
    {
        var page = ReadRepoFile("sources/ReactApp/src/features/admin/MemberDetailPage.tsx");
        var service = ReadRepoFile("sources/ReactApp/src/features/admin/members.ts");

        Assert.Contains("updateMemberProfile", page, StringComparison.Ordinal);
        Assert.Contains("upsertMembership", page, StringComparison.Ordinal);
        Assert.Contains("setRoleMember", page, StringComparison.Ordinal);
        Assert.Contains("setMemberClaim", page, StringComparison.Ordinal);
        Assert.Contains("useUnsavedChanges", page, StringComparison.Ordinal);
        Assert.Contains("challenge={displayName}", page, StringComparison.Ordinal);
        Assert.Contains("/command-center/community-snapshot", service, StringComparison.Ordinal);
        Assert.Contains("/users/{userId}/claims/{claimId}/to/{mode}", service, StringComparison.Ordinal);
        Assert.Contains("/verein/memberships/{userId}", service, StringComparison.Ordinal);
    }

    [Fact]
    public void MemberChallengeLocalization_IsSharedByEnglishAndGermanClients()
    {
        var english = ReadRepoFile("sources/WebApp/Properties/Language.resx");
        var german = ReadRepoFile("sources/WebApp/Properties/Language.de.resx");
        foreach (var key in new[]
                 {
                     "Pages_Command_Member_ConfirmBanTitle",
                     "Pages_Command_Member_ConfirmBanDescription",
                     "Pages_Command_Member_ConfirmBanAction",
                     "Pages_Command_Member_ConfirmEndMembershipTitle",
                     "Pages_Command_Member_ConfirmEndMembershipDescription",
                     "Pages_Command_Member_ConfirmEndMembershipAction",
                     "Pages_Command_Member_ConfirmChallenge",
                     "Pages_Command_Member_ConfirmKicker",
                 })
        {
            Assert.Contains($"name=\"{key}\"", english, StringComparison.Ordinal);
            Assert.Contains($"name=\"{key}\"", german, StringComparison.Ordinal);
        }
    }

    private static string ReadRepoFile(string relativePath)
        => File.ReadAllText(Path.Combine(FindRepoRoot(), relativePath));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "X39.UnitedTacticalForces.slnx")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("Failed to find repository root.");
    }
}

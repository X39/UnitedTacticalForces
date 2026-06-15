using Xunit;

namespace Api.Tests.WebApp;

public class RoleManagementBoundaryTests
{
    [Fact]
    public void RolesOverview_UsesSingleTableWithDedicatedDetailLinks()
    {
        var source = ReadRepoFile("sources/WebApp/Pages/Command/Roles/Index.razor");

        Assert.Contains("@page \"/command/roles\"", source, StringComparison.Ordinal);
        Assert.Contains("class=\"utf-command-table authority-table\"", source, StringComparison.Ordinal);
        Assert.Contains("$\"command/roles/{roleId}\"", source, StringComparison.Ordinal);
        Assert.Contains("Pages_Command_Roles_OpenDetails", source, StringComparison.Ordinal);
        Assert.DoesNotContain("authority-master-detail", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectRoleAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Localizer[\"Create role\"]", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RoleDetailRoute_OwnsRoleEditingMembersAndClaims()
    {
        var source = ReadRepoFile("sources/WebApp/Pages/Command/Roles/Details.razor");

        Assert.Contains("@page \"/command/roles/{RoleId:long}\"", source, StringComparison.Ordinal);
        Assert.Contains("RoleRepository.SetRoleClaimAsync", source, StringComparison.Ordinal);
        Assert.Contains("RoleRepository.SetUserRoleAsync", source, StringComparison.Ordinal);
        Assert.Contains("RoleRepository.UpdateRoleAsync", source, StringComparison.Ordinal);
        Assert.Contains("RoleRepository.GetRoleMembersAsync", source, StringComparison.Ordinal);
        Assert.Contains("AssignedClaims", source, StringComparison.Ordinal);
        Assert.Contains("AvailableClaims", source, StringComparison.Ordinal);
        Assert.Contains("role-members-panel", source, StringComparison.Ordinal);
        Assert.Contains("UtfCommandSearch Placeholder=\"@(Localizer[SearchMembersKey])\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Localizer[\"Assigned members\"]", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Localizer[\"Available members\"]", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RoleRepository_ExposesRoleClaimMutationEndpoint()
    {
        var contract = ReadRepoFile("sources/WebApp/Services/RoleRepository/IRoleRepository.cs");
        var implementation = ReadRepoFile("sources/WebApp/Services/RoleRepository/RoleRepositoryImpl.cs");

        Assert.Contains("Task SetRoleClaimAsync(", contract, StringComparison.Ordinal);
        Assert.Contains("roles/set/role/{roleId}/claim/{claimId}/to/{claimActive.ToString().ToLowerInvariant()}", implementation, StringComparison.Ordinal);
    }

    [Fact]
    public void RoleManagement_LocalizationCoversOverviewAndDetailStrings()
    {
        var english = ReadRepoFile("sources/WebApp/Properties/Language.resx");
        var german = ReadRepoFile("sources/WebApp/Properties/Language.de.resx");
        var keys = new[]
        {
            "Pages_Command_Roles_CreateRole",
            "Pages_Command_Roles_OpenDetails",
            "Pages_Command_Roles_DetailPageTitle",
            "Pages_Command_Roles_AssignedMembers",
            "Pages_Command_Roles_AvailableMembers",
            "Pages_Command_Roles_AssignedClaims",
            "Pages_Command_Roles_AvailableClaims",
            "Pages_Command_Roles_ClaimsSaved",
            "Pages_Command_Roles_SaveError",
        };

        foreach (var key in keys)
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

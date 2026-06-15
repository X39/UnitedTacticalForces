using Xunit;

namespace Api.Tests.WebApp;

public class VereinMembershipStatusBoundaryTests
{
    [Fact]
    public void MeService_UsesGeneratedMembershipFieldsForVereinStatus()
    {
        var meService = ReadRepositoryFile("sources/WebApp/Services/MeService.cs");

        Assert.Contains("User.IsVereinMember == true", meService, StringComparison.Ordinal);
        Assert.Contains("(string?) User.VereinMembershipStatus", meService, StringComparison.Ordinal);
        Assert.Contains("(string?) User.VereinMemberNumber", meService, StringComparison.Ordinal);
        Assert.Contains("GetAdditionalBool(nameof(IsVereinMember))", meService, StringComparison.Ordinal);
        Assert.Contains("GetAdditionalString(\"vereinMembershipStatus\")", meService, StringComparison.Ordinal);
        Assert.Contains("GetAdditionalString(\"vereinMemberNumber\")", meService, StringComparison.Ordinal);
        Assert.Contains("public bool HasResourceClaim(string claim, string value)", meService, StringComparison.Ordinal);
        Assert.Contains("if (!IsAuthenticated)", meService, StringComparison.Ordinal);
        Assert.Contains("return false;", meService, StringComparison.Ordinal);
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
}

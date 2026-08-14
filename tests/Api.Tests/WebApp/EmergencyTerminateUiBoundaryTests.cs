using Xunit;

namespace Api.Tests.WebApp;

public sealed class EmergencyTerminateUiBoundaryTests
{
    [Fact]
    public void NotificationTerminate_DeepLinksToTheTypedChallenge()
    {
        var profileMenu = ReadRepositoryFile("sources/WebApp/Components/UtfProfileMenu.razor");
        var details = ReadRepositoryFile("sources/WebApp/Pages/Servers/Details.razor");
        var challengeDialog = ReadRepositoryFile("sources/WebApp/Components/UtfChallengeConfirmationDialog.razor");

        Assert.Contains("?action={Uri.EscapeDataString(action.Id)}", profileMenu, StringComparison.Ordinal);
        Assert.Contains("[SupplyParameterFromQuery(Name = \"action\")]", details, StringComparison.Ordinal);
        Assert.Contains("await RequestActionAsync(requestedAction);", details, StringComparison.Ordinal);
        Assert.Contains("_confirmationInputElement.FocusAsync()", challengeDialog, StringComparison.Ordinal);
    }

    [Fact]
    public void FailedTerminateSubmission_KeepsTheChallengeOpen()
    {
        var details = ReadRepositoryFile("sources/WebApp/Pages/Servers/Details.razor");
        var challengeDialog = ReadRepositoryFile("sources/WebApp/Components/UtfChallengeConfirmationDialog.razor");

        Assert.Contains("private async Task<bool> ExecuteActionAsync", details, StringComparison.Ordinal);
        Assert.Contains(
            "onConfirmAsync: confirmation => ExecuteActionAsync(action, confirmation)",
            details,
            StringComparison.Ordinal);
        Assert.Contains("&& !await Content.OnConfirmAsync(Content.Confirmation)", challengeDialog, StringComparison.Ordinal);
        Assert.Contains("return false;", details, StringComparison.Ordinal);
    }

    [Fact]
    public void EmergencyTerminate_IsExplicitlyAvailableFromServerListAndDetailHeader()
    {
        var index = ReadRepositoryFile("sources/WebApp/Pages/Servers/Index.razor");
        var details = ReadRepositoryFile("sources/WebApp/Pages/Servers/Details.razor");

        Assert.Contains("GetTerminateAction(server)", index, StringComparison.Ordinal);
        Assert.Contains("?action={Uri.EscapeDataString(action.Id)}", index, StringComparison.Ordinal);
        Assert.Contains("EmergencyTerminateAction is { } terminateAction", details, StringComparison.Ordinal);
        Assert.Contains("OnClick=\"@(() => RequestActionAsync(terminateAction))\"", details, StringComparison.Ordinal);
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
            {
                directory = directory.Parent;
            }

            return directory?.FullName
                   ?? throw new InvalidOperationException("Could not find repository root.");
        }
    }
}

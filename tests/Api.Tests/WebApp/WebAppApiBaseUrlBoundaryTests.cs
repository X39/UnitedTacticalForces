using System.Text.Json;
using Xunit;

namespace Api.Tests.WebApp;

public class WebAppApiBaseUrlBoundaryTests
{
    [Fact]
    public void ProductionConfig_UsesSameOriginApiProxy()
    {
        var source = ReadRepoFile("sources/WebApp/wwwroot/appsettings.Production.json");
        using var document = JsonDocument.Parse(source);

        Assert.Equal(
            "/api/",
            document.RootElement
                .GetProperty("Api")
                .GetProperty("BaseUrl")
                .GetString()
        );
        Assert.False(document.RootElement.TryGetProperty("Features", out _));
    }

    [Fact]
    public void BaseUrl_NormalizesAbsoluteAndRelativeApiBases()
    {
        var source = ReadRepoFile("sources/WebApp/Services/BaseUrl.cs");

        Assert.Contains("public Uri ApiUri => ResolveBaseUri(ApiUrl, SelfUri);", source, StringComparison.Ordinal);
        Assert.Contains("public Uri SelfUri => ResolveBaseUri(SelfUrl, fallbackBaseUri: null);", source, StringComparison.Ordinal);
        Assert.Contains("Uri.TryCreate(trimmedUrl, UriKind.Absolute", source, StringComparison.Ordinal);
        Assert.Contains("new Uri(fallbackBaseUri, trimmedUrl)", source, StringComparison.Ordinal);
        Assert.Contains("EnsureTrailingSlash", source, StringComparison.Ordinal);
        Assert.Contains("ResolveApiUrlWithReturnUrl", source, StringComparison.Ordinal);
        Assert.Contains("Uri.EscapeDataString(ResolveSelfUrl(returnPath))", source, StringComparison.Ordinal);
    }

    [Fact]
    public void UsersController_AllowsConfiguredClientReturnUrls()
    {
        var source = ReadRepoFile("sources/Api/Controllers/UsersController.cs");

        Assert.Contains("Url.IsLocalUrl(returnUrl)", source, StringComparison.Ordinal);
        Assert.Contains("IsConfiguredClientReturnUrl(returnUrl)", source, StringComparison.Ordinal);
        Assert.Contains("_baseUrl.ClientUri", source, StringComparison.Ordinal);
        Assert.Contains("Uri.TryCreate(returnUrl, UriKind.Absolute", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RepositoryBase_UsesNormalizedApiBaseForKiota()
    {
        var source = ReadRepoFile("sources/WebApp/Services/RepositoryBase.cs");

        Assert.Contains("requestAdapter.BaseUrl = baseUrl.ApiUri.ToString();", source, StringComparison.Ordinal);
        Assert.DoesNotContain("requestAdapter.BaseUrl = baseUrl.ApiUrl;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void LogoutUi_SubmitsPostInsteadOfNavigatingWithGet()
    {
        var profileMenu = ReadRepoFile("sources/WebApp/Components/UtfProfileMenu.razor");
        var authenticationAction = ReadRepoFile("sources/WebApp/Pages/Authentication/_Action/Index.razor");

        Assert.Contains("method=\"post\"", profileMenu, StringComparison.Ordinal);
        Assert.Contains("action=\"@BaseUrl.ResolveApiUrlWithReturnUrl", profileMenu, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"@BaseUrl.ResolveApiUrlWithReturnUrl(\"/users/logout\"", profileMenu, StringComparison.Ordinal);

        Assert.Contains("method=\"post\" action=\"@LogoutUrl\"", authenticationAction, StringComparison.Ordinal);
        Assert.Contains("utfForms.submit", authenticationAction, StringComparison.Ordinal);
        Assert.DoesNotContain("Navigation.NavigateTo(redirectUrl", authenticationAction, StringComparison.Ordinal);
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

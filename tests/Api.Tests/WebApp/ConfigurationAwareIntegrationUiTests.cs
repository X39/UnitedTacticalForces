using Xunit;

namespace Api.Tests.WebApp;

public class ConfigurationAwareIntegrationUiTests
{
    [Fact]
    public void LoginSurfaces_UseSharedProviderAvailabilityAndUnavailableState()
    {
        var providerOptions = ReadRepositoryFile("sources/WebApp/Components/AuthenticationProviderOptions.razor");
        var communityLayout = ReadRepositoryFile("sources/WebApp/Shared/CommunityLayout.razor");
        var joinPage = ReadRepositoryFile("sources/WebApp/Pages/Join/Index.razor");
        var redirect = ReadRepositoryFile("sources/WebApp/Shared/RedirectToLogin.razor");

        Assert.Contains("AuthenticationProviders.Steam", providerOptions, StringComparison.Ordinal);
        Assert.Contains("AuthenticationProviders.Discord", providerOptions, StringComparison.Ordinal);
        Assert.Contains("Authentication_NoMethodsAvailable", providerOptions, StringComparison.Ordinal);
        Assert.Contains("AuthenticationProviderOptions", communityLayout, StringComparison.Ordinal);
        Assert.Contains("AuthenticationProviderOptions", joinPage, StringComparison.Ordinal);
        Assert.Contains("authentication/login?returnUrl=", redirect, StringComparison.Ordinal);
        Assert.DoesNotContain("/users/login/steam", redirect, StringComparison.Ordinal);
    }

    [Fact]
    public void CommunityChannels_UseValidatedLinksAndConnectionActions()
    {
        var homePage = ReadRepositoryFile("sources/WebApp/Pages/Index.razor");
        var communityLayout = ReadRepositoryFile("sources/WebApp/Shared/CommunityLayout.razor");
        var configuration = ReadRepositoryFile("sources/WebApp/Services/CommunityConnectionConfiguration.cs");

        Assert.Contains("CommunityConnections.DiscordUrl", homePage, StringComparison.Ordinal);
        Assert.Contains("CommunityConnections.TeamSpeak3Url", homePage, StringComparison.Ordinal);
        Assert.Contains("Pages_Public_Home_ChannelJoin", homePage, StringComparison.Ordinal);
        Assert.Contains("Pages_Public_Home_ChannelConnect", homePage, StringComparison.Ordinal);
        Assert.DoesNotContain("ChannelOnline", homePage, StringComparison.Ordinal);
        Assert.Contains("Icons.Custom.Size24.Discord", homePage, StringComparison.Ordinal);
        Assert.Contains("Icons.Custom.Size24.Discord", communityLayout, StringComparison.Ordinal);
        Assert.Contains("UriKind.Absolute", configuration, StringComparison.Ordinal);
        Assert.Contains("ts3server", configuration, StringComparison.Ordinal);
    }

    [Fact]
    public void ProviderActions_UseOfficialBrandIconTypes()
    {
        var providerOptions = ReadRepositoryFile("sources/WebApp/Components/AuthenticationProviderOptions.razor");
        var brandIcons = ReadRepositoryFile("sources/WebApp/Constants.Icons.Brands.cs");

        Assert.Contains("Icons.Custom.Size24.Steam", providerOptions, StringComparison.Ordinal);
        Assert.Contains("Icons.Custom.Size24.Discord", providerOptions, StringComparison.Ordinal);
        Assert.Contains("partner.steamgames.com/doc/marketing/branding", brandIcons, StringComparison.Ordinal);
        Assert.Contains("discord.com/branding", brandIcons, StringComparison.Ordinal);
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

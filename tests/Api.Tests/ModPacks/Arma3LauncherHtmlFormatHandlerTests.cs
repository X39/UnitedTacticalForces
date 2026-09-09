using System.Text;
using X39.UnitedTacticalForces.Api.Helpers;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.ModPacks;

public sealed class Arma3LauncherHtmlFormatHandlerTests
{
    [Fact]
    public async Task ImportAsync_DeduplicatesWorkshopEntries()
    {
        Arma3ModPackParser.TitleLinkPair[] mods = [
            new("CBA", "https://steamcommunity.com/sharedfiles/filedetails/?id=450814997"),
            new("CBA duplicate", "https://steamcommunity.com/sharedfiles/filedetails/?id=450814997"),
        ];
        var html = Arma3ModPackParser.ToHtml("Operations", mods, Array.Empty<Arma3ModPackParser.TitleLinkPair>());
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(html));

        var result = await new Arma3LauncherHtmlFormatHandler().ImportAsync(stream);

        Assert.Equal("Operations", result.Title);
        Assert.Single(result.Entries);
    }

    [Fact]
    public async Task ExportAsync_ExcludesUnsupportedAndServerOnlyEntries()
    {
        var entries = new[]
        {
            new ModPackFormatExportEntry("CBA", ModSourceProviderIdentifiers.SteamWorkshop, "450814997", EModUsageRole.Shared),
            new ModPackFormatExportEntry("Private", ModSourceProviderIdentifiers.Git, "https://example.test/mod.git", EModUsageRole.Shared),
            new ModPackFormatExportEntry("Server", ModSourceProviderIdentifiers.SteamWorkshop, "123", EModUsageRole.ServerOnly),
        };

        var result = await new Arma3LauncherHtmlFormatHandler().ExportAsync("Operations", entries);
        var html = Encoding.UTF8.GetString(result.Content);

        Assert.Contains("450814997", html, StringComparison.Ordinal);
        Assert.DoesNotContain("example.test", html, StringComparison.Ordinal);
        Assert.DoesNotContain("?id=123", html, StringComparison.Ordinal);
        Assert.Equal(2, result.Warnings.Count);
    }
}

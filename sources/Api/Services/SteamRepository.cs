using Steam.Models.SteamCommunity;
using SteamWebAPI2.Interfaces;
using SteamWebAPI2.Utilities;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services;

[Singleton<SteamRepository>]
public class SteamRepository
{
    private readonly SteamWebInterfaceFactory _webInterfaceFactory;

    public SteamRepository(IConfiguration configuration)
    {
        var apiKey = configuration[Constants.Configuration.Steam.ApiKey];
        _webInterfaceFactory = new SteamWebInterfaceFactory(apiKey);
    }

    public async Task<PlayerSummaryModel> GetProfileAsync(ulong steamId64)
    {
        var steamInterface = _webInterfaceFactory.CreateSteamWebInterface<SteamUser>();
        var playerSummaryResponse = await steamInterface.GetPlayerSummaryAsync(steamId64)
            .ConfigureAwait(false);
        var playerSummaryData = playerSummaryResponse.Data;
        if (playerSummaryData is null)
            throw new NullReferenceException("Failed to receive player summary data");
        return playerSummaryData;
    }
}
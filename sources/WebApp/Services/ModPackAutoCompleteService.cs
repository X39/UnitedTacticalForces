using Microsoft.FluentUI.AspNetCore.Components;
using X39.UnitedTacticalForces.WebApp.Services.GameServerRepository;
using X39.UnitedTacticalForces.WebApp.Services.ModPackRepository;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services;

/// <summary>Provides normalized mod-pack revision choices for autocomplete controls.</summary>
[Transient<ModPackAutoCompleteService>]
public sealed class ModPackAutoCompleteService(
    IModPackRepository modPackRepository,
    IGameServerRepository gameServerRepository)
{
    /// <summary>Searches active normalized revisions.</summary>
    /// <param name="args">Autocomplete search arguments.</param>
    /// <returns>A task representing the operation.</returns>
    public async Task OnOptionsSearchAsync(OptionsSearchEventArgs<NormalizedModPackRevisionOption> args)
    {
        var capabilities = await gameServerRepository.GetCapabilitiesAsync().ConfigureAwait(false);
        var items = new List<NormalizedModPackRevisionOption>();
        foreach (var game in capabilities.Where(q => q.SupportsModPacks))
        {
            var definitions = await modPackRepository.GetNormalizedModPacksAsync(game.GameIdentifier)
                .ConfigureAwait(false);
            items.AddRange(definitions
                .Where(q => q.LatestRevisionId.HasValue)
                .Where(q => string.IsNullOrWhiteSpace(args.Text)
                            || q.Title.Contains(args.Text, StringComparison.CurrentCultureIgnoreCase))
                .Select(q => new NormalizedModPackRevisionOption
                {
                    DefinitionId = q.DefinitionId,
                    RevisionId = q.LatestRevisionId!.Value,
                    GameIdentifier = q.GameIdentifier,
                    Title = q.Title,
                }));
        }

        args.Items = items.Take(10).ToArray();
    }

    /// <summary>Formats a normalized mod-pack option.</summary>
    /// <param name="option">Option to format.</param>
    /// <returns>The display label.</returns>
    public string OptionText(NormalizedModPackRevisionOption option)
        => $"{option.Title} · {option.GameIdentifier} · r{option.RevisionId}";
}

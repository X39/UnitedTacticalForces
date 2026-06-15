using Microsoft.FluentUI.AspNetCore.Components;
using X39.UnitedTacticalForces.WebApp.UtfApi.Models;
using X39.UnitedTacticalForces.WebApp.Services.TerrainRepository;
using X39.UnitedTacticalForces.WebApp.UtfApi.Models;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services;

[Transient<TerrainAutoCompleteService>]
public class TerrainAutoCompleteService(ITerrainRepository terrainRepository)
{
    public PlainTerrainDtoImpl PlainTerrainDto { get; } = new(terrainRepository);

    public sealed class PlainTerrainDtoImpl(ITerrainRepository terrainRepository)
    {
        public async Task OnOptionsSearch(OptionsSearchEventArgs<PlainTerrainDto> arg)
        {
            var results = await terrainRepository.GetTerrainsAsync(0, 20, arg.Text);
            arg.Items = results;
        }

        public string? OptionText(PlainTerrainDto arg)
        {
            return arg.Title;
        }
    }
}

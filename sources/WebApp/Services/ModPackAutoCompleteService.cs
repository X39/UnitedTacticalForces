using Microsoft.FluentUI.AspNetCore.Components;
using X39.UnitedTacticalForces.WebApp.UtfApi.Models;
using X39.UnitedTacticalForces.WebApp.Services.ModPackRepository;
using X39.UnitedTacticalForces.WebApp.UtfApi.Models;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services;

[Transient<ModPackAutoCompleteService>]
public class ModPackAutoCompleteService(IModPackRepository modPackRepository)
{
    public FullModPackDefinitionDtoImpl FullModPackDefinitionDto { get; } = new(modPackRepository);

    public sealed class FullModPackDefinitionDtoImpl(IModPackRepository modPackRepository)
    {
        public async Task OnOptionsSearch(OptionsSearchEventArgs<FullModPackDefinitionDto> arg)
        {
            var results = await modPackRepository.GetModPacksAsync(0, 10, false, arg.Text);
            arg.Items = results;
        }

        public string? OptionText(FullModPackDefinitionDto arg)
        {
            return arg.Title;
        }
    }

    public ModPackDefinitionDtoImpl ModPackDefinitionDto { get; } = new(modPackRepository);

    public sealed class ModPackDefinitionDtoImpl(IModPackRepository modPackRepository)
    {
        public async Task OnOptionsSearch(OptionsSearchEventArgs<ModPackDefinitionDto> arg)
        {
            var results = await modPackRepository.GetModPacksAsync(0, 10, false, arg.Text);
            arg.Items = results.Select(modPack => new ModPackDefinitionDto
                {
                    IsActive                = modPack.IsActive,
                    IsComposition           = modPack.IsComposition,
                    MetaTimeStampDownloaded = null,
                    OwnerFk                 = ((PlainUserDto?) modPack.Owner)?.PrimaryKey,
                    PrimaryKey              = modPack.PrimaryKey,
                    RevisionHtml = modPack.ModPackRevisions
                        ?.OrderByDescending(e => e.TimeStampCreated)
                        .FirstOrDefault()
                        ?.Html,
                    RevisionIsActive = modPack.ModPackRevisions
                        ?.OrderByDescending(e => e.TimeStampCreated)
                        .FirstOrDefault()
                        ?.IsActive,
                    RevisionPrimaryKey = modPack.ModPackRevisions
                        ?.OrderByDescending(e => e.TimeStampCreated)
                        .FirstOrDefault()
                        ?.PrimaryKey,
                    RevisionTimeStampCreated = modPack.ModPackRevisions
                        ?.OrderByDescending(e => e.TimeStampCreated)
                        .FirstOrDefault()
                        ?.TimeStampCreated,
                    RevisionUpdatedByFk = modPack.ModPackRevisions
                        ?.OrderByDescending(e => e.TimeStampCreated)
                        .FirstOrDefault()
                        ?.UpdatedByFk,
                    TimeStampCreated = modPack.TimeStampCreated,
                    Title            = modPack.Title,
                }
            );
        }

        public string? OptionText(ModPackDefinitionDto arg)
        {
            return arg.Title;
        }
    }

    public ModPackRevisionDtoImpl ModPackRevisionDto { get; } = new(modPackRepository);

    public sealed class ModPackRevisionDtoImpl(IModPackRepository modPackRepository)
    {
        public async Task OnOptionsSearch(OptionsSearchEventArgs<ModPackRevisionDto> arg)
        {
            var results = await modPackRepository.GetModPacksAsync(0, 10, false, arg.Text);
            arg.Items = results.Select(definition => (definition, revision: definition.ModPackRevisions
                    ?.OrderByDescending(e => e.TimeStampCreated)
                    .FirstOrDefault())
                )
                .Select(tuple => new ModPackRevisionDto
                    {
                        DefinitionIsActive         = tuple.definition.IsActive,
                        DefinitionIsComposition    = tuple.definition.IsComposition,
                        DefinitionOwnerFk          = ((PlainUserDto?) tuple.definition.Owner)?.PrimaryKey,
                        DefinitionPrimaryKey       = tuple.definition.PrimaryKey,
                        DefinitionTimeStampCreated = tuple.definition.TimeStampCreated,
                        DefinitionTitle            = tuple.definition.Title,
                        Html                       = tuple.revision?.Html,
                        IsActive                   = tuple.revision?.IsActive,
                        MetaTimeStampDownloaded    = null,
                        PrimaryKey                 = tuple.revision?.PrimaryKey,
                        TimeStampCreated           = tuple.revision?.TimeStampCreated,
                        UpdatedByFk                = tuple.revision?.UpdatedByFk,
                    }
                );
        }

        public string? OptionText(ModPackRevisionDto arg)
        {
            return arg.DefinitionTitle;
        }
    }

    public PlainModPackDefinitionDtoImpl PlainModPackDefinitionDto { get; } = new(modPackRepository);

    public sealed class PlainModPackDefinitionDtoImpl(IModPackRepository modPackRepository)
    {
        public async Task OnOptionsSearch(OptionsSearchEventArgs<PlainModPackDefinitionDto> arg)
        {
            var results = await modPackRepository.GetModPacksAsync(0, 10, false, arg.Text);
            arg.Items = results.Select(definition => new PlainModPackDefinitionDto
                {
                    IsActive         = definition.IsActive,
                    IsComposition    = definition.IsComposition,
                    OwnerFk          = ((PlainUserDto?) definition.Owner)?.PrimaryKey,
                    PrimaryKey       = definition.PrimaryKey,
                    TimeStampCreated = definition.TimeStampCreated,
                    Title            = definition.Title,
                }
            );
        }

        public string? OptionText(PlainModPackDefinitionDto arg)
        {
            return arg.Title;
        }
    }

    public PlainModPackRevisionDtoImpl PlainModPackRevisionDto { get; } = new(modPackRepository);

    public sealed class PlainModPackRevisionDtoImpl(IModPackRepository modPackRepository)
    {
        public async Task OnOptionsSearch(OptionsSearchEventArgs<PlainModPackRevisionDto> arg)
        {
            var results = await modPackRepository.GetModPacksAsync(0, 10, false, arg.Text);
            arg.Items = results.Select(definition => (definition, revision: definition.ModPackRevisions
                    ?.OrderByDescending(e => e.TimeStampCreated)
                    .FirstOrDefault())
                )
                .Select(tuple => new PlainModPackRevisionDto
                    {
                        AdditionalData = new Dictionary<string, object>
                            { { "Title", tuple.definition.Title ?? string.Empty } },
                        DefinitionFk     = tuple.revision?.DefinitionFk,
                        Html             = tuple.revision?.Html,
                        IsActive         = tuple.revision?.IsActive,
                        PrimaryKey       = tuple.revision?.PrimaryKey,
                        TimeStampCreated = tuple.revision?.TimeStampCreated,
                        UpdatedByFk      = tuple.revision?.UpdatedByFk,
                    }
                );
        }

        public string? OptionText(PlainModPackRevisionDto arg)
        {
            return arg.AdditionalData.TryGetValue("Title", out var value)
                ? value as string
                : arg.PrimaryKey?.ToString();
        }
    }
}

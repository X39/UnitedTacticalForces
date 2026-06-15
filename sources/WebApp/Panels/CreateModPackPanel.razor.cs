using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;
using X39.UnitedTacticalForces.WebApp.UtfApi.Models;
using X39.UnitedTacticalForces.WebApp.Properties;
using X39.UnitedTacticalForces.WebApp.Services.ModPackRepository;
using X39.UnitedTacticalForces.WebApp.UtfApi.Models;

namespace X39.UnitedTacticalForces.WebApp.Panels;

public partial class CreateModPackPanel
{
    public class Data : IValidatableObject
    {
        [Required]
        [MinLength(1)]
        public string Title { get; set; } = string.Empty;

        public bool IsNew { get; init; } = true;

        public bool IsComposition { get; init; }

        public string Html { get; set; } = string.Empty;

        public string HtmlFileName { get; set; } = string.Empty;

        public FullModPackDefinitionDto? ModPack { get; init; }

        public IEnumerable<FullModPackDefinitionDto> SelectedModPacks { get; set; } = [];

        internal IStringLocalizer<Language>? Localizer { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(Title))
            {
                yield return new ValidationResult(
                    GetText(nameof(Language.Panels_CreateModPackPanel_ValidationTitleRequired), "A title is required."),
                    [nameof(Title)]
                );
            }

            if (!IsComposition)
            {
                if (IsNew && string.IsNullOrWhiteSpace(Html))
                    yield return new ValidationResult(
                        GetText(
                            nameof(Language.Panels_CreateModPackPanel_ValidationHtmlRequired),
                            "An HTML file is required for standalone mod packs."
                        ),
                        [nameof(Html)]
                    );
                yield break;
            }

            var selectedModPacks = SelectedModPacks.ToArray();
            if (selectedModPacks.Length == 0)
            {
                yield return new ValidationResult(
                    GetText(
                        nameof(Language.Panels_CreateModPackPanel_ValidationCompositionRequiresPack),
                        "At least one standalone mod pack is required for compositions."
                    ),
                    [nameof(SelectedModPacks)]
                );
                yield break;
            }

            if (selectedModPacks.Any((definitionDto) => GetLatestActiveRevision(definitionDto)?.PrimaryKey is null))
                yield return new ValidationResult(
                    GetText(
                        nameof(Language.Panels_CreateModPackPanel_ValidationSelectedRequiresRevision),
                        "Each selected mod pack must have a revision."
                    ),
                    [nameof(SelectedModPacks)]
                );
        }

        private string GetText(string key, string fallback)
            => Localizer?[key].Value ?? fallback;
    }

    [Parameter]
    public Data Content { get; set; } = default!;

    private string HtmlUploaderId
        => Content.IsNew ? "modpack-html-create-uploader" : "modpack-html-update-uploader";

    private string HtmlFileLabel
        => string.IsNullOrWhiteSpace(Content.HtmlFileName)
            ? Content.IsNew
                ? Localizer[nameof(Language.Dialogs_ModifyModPackDialog_SetModPackHtml)]
                : Localizer[nameof(Language.Dialogs_ModifyModPackDialog_ReplaceModPackHtml)]
            : Content.HtmlFileName;

    private string ModeLabel
        => Content.IsComposition
            ? Localizer[nameof(Language.Panels_CreateModPackPanel_ModeComposition)].Value
            : Localizer[nameof(Language.Panels_CreateModPackPanel_ModeStandalone)].Value;

    private string ModeStatusClass
        => Content.IsComposition ? "utf-status utf-status--info" : "utf-status utf-status--neutral";

    private string SelectedModPacksLabel
    {
        get
        {
            var count = Content.SelectedModPacks.Count();
            return count == 1
                ? Localizer[nameof(Language.Panels_CreateModPackPanel_SelectedStandalonePack)].Value
                : Localizer[nameof(Language.Panels_CreateModPackPanel_SelectedStandalonePacks), count].Value;
        }
    }

    private async Task OnCompletedAsync(IEnumerable<FluentInputFileEventArgs> arg)
    {
        if (arg.SingleOrDefault() is not { Stream: not null } file)
            return;
        using var reader = new StreamReader(file.Stream);
        Content.Html = await reader.ReadToEndAsync();
        Content.HtmlFileName = file.Name;
    }

    private async Task OnStandaloneOptionsSearch(OptionsSearchEventArgs<FullModPackDefinitionDto> arg)
    {
        await ModPackAutoCompleteService.FullModPackDefinitionDto.OnOptionsSearch(arg);
        arg.Items = arg.Items?
            .Where(IsSelectableStandalone)
            .ToArray() ?? [];
    }

    public static Task<PlainModPackDefinitionDto?> OpenCreateStandaloneAsync(
        IDialogService dialogService,
        IStringLocalizer<Language> localizer,
        IModPackRepository modPackRepository,
        CancellationToken cancellationToken = default
    )
        => OpenCreateAsync(false, dialogService, localizer, modPackRepository, cancellationToken);

    public static Task<PlainModPackDefinitionDto?> OpenCreateCompositionAsync(
        IDialogService dialogService,
        IStringLocalizer<Language> localizer,
        IModPackRepository modPackRepository,
        CancellationToken cancellationToken = default
    )
        => OpenCreateAsync(true, dialogService, localizer, modPackRepository, cancellationToken);

    public static async Task<bool> OpenUpdateAsync(
        IDialogService dialogService,
        IStringLocalizer<Language> localizer,
        IModPackRepository modPackRepository,
        FullModPackDefinitionDto modPack,
        CancellationToken cancellationToken = default
    )
    {
        if (modPack.PrimaryKey is not { } primaryKey)
            return false;

        var data = new Data
        {
            IsNew = false,
            IsComposition = modPack.IsComposition ?? false,
            Localizer = localizer,
            ModPack = modPack,
            Title = modPack.Title ?? string.Empty,
            SelectedModPacks = modPack.IsComposition ?? false
                ? await ResolveSelectedDefinitionsAsync(modPackRepository, modPack, localizer, cancellationToken)
                    .ConfigureAwait(false)
                : [],
        };
        var dialogReference = await dialogService.ShowPanelAsync<CreateModPackPanel>(
            data,
            CreateDialogParameters(
                data,
                localizer[nameof(Language.Dialogs_ModifyModPackDialog_Title)],
                localizer[nameof(Language.Common_Submit)],
                localizer
            )
        );
        var result = await dialogReference.Result;
        if (result.Cancelled || !IsValid(data))
            return false;

        if (data.IsComposition)
        {
            await modPackRepository.ModifyModPackAsync(
                    primaryKey,
                    title: data.Title,
                    useLatest: true,
                    modPackRevisionIds: GetLatestRevisionIds(data),
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false);
        }
        else
        {
            await modPackRepository.ModifyModPackAsync(
                    primaryKey,
                    title: data.Title,
                    html: string.IsNullOrWhiteSpace(data.Html) ? null : data.Html,
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false);
        }

        return true;
    }

    private static async Task<PlainModPackDefinitionDto?> OpenCreateAsync(
        bool isComposition,
        IDialogService dialogService,
        IStringLocalizer<Language> localizer,
        IModPackRepository modPackRepository,
        CancellationToken cancellationToken
    )
    {
        var data = new Data
        {
            IsComposition = isComposition,
            Localizer = localizer,
        };
        var dialogReference = await dialogService.ShowPanelAsync<CreateModPackPanel>(
            data,
            CreateDialogParameters(
                data,
                localizer[nameof(Language.Panels_CreateModPackPanel_CreateNewModPack)],
                localizer[nameof(Language.Common_Create)],
                localizer
            )
        );
        var result = await dialogReference.Result;
        if (result.Cancelled || !IsValid(data))
            return null;

        if (data.IsComposition)
        {
            var payload = new ModPackCompositionCreationPayload
            {
                Title = data.Title,
                RevisionIds = GetLatestRevisionIds(data)
                    .Cast<long?>()
                    .ToList(),
            };
            return await modPackRepository.CreateModPackAsync(payload, cancellationToken);
        }

        return await modPackRepository.CreateModPackAsync(
            new ModPackCreationPayload
            {
                Title = data.Title,
                Html = data.Html,
            },
            cancellationToken
        );
    }

    private static DialogParameters<Data> CreateDialogParameters(
        Data data,
        string title,
        string primaryAction,
        IStringLocalizer<Language> localizer
    )
        => new()
        {
            Content = data,
            Title = title,
            Alignment = HorizontalAlignment.Right,
            Modal = true,
            ShowDismiss = false,
            PrimaryAction = primaryAction,
            SecondaryAction = localizer[nameof(Language.Common_Cancel)],
            Width = "520px",
        };

    private static async Task<IReadOnlyList<FullModPackDefinitionDto>> ResolveSelectedDefinitionsAsync(
        IModPackRepository modPackRepository,
        FullModPackDefinitionDto modPack,
        IStringLocalizer<Language> localizer,
        CancellationToken cancellationToken
    )
    {
        var selectedDefinitions = new List<FullModPackDefinitionDto>();
        foreach (var revision in modPack.ModPackRevisions?
                     .Where((revision) => revision.DefinitionFk is not null)
                     .GroupBy((revision) => revision.DefinitionFk)
                     .Select((group) => group.OrderByDescending((revision) => revision.TimeStampCreated)
                         .First()) ?? [])
        {
            if (revision.DefinitionFk is not { } definitionFk)
                continue;

            try
            {
                var fullDefinition = await modPackRepository.GetFullModPackDefinitionAsync(definitionFk, cancellationToken)
                    .ConfigureAwait(false);
                if (IsSelectableStandalone(fullDefinition))
                    selectedDefinitions.Add(fullDefinition);
            }
            catch
            {
                selectedDefinitions.Add(
                    new FullModPackDefinitionDto
                    {
                        PrimaryKey = definitionFk,
                        Title = localizer[nameof(Language.Panels_CreateModPackPanel_FallbackModPackTitle), definitionFk].Value,
                        IsActive = true,
                        IsComposition = false,
                        ModPackRevisions = new List<PlainModPackRevisionDto> { revision },
                    }
                );
            }
        }

        return selectedDefinitions;
    }

    private static long[] GetLatestRevisionIds(Data data)
        => data.SelectedModPacks
            .Select((definitionDto) => GetLatestActiveRevision(definitionDto)?.PrimaryKey)
            .Where((revisionId) => revisionId is not null)
            .Select((revisionId) => revisionId!.Value)
            .ToArray();

    private static PlainModPackRevisionDto? GetLatestActiveRevision(FullModPackDefinitionDto definitionDto)
    {
        return definitionDto.ModPackRevisions
            ?.Where((revisionDto) => revisionDto.IsActive ?? true)
            ?.OrderByDescending((revisionDto) => revisionDto.TimeStampCreated)
            .FirstOrDefault();
    }

    private static bool IsSelectableStandalone(FullModPackDefinitionDto definitionDto)
        => !(definitionDto.IsComposition ?? false) && GetLatestActiveRevision(definitionDto)?.PrimaryKey is not null;

    private static bool IsValid(Data data)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(data);
        return Validator.TryValidateObject(
            data,
            validationContext,
            validationResults,
            validateAllProperties: true
        );
    }

    private string FormatTitle(string? title)
        => string.IsNullOrWhiteSpace(title) ? Localizer[nameof(Language.Panels_CreateModPackPanel_UntitledModPack)].Value : title;
}

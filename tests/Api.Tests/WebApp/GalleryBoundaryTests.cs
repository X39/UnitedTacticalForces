using Xunit;

namespace Api.Tests.WebApp;

public class GalleryBoundaryTests
{
    [Fact]
    public void GalleryCommandCenterSurface_ProvidesUploadTagUsageAndReferenceWorkflows()
    {
        var page = ReadRepoFile("sources/WebApp/Pages/Command/Content/Gallery/Index.razor");
        var repository = ReadRepoFile("sources/WebApp/Services/MediaAssetRepository/MediaAssetRepositoryImpl.cs");
        var layout = ReadRepoFile("sources/WebApp/Shared/CommandCenterLayout.razor");
        var baseUrl = ReadRepoFile("sources/WebApp/Services/BaseUrl.cs");
        var failures = new List<string>();

        AssertContains(page, "@page \"/command/content/gallery\"", "gallery page", failures);
        AssertContains(page, "IMediaAssetRepository MediaAssetRepository", "gallery page", failures);
        AssertContains(page, "FluentInputFile Id=\"gallery-image-uploader\"", "gallery page", failures);
        AssertContains(page, "Mode=\"InputFileMode.Stream\"", "gallery page", failures);
        AssertContains(page, "IncludeTags", "gallery page", failures);
        AssertContains(page, "ExcludeTags", "gallery page", failures);
        AssertContains(page, "TerrainDomain", "gallery page", failures);
        AssertContains(page, "EventDomain", "gallery page", failures);
        AssertContains(page, "FieldReportDomain", "gallery page", failures);
        AssertContains(page, "PublicPageDomain", "gallery page", failures);
        AssertContains(page, "SiteImageDomain", "gallery page", failures);
        AssertContains(page, "UnusedDomain", "gallery page", failures);
        AssertContains(page, "MatchesSourceToggles", "gallery page", failures);
        AssertContains(page, "asset.Usages.Any(usage => _enabledSources.Contains(usage.Domain))", "gallery page", failures);
        AssertContains(page, "CopyReferenceAsync", "gallery page", failures);
        AssertContains(page, "ReplaceTagsAsync", "gallery page", failures);
        AssertContains(repository, "media-assets/uploads", "gallery repository", failures);
        AssertContains(repository, "excludeTag", "gallery repository", failures);
        AssertContains(repository, "usageDomain", "gallery repository", failures);
        AssertContains(repository, "excludeUsageDomain", "gallery repository", failures);
        AssertContains(repository, "while (true)", "gallery repository", failures);
        AssertContains(repository, "skip += page.Length", "gallery repository", failures);
        AssertContains(layout, "command/content/gallery", "command center layout", failures);
        AssertContains(layout, "Shared_CommandCenter_Nav_Gallery", "command center layout", failures);
        AssertContains(baseUrl, "ResolveImageUrl", "BaseUrl", failures);
        AssertContains(baseUrl, "media-assets/", "BaseUrl", failures);

        AssertNoFailures(failures);
    }

    [Fact]
    public void GalleryApiSurface_DoesNotExposeStorageFileNameAndTracksKnownConsumers()
    {
        var controller = ReadRepoFile("sources/Api/Controllers/MediaAssetsController.cs");
        var service = ReadRepoFile("sources/Api/Services/Media/MediaAssetService.cs");
        var dto = ReadRepoFile("sources/Api/DTO/MediaAssetDto.cs");
        var repository = ReadRepoFile("sources/WebApp/Services/MediaAssetRepository/MediaAssetRepositoryImpl.cs");
        var repositoryInterface = ReadRepoFile("sources/WebApp/Services/MediaAssetRepository/IMediaAssetRepository.cs");
        var picker = ReadRepoFile("sources/WebApp/Components/GalleryImagePicker.razor");
        var pickerDialog = ReadRepoFile("sources/WebApp/Components/GalleryImagePickerDialog.razor");
        var pickerDialogStyles = ReadRepoFile("sources/WebApp/Components/GalleryImagePickerDialog.razor.css");
        var englishResources = ReadRepoFile("sources/WebApp/Properties/Language.resx");
        var germanResources = ReadRepoFile("sources/WebApp/Properties/Language.de.resx");
        var fieldReportEditor = ReadRepoFile("sources/WebApp/Pages/Command/FieldReports/Edit.razor");
        var publicPageEditor = ReadRepoFile("sources/WebApp/Pages/Command/Content/Pages/_Slug/Edit.razor");
        var siteImageEditor = ReadRepoFile("sources/WebApp/Pages/Command/Content/Images/_Group/Index.razor");
        var failures = new List<string>();

        AssertContains(controller, "[Route(Constants.Routes.MediaAssets)]", "media assets API", failures);
        AssertContains(controller, "[HttpPost(\"uploads\"", "media assets API", failures);
        AssertContains(controller, "[HttpPut(\"{assetId:guid}/tags\"", "media assets API", failures);
        AssertContains(controller, "[HttpGet(\"{assetId:guid}/content\"", "media assets API", failures);
        AssertContains(service, "Sha256Hash", "media asset service", failures);
        AssertContains(service, "Domain      = \"field-report\"", "media asset service", failures);
        AssertContains(service, "Domain      = \"public-page\"", "media asset service", failures);
        AssertContains(service, "StorageRootPath", "media asset service", failures);
        AssertContains(service, "IsPathInDirectory", "media asset service", failures);
        AssertDoesNotContain(dto, "StorageFileName", "media asset dto", failures);
        AssertContains(repositoryInterface, "GetAssetPageAsync", "gallery repository interface", failures);
        AssertContains(repository, "GetAssetPageAsync", "gallery repository", failures);
        AssertContains(picker, "ShowDialogAsync<GalleryImagePickerDialog, GalleryImagePickerDialog.Data>", "gallery picker", failures);
        AssertContains(pickerDialog, "PageSize = 48", "gallery picker dialog", failures);
        AssertContains(pickerDialog, "GetAssetPageAsync", "gallery picker dialog", failures);
        AssertContains(pickerDialog, "ImageSource.NormalizeMimeType", "gallery picker dialog", failures);
        AssertContains(pickerDialog, "Mode=\"InputFileMode.Stream\"", "gallery picker dialog", failures);
        AssertContains(pickerDialog, "UploadAssetAsync(stream, file.Name, contentType, Content.UploadTags)", "gallery picker dialog", failures);
        AssertContains(pickerDialog, "Load more", "gallery picker dialog", failures);
        AssertContains(pickerDialogStyles, "grid-template-rows: auto minmax(0, 1fr);", "gallery picker dialog styles", failures);
        AssertContains(pickerDialogStyles, "grid-template-rows: auto minmax(0, 1fr) auto;", "gallery picker dialog styles", failures);
        AssertContains(pickerDialogStyles, "overflow: hidden;", "gallery picker dialog styles", failures);
        AssertContains(englishResources, "Components_GalleryImagePicker_DialogTitle", "English resources", failures);
        AssertContains(germanResources, "Components_GalleryImagePicker_DialogTitle", "German resources", failures);
        AssertContains(germanResources, "Bild hochladen", "German resources", failures);
        AssertContains(fieldReportEditor, "<GalleryImagePicker", "field report editor", failures);
        AssertContains(publicPageEditor, "<GalleryImagePicker", "public page editor", failures);
        AssertContains(picker, "@if (ShowPreview)", "gallery picker", failures);
        AssertContains(siteImageEditor, "ShowPreview=\"false\"", "site image editor", failures);
        AssertDoesNotContain(fieldReportEditor, "LoadGalleryAssetsAsync", "field report editor", failures);
        AssertDoesNotContain(fieldReportEditor, "UploadHeroImageAsync(stream", "field report editor", failures);
        AssertDoesNotContain(publicPageEditor, "OnGalleryAssetChanged", "public page editor", failures);

        AssertNoFailures(failures);
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

    private static void AssertContains(
        string source,
        string marker,
        string file,
        ICollection<string> failures)
    {
        if (!source.Contains(marker, StringComparison.Ordinal))
            failures.Add($"{file}: expected to contain '{marker}'.");
    }

    private static void AssertDoesNotContain(
        string source,
        string marker,
        string file,
        ICollection<string> failures)
    {
        if (source.Contains(marker, StringComparison.Ordinal))
            failures.Add($"{file}: expected not to contain '{marker}'.");
    }

    private static void AssertNoFailures(IReadOnlyCollection<string> failures)
    {
        if (failures.Count != 0)
            Assert.Fail(string.Join(Environment.NewLine, failures));
    }
}

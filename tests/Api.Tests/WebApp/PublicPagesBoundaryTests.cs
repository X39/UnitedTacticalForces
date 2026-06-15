using Xunit;

namespace Api.Tests.WebApp;

public class PublicPagesBoundaryTests
{
    [Fact]
    public void GdprPage_FallsBackToPublishedMarkdownAndEscapesHeroImagePath()
    {
        var source = ReadRepoFile("sources/WebApp/Pages/GDPR/Index.razor");

        Assert.Contains("IPublicPageRepository PublicPages", source, StringComparison.Ordinal);
        Assert.Contains("GetPublishedPageAsync(\"gdpr\")", source, StringComparison.Ordinal);
        Assert.Contains("GdprPageContentSupport.TryDeserialize", source, StringComparison.Ordinal);
        Assert.Contains("GdprPageContentView", source, StringComparison.Ordinal);
        Assert.Contains("else if (HasPublishedMarkdown)", source, StringComparison.Ordinal);
        Assert.Contains("<FluentMarkdown @key=\"PublishedRevision?.PrimaryKey\" Content=\"@PublishedMarkdown\"/>", source, StringComparison.Ordinal);
        Assert.Contains("EscapeCssUrl(BaseUrl.ResolveImageUrl(SafeHeroImagePath))", source, StringComparison.Ordinal);
        Assert.Contains("IsSafeHeroImagePath", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GdprPageContentSupport.CreateLocalizedFallbackContent", source, StringComparison.Ordinal);
        Assert.DoesNotContain("utf-gdpr-managed-content", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GdprStructuredRenderer_NormalizesNullMarkdownStrings()
    {
        var source = ReadRepoFile("sources/WebApp/Pages/GDPR/GdprPageContentView.razor");
        var jsonContract = ReadRepoFile("sources/Common/PublicPages/GdprPageContentJson.cs");
        var normalizerContract = ReadRepoFile("sources/Common/PublicPages/GdprPageContentNormalizer.cs");

        Assert.Contains("GdprPageContentNormalizer.Normalize(Content)", source, StringComparison.Ordinal);
        Assert.Contains("MarkdownText(", source, StringComparison.Ordinal);
        Assert.Contains("private static string MarkdownText(string? text)", source, StringComparison.Ordinal);
        Assert.Contains("GdprPageContentJson", jsonContract, StringComparison.Ordinal);
        Assert.Contains("NormalizeString(string? value)", normalizerContract, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicPageRepository_UsesExpectedApiRoutes()
    {
        var source = ReadRepoFile("sources/WebApp/Services/PublicPageRepository/PublicPageRepositoryImpl.cs");

        Assert.Contains("public-pages/{Uri.EscapeDataString(slug)}", source, StringComparison.Ordinal);
        Assert.Contains("\"public-pages/admin\"", source, StringComparison.Ordinal);
        Assert.Contains("public-pages/admin/{Uri.EscapeDataString(slug)}", source, StringComparison.Ordinal);
        Assert.Contains("public-pages/admin/{Uri.EscapeDataString(slug)}/revisions", source, StringComparison.Ordinal);
        Assert.Contains("public-pages/admin/revisions/{revisionId}/publish", source, StringComparison.Ordinal);
        Assert.Contains("public-pages/admin/revisions/{revisionId}/unpublish", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicPageEditor_ValidatesRequiredFieldsAndShowsMarkdownAndGdprTemplatePreviews()
    {
        var source = ReadRepoFile("sources/WebApp/Pages/Command/Content/Pages/_Slug/Edit.razor");

        Assert.Contains("@page \"/command/content/pages/{Slug}/edit\"", source, StringComparison.Ordinal);
        Assert.Contains("ValidatePage()", source, StringComparison.Ordinal);
        Assert.Contains("PageValidationTitle", source, StringComparison.Ordinal);
        Assert.Contains("PageValidationMarkdown", source, StringComparison.Ordinal);
        Assert.Contains("@bind=\"_pageMarkdown\"", source, StringComparison.Ordinal);
        Assert.Contains("@bind:event=\"oninput\"", source, StringComparison.Ordinal);
        Assert.Contains("<GalleryImagePicker", source, StringComparison.Ordinal);
        Assert.Contains("PublicPageUploadTags = [\"public-page\"]", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IMediaAssetRepository", source, StringComparison.Ordinal);
        Assert.DoesNotContain("OnGalleryAssetChanged", source, StringComparison.Ordinal);
        Assert.Contains("command/content/gallery", source, StringComparison.Ordinal);
        Assert.Contains("<FluentMarkdown @key=\"_pageMarkdown\" Content=\"@GetPreviewMarkdown()\"/>", source, StringComparison.Ordinal);
        Assert.Contains("GdprPageContentEditor", source, StringComparison.Ordinal);
        Assert.Contains("GdprPageContentMarkdown.ToMarkdown", source, StringComparison.Ordinal);
        Assert.Contains("ContentJson", source, StringComparison.Ordinal);
        Assert.Contains("_preserveLegacyGdprMarkdown", source, StringComparison.Ordinal);
        Assert.Contains("OnContentChanged=\"HandleGdprContentChanged\"", source, StringComparison.Ordinal);
        Assert.Contains("LegacyMarkdownSaveBlocked", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicPageDetail_CanPublishAndUnpublishRevisions()
    {
        var source = ReadRepoFile("sources/WebApp/Pages/Command/Content/Pages/_Slug/Index.razor");

        Assert.Contains("@page \"/command/content/pages/{Slug}\"", source, StringComparison.Ordinal);
        Assert.Contains("PublishRevisionAsync(revision.PrimaryKey)", source, StringComparison.Ordinal);
        Assert.Contains("UnpublishRevisionAsync(revision.PrimaryKey)", source, StringComparison.Ordinal);
        Assert.Contains("PageToastPublicationUpdated", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CommandCenter_ExposesPublicPagesForContentClaim()
    {
        var layout = ReadRepoFile("sources/WebApp/Shared/CommandCenterLayout.razor");
        var overview = ReadRepoFile("sources/WebApp/Pages/Command/Index.razor");

        Assert.Contains("Claims.Content.Pages", layout, StringComparison.Ordinal);
        Assert.Contains("command/content/pages", layout, StringComparison.Ordinal);
        Assert.Contains("Shared_CommandCenter_Nav_PublicPages", layout, StringComparison.Ordinal);
        Assert.Contains("Claims.Content.Pages", overview, StringComparison.Ordinal);
        Assert.Contains("command/content/pages", overview, StringComparison.Ordinal);
        Assert.Contains("Pages_Command_Content_Pages_PageTitle", overview, StringComparison.Ordinal);
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


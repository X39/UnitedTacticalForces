using System.Text.Json;
using Api.Tests.GameServerHosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using X39.UnitedTacticalForces.Api.Controllers;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using Xunit;

namespace Api.Tests.ModPacks;

/// <summary>Verifies source previews retain their original client correlation value.</summary>
public sealed class ModSourcePreviewCorrelationTests
{
    /// <summary>Verifies multi-source previews return the original source input.</summary>
    [Fact]
    public async Task SourcePreview_ReturnsOriginalSourceInput()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var catalog = new GameServerDefinitionCatalog([new CounterStrike2GameServerDefinitionRegistration()]);
        var controller = new GameContentController(store.DbContext, catalog, null!, null!, [new TestSourceProvider()], null!);
        var payload = new GameContentController.ModSourcePreviewPayload(
            "cs2",
            [new GameContentController.ModSourceInputPayload("user-pasted-source")]);

        var response = Assert.IsType<OkObjectResult>(await controller.PreviewModSourcesAsync(payload, default));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response.Value));

        Assert.Equal("user-pasted-source", json.RootElement[0].GetProperty("SourceInput").GetString());
        Assert.Equal("https://example.test/canonical.git",
            json.RootElement[0].GetProperty("Source").GetProperty("CanonicalSourceIdentifier").GetString());
    }

    /// <summary>Verifies custom-format previews return the parsed source input.</summary>
    [Fact]
    public async Task FormatPreview_ReturnsOriginalSourceInput()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var controller = new ModPackFormatsController(
            store.DbContext,
            [new TestFormatHandler()],
            [new TestSourceProvider()]);
        await using var content = new MemoryStream([1]);
        var file = new FormFile(content, 0, content.Length, "file", "pack.test");

        var response = Assert.IsType<OkObjectResult>(
            await controller.PreviewAsync("cs2", "test-format", file, default));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response.Value));

        Assert.Equal("format-source", json.RootElement.GetProperty("Entries")[0]
            .GetProperty("SourceInput").GetString());
    }

    /// <summary>Deterministic source provider for correlation tests.</summary>
    private sealed class TestSourceProvider : IModSourceProvider
    {
        /// <inheritdoc />
        public string Identifier => ModSourceProviderIdentifiers.Git;

        /// <inheritdoc />
        public bool CanResolve(string input) => true;

        /// <inheritdoc />
        public Task<ModSourceResolution> ResolveAsync(
            string input,
            string? versionSelector,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ModSourceResolution(
                Identifier,
                "https://example.test/canonical.git",
                "Test Mod",
                versionSelector ?? string.Empty));
    }

    /// <summary>Deterministic format handler for correlation tests.</summary>
    private sealed class TestFormatHandler : IModPackFormatHandler
    {
        /// <inheritdoc />
        public string GameIdentifier => "cs2";

        /// <inheritdoc />
        public ModPackFormatDefinition Definition { get; } = new(
            "test-format",
            "Test format",
            "test",
            "application/octet-stream",
            1024);

        /// <inheritdoc />
        public Task<ModPackFormatImportResult> ImportAsync(
            Stream content,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ModPackFormatImportResult(
                "Test pack",
                [new ModPackFormatImportEntry("Test Mod", "format-source")],
                []));

        /// <inheritdoc />
        public Task<ModPackFormatExportResult> ExportAsync(
            string title,
            IReadOnlyCollection<ModPackFormatExportEntry> entries,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ModPackFormatExportResult([], []));
    }
}

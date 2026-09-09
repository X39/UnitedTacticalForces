using System.Globalization;
using System.Text;
using X39.UnitedTacticalForces.Api.Helpers;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

/// <summary>Imports and exports Arma 3 Launcher HTML as a projection of normalized membership.</summary>
[Singleton<Arma3LauncherHtmlFormatHandler, IModPackFormatHandler>]
public sealed class Arma3LauncherHtmlFormatHandler : IModPackFormatHandler
{
    /// <inheritdoc />
    public string GameIdentifier => Arma3GameServerDefinitionRegistration.GameIdentifier;
    /// <inheritdoc />
    public ModPackFormatDefinition Definition { get; } = new(
        "arma3-launcher-html", "Arma 3 Launcher", "html", "text/html", 4 * 1024 * 1024);

    /// <inheritdoc />
    public async Task<ModPackFormatImportResult> ImportAsync(
        Stream content,
        CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(content, Encoding.UTF8, true, 4096, true);
        var html = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var (title, mods, dependencies) = Arma3ModPackParser.FromHtml(html);
        var warnings = dependencies.Count == 0
            ? Array.Empty<string>()
            : ["Launcher dependency rows were imported as ordinary mod entries."];
        var entries = mods.Concat(dependencies)
            .GroupBy(q => q.Link, StringComparer.OrdinalIgnoreCase)
            .Select(q => new ModPackFormatImportEntry(q.First().Title, q.Key)).ToArray();
        return new(title, entries, warnings);
    }

    /// <inheritdoc />
    public Task<ModPackFormatExportResult> ExportAsync(
        string title,
        IReadOnlyCollection<ModPackFormatExportEntry> entries,
        CancellationToken cancellationToken = default)
    {
        var supported = entries.Where(q => q.ProviderIdentifier == ModSourceProviderIdentifiers.SteamWorkshop
                                            && q.UsageRole != EModUsageRole.ServerOnly).ToArray();
        var warnings = entries.Except(supported).Select(q => $"'{q.Title}' cannot be represented by Arma Launcher HTML.").ToArray();
        var mods = supported.Select(q => new Arma3ModPackParser.TitleLinkPair(q.Title,
            $"https://steamcommunity.com/sharedfiles/filedetails/?id={q.CanonicalSourceIdentifier}")).ToArray();
        var html = Arma3ModPackParser.ToHtml(title, mods, []);
        return Task.FromResult(new ModPackFormatExportResult(Encoding.UTF8.GetBytes(html), warnings));
    }
}

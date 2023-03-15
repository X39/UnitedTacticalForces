﻿using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.Data.Hosting;
using X39.UnitedTacticalForces.Api.Properties;

namespace X39.UnitedTacticalForces.Api.Services.GameServerController.Controllers;

/// <summary>
/// Implementation of <see cref="IGameServerController"/> for Arma 3 game servers.
/// </summary>
[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
public sealed class Arma3GameServerController : SteamGameServerControllerBase, IGameServerControllerCreatable
{

    private static readonly Regex HtmlARegex = new Regex(
        """<a href="(?<HREF>.+?)" .*?>(?<LABEL>.+?)</a>""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private Arma3GameServerController(
        GameServer gameServer,
        IDbContextFactory<ApiDbContext> dbContextFactory,
        ILogger<Arma3GameServerController> logger,
        IConfiguration configuration) : base(configuration, gameServer, dbContextFactory, logger)
    {
    }

    /// <inheritdoc />
    public static string Identifier { get; } = Constants.Steam.AppId.Arma3.ToString();

    /// <inheritdoc />
    public static Task<IGameServerController> CreateAsync(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        GameServer gameServer)
    {
        var controller = new Arma3GameServerController(
            gameServer,
            serviceProvider.GetRequiredService<IDbContextFactory<ApiDbContext>>(),
            serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<Arma3GameServerController>(),
            configuration);
        return Task.FromResult<IGameServerController>(controller);
    }

    /// <inheritdoc />
    public override bool AllowAnyConfigurationEntry => false;


    private const string RegExNumber = """\A[0-9]+\z""";

    private const string RegExArrayOfStrings =
        """\A\{\s*(?:(?<String>"(?:[^"]|"")+?")(?:,(?<String>\s*"(?:[^"]|"")+?"))*)?\}+\z""";

    private const string RealmHost      = "host";
    private const string RealmServerCfg = "server.cfg";

    /// <inheritdoc />
    public override IEnumerable<ConfigurationEntryDefinition> GetConfigurationEntryDefinitions(CultureInfo cultureInfo)
    {
        // @formatter:max_line_length 1000
        // ReSharper disable StringLiteralTypo
        yield return new ConfigurationEntryDefinition(false, RealmHost, EConfigurationEntryKind.Number, "port", RegExNumber, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_Host), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_Host_Port_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_Host_Port_Description), cultureInfo) ?? string.Empty);
        yield return new ConfigurationEntryDefinition(false, RealmHost, EConfigurationEntryKind.Text, "serverMod", RegExNumber, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_Host), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_Host_ServerMod_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_Host_ServerMod_Description), cultureInfo) ?? string.Empty);
        // https://community.bistudio.com/wiki/Arma_3:_Server_Config_File top to bottom
        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Password, "passwordAdmin", null, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_GeneralGroup), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_PasswordAdmin_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_PasswordAdmin_Description), cultureInfo) ?? string.Empty);
        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Password, "password", null, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_GeneralGroup), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_Password_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_Password_Description), cultureInfo) ?? string.Empty);
        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Password, "serverCommandPassword", null, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_GeneralGroup), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ServerCommandPassword_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ServerCommandPassword_Description), cultureInfo) ?? string.Empty);
        yield return new ConfigurationEntryDefinition(true, RealmServerCfg, EConfigurationEntryKind.Text, "hostname", null, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_GeneralGroup), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_Hostname_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_Hostname_Description), cultureInfo) ?? string.Empty);
        yield return new ConfigurationEntryDefinition(true, RealmServerCfg, EConfigurationEntryKind.Number, "maxPlayers", RegExNumber, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_GeneralGroup), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_MaxPlayers_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_MaxPlayers_Description), cultureInfo) ?? string.Empty);
        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Text, "motd[]", RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_GeneralGroup), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_Motd_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_Motd_Description), cultureInfo) ?? string.Empty);
        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Text, "admins[]", RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_GeneralGroup), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_Admins_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_Admins_Description), cultureInfo) ?? string.Empty);
        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Text, "headlessClients[]", RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_GeneralGroup), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_HeadlessClients_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_HeadlessClients_Description), cultureInfo) ?? string.Empty);
        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Text, "localClient[]", RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_GeneralGroup), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_LocalClient_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_LocalClient_Description), cultureInfo) ?? string.Empty);
        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Text, "filePatchingExceptions[]", RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_GeneralGroup), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_FilePatchingExceptions_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_FilePatchingExceptions_Description), cultureInfo) ?? string.Empty);
        // https://community.bistudio.com/wiki/Arma_3:_Basic_Server_Config_File
        // https://community.bistudio.com/wiki/Arma_3:_Server_Profile
        // https://community.bistudio.com/wiki/Arma_3:_Startup_Parameters
        // ReSharper restore StringLiteralTypo
        // @formatter:max_line_length restore
    }

    private StreamWriter CreateServerConfigurationWriter()
    {
        var fileStreamOptions = new FileStreamOptions
        {
            Mode    = FileMode.Create,
            Access  = FileAccess.Write,
            Share   = FileShare.Read,
            Options = FileOptions.Asynchronous | FileOptions.WriteThrough,
        };
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            fileStreamOptions.UnixCreateMode =
                UnixFileMode.UserRead
                | UnixFileMode.GroupRead
                | UnixFileMode.OtherRead;
        }

        return new StreamWriter(
            ServerConfigurationPath,
            Encoding.UTF8,
            fileStreamOptions);
    }

    private static string ToArmaString(string input)
    {
        var builder = new StringBuilder(input.Length + input.Count((c) => c is '"') + 2);
        builder.Append('"');
        foreach (var c in input)
        {
            builder.Append(c);
            if (c is '"')
                builder.Append('"');
        }

        builder.Append('"');
        return builder.ToString();
    }

    private string BattleEyePath => Path.Combine(GameServer.PrimaryKey.ToString("0000000"), "battle-eye");

    private string ServerConfigurationPath => Path.Combine(GameServer.PrimaryKey.ToString("0000000"), "server.cfg");

    private string BasicConfigurationPath => Path.Combine(GameServer.PrimaryKey.ToString("0000000"), "basic.cfg");

    private string PidPath => Path.Combine(GameServer.PrimaryKey.ToString("0000000"), "server.pid");

    private string ProfilesPath => Path.Combine(GameServer.PrimaryKey.ToString("0000000"), "profiles");

    /// <inheritdoc />
    protected override long SteamAppId => 107410;

    /// <inheritdoc />
    protected override async Task DoUpdateConfigurationAsync()
    {
        await using var dbContext = await DbContextFactory.CreateDbContextAsync()
            .ConfigureAwait(false);
        var gameServerPk = GameServer.PrimaryKey;
        var configurationEntries = dbContext.ConfigurationEntries
            .Where((q) => q.GameServerFk == gameServerPk)
            .Where((q) => q.IsActive)
            .Where((q) => q.Realm != RealmHost)
            .OrderBy((q) => q.Realm)
            .ThenBy((q) => q.Path)
            .AsAsyncEnumerable();
        StreamWriter? streamWriter = null;
        var entryDefinitions = GetConfigurationEntryDefinitions(CultureInfo.CurrentUICulture)
            .ToDictionary((q) => (q.Realm, q.Path));
        try
        {
            var realm = string.Empty;
            await foreach (var configurationEntry in configurationEntries.ConfigureAwait(false))
            {
                if (configurationEntry.Realm != realm || streamWriter is null)
                {
                    realm = configurationEntry.Realm;
                    if (streamWriter is not null)
                        await streamWriter.DisposeAsync()
                            .ConfigureAwait(false);
                    streamWriter = realm switch
                    {
                        RealmHost      => throw new Exception($"Query fault. Query should not return {realm} but did."),
                        RealmServerCfg => CreateServerConfigurationWriter(),
                        _              => throw new InvalidDataException($"Unknown realm {realm}")
                    };
                }

                var path = configurationEntry.Path;
                var definition = entryDefinitions.GetValueOrDefault(
                    (configurationEntry.Realm, configurationEntry.Path));
                var value = definition?.Kind is EConfigurationEntryKind.Text
                    ? ToArmaString(configurationEntry.Value)
                    : configurationEntry.Value;
                await streamWriter.WriteLineAsync($"{path} = {value};");
            }
        }
        finally
        {
            if (streamWriter is not null)
                await streamWriter.DisposeAsync()
                    .ConfigureAwait(false);
        }
    }

    /// <param name="dbContext"></param>
    /// <param name="executingUser"></param>
    /// <inheritdoc />
    protected override async ValueTask<ProcessStartInfo> GetProcessStartInfoAsync(
        ApiDbContext dbContext,
        User? executingUser)
    {
        var fileName = Path.Combine(
            GameInstallPath,
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "arma3server_x64.exe"
                : "arma3server_x64");
        var modList = await GetAndUpdateSelectedWorkshopItemsAsync(dbContext, executingUser)
            .ConfigureAwait(false);
        return new ProcessStartInfo
        {
            ArgumentList =
            {
                // ReSharper disable StringLiteralTypo
                $"-profiles={ProfilesPath}",
                $"-pid={PidPath}",
                $"-cfg={BasicConfigurationPath}",
                $"-config={ServerConfigurationPath}",
                $"-bepath{BattleEyePath}",
                $"-port={await GetAsync("host:://port", 12345).ConfigureAwait(false)}",
                $"-serverMod={await GetAsync("host:://serverMod", 12345).ConfigureAwait(false)}",
                $"-bandwidthAlg={2}",
                $"-limitFPS={50}",
                $"-mods={string.Join(';', modList)}",
                "-loadMissionToMemory",
                // ReSharper restore StringLiteralTypo
            },
            FileName               = fileName,
            RedirectStandardError  = true,
            RedirectStandardInput  = false,
            RedirectStandardOutput = true,
            StandardErrorEncoding  = Encoding.UTF8,
            StandardInputEncoding  = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8,
            CreateNoWindow         = true,
            WindowStyle            = ProcessWindowStyle.Hidden,
            UseShellExecute        = false,
        };
    }

    /// <inheritdoc />
    protected override async Task OnInstallOrUpgradeAsync(User? executingUser)
    {
        await using var dbContext = await DbContextFactory.CreateDbContextAsync().ConfigureAwait(false);
        _ = await GetAndUpdateSelectedWorkshopItemsAsync(dbContext, executingUser)
            .ConfigureAwait(false);
    }

    private async Task<IReadOnlyCollection<string>> GetAndUpdateSelectedWorkshopItemsAsync(
        ApiDbContext dbContext,
        User? executingUser)
    {
        const string workshopBase = "https://steamcommunity.com/sharedfiles/filedetails/?id=";
        var modPackPk = GameServer.SelectedModPackFk;
        if (modPackPk is null)
            return ArraySegment<string>.Empty;
        var mod = await dbContext.ModPacks.SingleOrDefaultAsync((q) => q.PrimaryKey == modPackPk).ConfigureAwait(false);
        if (mod is null)
            throw new NullReferenceException($"Failed to receive mod with the id {modPackPk} from database");
        var matches = HtmlARegex.Matches(mod.Html);
        var workshopItemPaths = new List<string>();
        foreach (Match match in matches)
        {
            var href = match.Groups["HREF"].Value;
            if (!Uri.IsWellFormedUriString(href, UriKind.Absolute))
                continue;
            if (!href.StartsWith(workshopBase))
                continue;
            var workshopItemIdString = href[workshopBase.Length..];
            if (!long.TryParse(workshopItemIdString, out var workshopItemId))
                continue;
            var workshopItemPath = await DoUpdateWorkshopMod(workshopItemId, executingUser)
                .ConfigureAwait(false);
            workshopItemPaths.Add(workshopItemPath);
        }

        return workshopItemPaths;
    }
}
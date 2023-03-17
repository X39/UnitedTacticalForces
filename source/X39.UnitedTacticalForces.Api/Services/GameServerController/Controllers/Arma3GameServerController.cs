using System.Collections.Immutable;
using System.Diagnostics;
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
using X39.Util;
using X39.Util.Collections;
using StreamWriter = System.IO.StreamWriter;

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
    protected override long ServerAppId => Constants.Steam.AppId.Arma3Server;

    /// <inheritdoc />
    protected override long GameAppId => Constants.Steam.AppId.Arma3;

    /// <inheritdoc/>
    protected override bool RequireLogin => true;

    /// <inheritdoc/>
    protected override bool RequirePurchaseForWorkshop => true;

    /// <inheritdoc />
    public static string Identifier { get; } = $"arma3-{Constants.Steam.AppId.Arma3Server}";

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
    private const string RealmBasicCfg  = "basic.cfg";

    /// <inheritdoc />
    public override IEnumerable<ConfigurationEntryDefinition> GetConfigurationEntryDefinitions(CultureInfo cultureInfo)
    {
        // @formatter:max_line_length 1000
        // ReSharper disable StringLiteralTypo
        yield return new ConfigurationEntryDefinition(false, RealmHost, EConfigurationEntryKind.Number, "port", RegExNumber, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_Host), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_Host_Port_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_Host_Port_Description), cultureInfo) ?? string.Empty, DefaultValue: "2302");
        yield return new ConfigurationEntryDefinition(false, RealmHost, EConfigurationEntryKind.Text, "serverMod", RegExNumber, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_Host), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_Host_ServerMod_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_Host_ServerMod_Description), cultureInfo) ?? string.Empty);
        // https://community.bistudio.com/wiki/Arma_3:_Server_Config_File top to bottom
        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Password, "passwordAdmin", null, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_GeneralGroup), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_PasswordAdmin_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_PasswordAdmin_Description), cultureInfo) ?? string.Empty);
        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Password, "password", null, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_GeneralGroup), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_Password_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_Password_Description), cultureInfo) ?? string.Empty);
        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Password, "serverCommandPassword", null, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_GeneralGroup), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ServerCommandPassword_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ServerCommandPassword_Description), cultureInfo) ?? string.Empty);
        yield return new ConfigurationEntryDefinition(true, RealmServerCfg, EConfigurationEntryKind.Text, "hostname", null, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_GeneralGroup), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_Hostname_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_Hostname_Description), cultureInfo) ?? string.Empty);
        yield return new ConfigurationEntryDefinition(true, RealmServerCfg, EConfigurationEntryKind.Number, "maxPlayers", RegExNumber, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_GeneralGroup), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_MaxPlayers_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_MaxPlayers_Description), cultureInfo) ?? string.Empty, MinValue: 0);
        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Text, "motd[]", RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_GeneralGroup), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_Motd_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_Motd_Description), cultureInfo) ?? string.Empty);
        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Text, "admins[]", RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_GeneralGroup), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_Admins_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_Admins_Description), cultureInfo) ?? string.Empty);
        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Text, "headlessClients[]", RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_GeneralGroup), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_HeadlessClients_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_HeadlessClients_Description), cultureInfo) ?? string.Empty);
        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Text, "localClient[]", RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_GeneralGroup), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_LocalClient_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_LocalClient_Description), cultureInfo) ?? string.Empty);
        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Text, "filePatchingExceptions[]", RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_GeneralGroup), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_FilePatchingExceptions_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_FilePatchingExceptions_Description), cultureInfo) ?? string.Empty);
        // https://community.bistudio.com/wiki/Arma_3:_Basic_Server_Config_File
        yield return new ConfigurationEntryDefinition(false, RealmBasicCfg, EConfigurationEntryKind.Number, "MaxMsgSend", RegExNumber, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_BasicCfg_GeneralGroup), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_MaxMsgSend_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_MaxMsgSend_Description), cultureInfo) ?? string.Empty, DefaultValue: "128");
        yield return new ConfigurationEntryDefinition(false, RealmBasicCfg, EConfigurationEntryKind.Number, "MaxSizeGuaranteed", RegExNumber, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_BasicCfg_GeneralGroup), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_MaxSizeGuaranteed_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_MaxSizeGuaranteed_Description), cultureInfo) ?? string.Empty, DefaultValue: "512");
        yield return new ConfigurationEntryDefinition(false, RealmBasicCfg, EConfigurationEntryKind.Number, "MaxSizeNonguaranteed", RegExNumber, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_BasicCfg_GeneralGroup), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_MaxSizeNonguaranteed_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_MaxSizeNonguaranteed_Description), cultureInfo) ?? string.Empty, DefaultValue: "256");
        yield return new ConfigurationEntryDefinition(false, RealmBasicCfg, EConfigurationEntryKind.Number, "MinBandwidth", RegExNumber, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_BasicCfg_GeneralGroup), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_MinBandwidth_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_MinBandwidth_Description), cultureInfo) ?? string.Empty, DefaultValue: "131072");
        yield return new ConfigurationEntryDefinition(false, RealmBasicCfg, EConfigurationEntryKind.Number, "MaxBandwidth", RegExNumber, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_BasicCfg_GeneralGroup), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_MaxBandwidth_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_MaxBandwidth_Description), cultureInfo) ?? string.Empty);
        yield return new ConfigurationEntryDefinition(false, RealmBasicCfg, EConfigurationEntryKind.Number, "MinErrorToSend", RegExNumber, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_BasicCfg_GeneralGroup), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_MinErrorToSend_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_MinErrorToSend_Description), cultureInfo) ?? string.Empty, DefaultValue: "0.001");
        yield return new ConfigurationEntryDefinition(false, RealmBasicCfg, EConfigurationEntryKind.Number, "MinErrorToSendNear", RegExNumber, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_BasicCfg_GeneralGroup), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_MinErrorToSendNear_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_MinErrorToSendNear_Description), cultureInfo) ?? string.Empty, DefaultValue: "0.01");
        yield return new ConfigurationEntryDefinition(false, RealmBasicCfg, EConfigurationEntryKind.Number, "MaxCustomFileSize", RegExNumber, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_BasicCfg_GeneralGroup), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_MaxCustomFileSize_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_MaxCustomFileSize_Description), cultureInfo) ?? string.Empty);
        yield return new ConfigurationEntryDefinition(false, RealmBasicCfg, EConfigurationEntryKind.Number, "sockets/maxPacketSize", RegExNumber, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_BasicCfg_GeneralGroup), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_SocketsMaxPacketSize_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_SocketsMaxPacketSize_Description), cultureInfo) ?? string.Empty, DefaultValue: "1400");
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

    private StreamWriter CreateBasicConfigurationWriter()
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
            BasicConfigurationPath,
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

    private string BattleEyePath => Path.Combine(GameServerPath, "battle-eye");

    private string ServerConfigurationPath => Path.Combine(GameServerPath, "server.cfg");

    private string BasicConfigurationPath => Path.Combine(GameServerPath, "basic.cfg");

    private string PidPath => Path.Combine(GameServerPath, "server.pid");

    private string ProfilesPath => Path.Combine(GameServerPath, "profiles");

    /// <inheritdoc />
    protected override async Task DoUpdateConfigurationAsync()
    {
        var activePathSegment = Array.Empty<string>();
        static string Tab(int len) => len <= 0 ? string.Empty : new string(' ', len * 4);

        async Task WritePathSegmentsIfNeeded(StreamWriter streamWriter, string[] pathSegments)
        {
            if (activePathSegment.SequenceEqual(pathSegments))
                return;
            var remainingMatchingPathSegments = activePathSegment
                .Zip(pathSegments)
                .TakeWhile((q) => q.First == q.Second)
                .Select((q) => q.First)
                .ToArray();
            for (var i = activePathSegment.Length; i > remainingMatchingPathSegments.Length; i--)
                await streamWriter.WriteLineAsync($"{Tab(i - 1)}}};").ConfigureAwait(false);
            for (var i = activePathSegment.Length; i < pathSegments.Length; i++)
                await streamWriter.WriteLineAsync($"{Tab(i)}class {pathSegments[i]} {{")
                    .ConfigureAwait(false);
            activePathSegment = pathSegments;
        }

        await using var dbContext = await DbContextFactory.CreateDbContextAsync()
            .ConfigureAwait(false);
        var gameServerPk = GameServer.PrimaryKey;
        var configurationEntries = await dbContext.ConfigurationEntries
            .Where((q) => q.GameServerFk == gameServerPk)
            .Where((q) => q.IsActive)
            .Where((q) => q.Realm != RealmHost)
            .OrderBy((q) => q.Realm)
            .ThenBy((q) => q.Path)
            .ToArrayAsync();
        var entryDefinitions = GetConfigurationEntryDefinitions(CultureInfo.CurrentUICulture)
            .ToDictionary((q) => (q.Realm, q.Path));
        await CreateBasicConfigurationWriter().DisposeAsync().ConfigureAwait(false);
        await CreateServerConfigurationWriter().DisposeAsync().ConfigureAwait(false);
        foreach (var group in configurationEntries.GroupBy((q) => q.Realm))
        {
            await using var streamWriter = group.Key switch
            {
                RealmHost      => throw new Exception($"Query fault. Query should not return {group.Key} but did."),
                RealmBasicCfg  => CreateBasicConfigurationWriter(),
                RealmServerCfg => CreateServerConfigurationWriter(),
                _              => throw new InvalidDataException($"Unknown realm {group.Key}"),
            };

            foreach (var configurationEntry in group
                         .OrderBy((q) => q.Path))
            {
                var splattedPath = configurationEntry.Path.Split('/');
                var pathSegments = splattedPath.SkipLast(1).ToArray();
                var actualPath = splattedPath.Last();
                await WritePathSegmentsIfNeeded(streamWriter, pathSegments)
                    .ConfigureAwait(false);

                var path = configurationEntry.Path;
                var definition = entryDefinitions.GetValueOrDefault(
                    (configurationEntry.Realm, configurationEntry.Path));
                var value = definition?.Kind is EConfigurationEntryKind.Text
                    ? ToArmaString(configurationEntry.Value)
                    : configurationEntry.Value;
                await streamWriter.WriteLineAsync($"{Tab(pathSegments.Length)}{actualPath} = {value};");
            }

            await WritePathSegmentsIfNeeded(streamWriter, Array.Empty<string>())
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
        var modList = await GetWorkshopPathsAsync(dbContext)
            .ConfigureAwait(false);
        var psi = new ProcessStartInfo
        {
            FileName               = fileName,
            RedirectStandardError  = true,
            RedirectStandardInput  = false,
            RedirectStandardOutput = true,
            StandardErrorEncoding  = Encoding.UTF8,
            // StandardInputEncoding  = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8,
            CreateNoWindow         = true,
            WindowStyle            = ProcessWindowStyle.Hidden,
            UseShellExecute        = false,
        };
        // ReSharper disable StringLiteralTypo
        psi.ArgumentList.Add($"-pid={PidPath}");
        psi.ArgumentList.Add($"-bandwidthAlg={2}");
        psi.ArgumentList.Add($"-limitFPS={120}");
        psi.ArgumentList.Add("-loadMissionToMemory");

        if (File.Exists(BasicConfigurationPath))
            psi.ArgumentList.Add($"-cfg={BasicConfigurationPath}");

        if (File.Exists(ServerConfigurationPath))
            psi.ArgumentList.Add($"-config={ServerConfigurationPath}");

        Directory.CreateDirectory(BattleEyePath);
        psi.ArgumentList.Add($"-bepath={BattleEyePath}");

        Directory.CreateDirectory(ProfilesPath);
        psi.ArgumentList.Add($"-profiles={ProfilesPath}");

        var port = await GetAsync("host://port", -1).ConfigureAwait(false);
        if (port > 0)
            psi.ArgumentList.Add($"-port={port.ToString()}");
        var serverMod = await GetAsync("host://serverMod").ConfigureAwait(false);
        if (serverMod.IsNotNullOrWhiteSpace())
            psi.ArgumentList.Add($"-serverMod={serverMod}");

        var mods = await GetWorkshopPathsAsync(dbContext).ConfigureAwait(false);
        if (mods.Any())
            psi.ArgumentList.Add($"-mods={string.Join(';', modList)}");
        // ReSharper restore StringLiteralTypo

        return psi;
    }

    /// <inheritdoc />
    protected override async Task OnInstallOrUpgradeAsync(User? executingUser)
    {
        await using var dbContext = await DbContextFactory.CreateDbContextAsync().ConfigureAwait(false);
        await UpdateWorkshopItemsOfActiveModPackAsync(dbContext, executingUser)
            .ConfigureAwait(false);
    }

    private async Task<IReadOnlyCollection<long>> GetWorkshopIdsAsync(ApiDbContext dbContext)
    {
        const string workshopBase = "https://steamcommunity.com/sharedfiles/filedetails/?id=";
        var modPackPk = GameServer.SelectedModPackFk;
        if (modPackPk is null)
            return ArraySegment<long>.Empty;
        var mod = await dbContext.ModPacks.SingleOrDefaultAsync((q) => q.PrimaryKey == modPackPk).ConfigureAwait(false);
        if (mod is null)
            throw new NullReferenceException($"Failed to receive mod with the id {modPackPk} from database");
        var matches = HtmlARegex.Matches(mod.Html);
        var workshopIds = new List<long>();
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
            workshopIds.Add(workshopItemId);
        }

        return workshopIds;
    }

    private async Task<IReadOnlyCollection<string>> GetWorkshopPathsAsync(
        ApiDbContext dbContext)
    {
        var workshopIds = await GetWorkshopIdsAsync(dbContext).ConfigureAwait(false);
        return workshopIds.Select(
                (q) => Path.Combine(
                    GetWorkshopPath(q),
                    "steamapps",
                    "workshop",
                    "content",
                    GameAppId.ToString(),
                    q.ToString()))
            .ToImmutableArray();
    }

    private async Task UpdateWorkshopItemsOfActiveModPackAsync(
        ApiDbContext dbContext,
        User? executingUser)
    {
        var workshopItemIds = await GetWorkshopIdsAsync(dbContext).ConfigureAwait(false);
        foreach (var workshopItemId in workshopItemIds)
        {
            _ = await DoUpdateWorkshopMod(workshopItemId, executingUser)
                .ConfigureAwait(false);
        }
    }
}
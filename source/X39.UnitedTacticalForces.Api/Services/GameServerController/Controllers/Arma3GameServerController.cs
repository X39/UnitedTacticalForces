using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.Data.Hosting;
using X39.UnitedTacticalForces.Api.Helpers;
using X39.UnitedTacticalForces.Api.Properties;
using X39.UnitedTacticalForces.Api.Services.UpdateStreamService;
using X39.UnitedTacticalForces.Contract.GameServer;
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
    private Arma3GameServerController(
        GameServer gameServer,
        IDbContextFactory<ApiDbContext> dbContextFactory,
        ILogger<Arma3GameServerController> logger,
        IConfiguration configuration,
        IUpdateStreamService updateStreamService) : base(
        configuration,
        gameServer,
        dbContextFactory,
        updateStreamService,
        logger)
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

    private string MpMissionsPath => Path.Combine(GameInstallPath, MpMissionsFolder);

    /// <inheritdoc />
    public static Task<IGameServerController> CreateAsync(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        GameServer gameServer,
        IUpdateStreamService updateStreamService)
    {
        var controller = new Arma3GameServerController(
            gameServer,
            serviceProvider.GetRequiredService<IDbContextFactory<ApiDbContext>>(),
            serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<Arma3GameServerController>(),
            configuration,
            updateStreamService);
        return Task.FromResult<IGameServerController>(controller);
    }

    /// <inheritdoc />
    public override bool AllowAnyConfigurationEntry => false;

    private const string RegExNumber  = """\A[0-9]+\z""";
    private const string RegExBoolean = """\A[01]\z""";
    private const string RegExDecimal = """\A[0-9]+(?:\.[0-9]+)?\z""";


    private const string RegExArrayOfStrings =
        """\A\{\s*(?:(?<String>"(?:[^"]|"")+?")(?:,(?<String>\s*"(?:[^"]|"")+?"))*)?\}+\z""";

    private const string RegExAllowedVoteCmds =
        """\A\{(?:\s*{\s*(?<CommandName>"(?:[^"]|"")+?")(?<PreMissionStart>\s*,\s*(?:true|false)(?<PostMissionStart>\s*,\s*(?:true|false)(?<VotingThreshold>\s*,\s*[0-9]+(?:\.[0-9]+)?(?<PercentSideVotingThreshold>\s*,\s*[0-9]+(?:\.[0-9]+)?)?)?)?)?\s*}\s*(?:,\s*{\s*(?<CommandName>"(?:[^"]|"")+?")(?<PreMissionStart>\s*,\s*(?:true|false)(?<PostMissionStart>\s*,\s*(?:true|false)(?<VotingThreshold>\s*,\s*[0-9]+(?:\.[0-9]+)?(?<PercentSideVotingThreshold>\s*,\s*[0-9]+(?:\.[0-9]+)?)?)?)?)?\s*}\s*)*)?\}+\z""";

    private const string RegExAllowedVotedAdminCmds =
        """\A\{(?:\s*{\s*(?<CommandName>"(?:[^"]|"")+?")(?<PreMissionStart>\s*,\s*(?:true|false)(?<PostMissionStart>\s*,\s*(?:true|false))?)?\s*}\s*(?:,\s*{\s*(?<CommandName>"(?:[^"]|"")+?")(?<PreMissionStart>\s*,\s*(?:true|false)(?<PostMissionStart>\s*,\s*(?:true|false))?)?\s*}\s*)*)?\}+\z""";

    // ReSharper disable once IdentifierTypo
    private const string RealmBattlEyeCfg = "beserver.cfg";
    private const string RealmHost        = "host";
    private const string RealmServerCfg   = "server.cfg";
    private const string RealmBasicCfg    = "basic.cfg";
    private const string MpMissionsFolder = "mpmissions";


    /// <inheritdoc />
    public override IEnumerable<ConfigurationEntryDefinition> GetConfigurationEntryDefinitions(CultureInfo cultureInfo)
    {
        // @formatter:max_line_length 2000
        // ReSharper disable StringLiteralTypo
        // https://community.bistudio.com/wiki/Arma_3:_Startup_Parameters
        yield return new ConfigurationEntryDefinition(false, RealmHost, EConfigurationEntryKind.Number, "port", RegExNumber, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_Host), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_Host_Port_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_Host_Port_Description), cultureInfo) ?? string.Empty, DefaultValue: "2302");
        yield return new ConfigurationEntryDefinition(false, RealmHost, EConfigurationEntryKind.Raw, "serverMod", RegExNumber, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_Host), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_Host_ServerMod_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_Host_ServerMod_Description), cultureInfo) ?? string.Empty);
        yield return new ConfigurationEntryDefinition(false, RealmHost, EConfigurationEntryKind.Raw, "mod", null, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_Host), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_Host_Mod_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_Host_Mod_Description), cultureInfo) ?? string.Empty);
        yield return new ConfigurationEntryDefinition(false, RealmHost, EConfigurationEntryKind.Raw, "headless-client-ip", null, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_Host), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_Host_HeadlessClientIp_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_Host_HeadlessClientIp_Description), cultureInfo) ?? string.Empty);
        yield return new ConfigurationEntryDefinition(false, RealmHost, EConfigurationEntryKind.Raw, "headless-client-password", null, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_Host), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_Host_HeadlessClientPassword_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_Host_HeadlessClientPassword_Description), cultureInfo) ?? string.Empty);
        yield return new ConfigurationEntryDefinition(false, RealmHost, EConfigurationEntryKind.Selection, "branch", null, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_Host), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_Host_Branch_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_Host_Branch_Description), cultureInfo) ?? string.Empty, DefaultValue: string.Empty, AllowedValues: new ValuePair[] {new("", "default"), new("contact", "contact"), new("creatordlc", "creatordlc"), new("profiling", "profiling")});
        // https://community.bistudio.com/wiki/Arma_3:_Server_Config_File top to bottom
        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Password, "passwordAdmin", null, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_GeneralGroup), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_PasswordAdmin_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_PasswordAdmin_Description), cultureInfo) ?? string.Empty);
        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Password, "password", null, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_GeneralGroup), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_Password_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_Password_Description), cultureInfo) ?? string.Empty);
        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Password, "serverCommandPassword", null, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_GeneralGroup), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ServerCommandPassword_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ServerCommandPassword_Description), cultureInfo) ?? string.Empty);
        yield return new ConfigurationEntryDefinition(true, RealmServerCfg, EConfigurationEntryKind.String, "hostname", null, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_GeneralGroup), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_Hostname_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_Hostname_Description), cultureInfo) ?? string.Empty);
        yield return new ConfigurationEntryDefinition(true, RealmServerCfg, EConfigurationEntryKind.Number, "maxPlayers", RegExNumber, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_GeneralGroup), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_MaxPlayers_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_MaxPlayers_Description), cultureInfo) ?? string.Empty, MinValue: 0);
        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Raw, "motd[]", RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_GeneralGroup), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_Motd_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_Motd_Description), cultureInfo) ?? string.Empty);
        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Raw, "admins[]", RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_GeneralGroup), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_Admins_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_Admins_Description), cultureInfo) ?? string.Empty);
        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Raw, "headlessClients[]", RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_GeneralGroup), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_HeadlessClients_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_HeadlessClients_Description), cultureInfo) ?? string.Empty);
        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Raw, "localClient[]", RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_GeneralGroup), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_LocalClient_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_LocalClient_Description), cultureInfo) ?? string.Empty);
        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Raw, "filePatchingExceptions[]", RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_GeneralGroup), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_FilePatchingExceptions_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_FilePatchingExceptions_Description), cultureInfo) ?? string.Empty);

        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Number, "voteThreshold", RegExNumber, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ServerBehavior), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_VoteThreshold_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_VoteThreshold_Description), cultureInfo) ?? string.Empty);
        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Number, "voteMissionPlayers", RegExDecimal, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ServerBehavior), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_VoteMissionPlayers_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_VoteMissionPlayers_Description), cultureInfo) ?? string.Empty);
        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Raw, "allowedVoteCmds[]", RegExAllowedVoteCmds, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ServerBehavior), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_AllowedVoteCmdsArray_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_AllowedVoteCmdsArray_Description), cultureInfo) ?? string.Empty, DefaultValue: "{}");
        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Raw, "allowedVotedAdminCmds[]", RegExAllowedVotedAdminCmds, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ServerBehavior), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_AllowedVotedAdminCmdsArray_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_AllowedVotedAdminCmdsArray_Description), cultureInfo) ?? string.Empty, DefaultValue: "{}");
        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Boolean, "kickduplicate", RegExBoolean, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ServerBehavior), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_Kickduplicate_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_Kickduplicate_Description), cultureInfo) ?? string.Empty);
        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Boolean, "loopback", RegExBoolean, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ServerBehavior), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_Loopback_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_Loopback_Description), cultureInfo) ?? string.Empty);
        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Boolean, "upnp", RegExBoolean, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ServerBehavior), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_Upnp_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_Upnp_Description), cultureInfo) ?? string.Empty);
        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Selection, "allowedFilePatching", null, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ServerBehavior), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_AllowedFilePatching_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_AllowedFilePatching_Description), cultureInfo) ?? string.Empty, DefaultValue: "0", AllowedValues: new ValuePair[] {new(Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_AllowedFilePatching_Value0_Name), cultureInfo) ?? string.Empty, "0", Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_AllowedFilePatching_Value0_Description), cultureInfo) ?? string.Empty), new(Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_AllowedFilePatching_Value1_Name), cultureInfo) ?? string.Empty, "1", Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_AllowedFilePatching_Value1_Description), cultureInfo) ?? string.Empty), new(Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_AllowedFilePatching_Value2_Name), cultureInfo) ?? string.Empty, "2", Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_AllowedFilePatching_Value2_Description), cultureInfo) ?? string.Empty)});
        // yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Raw, "allowedLoadFileExtensions[]",       RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ServerBehavior), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_AllowedLoadFileExtensionsArray_Title), cultureInfo) ?? string.Empty,       Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_AllowedLoadFileExtensionsArray_Description), cultureInfo) ?? string.Empty                                                                                           );
        // yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Raw, "allowedPreprocessFileExtensions[]", RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ServerBehavior), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_AllowedPreprocessFileExtensionsArray_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_AllowedPreprocessFileExtensionsArray_Description), cultureInfo) ?? string.Empty                                                                                           );
        // yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Raw, "allowedHTMLLoadExtensions[]",       RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ServerBehavior), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_AllowedHTMLLoadExtensionsArray_Title), cultureInfo) ?? string.Empty,       Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_AllowedHTMLLoadExtensionsArray_Description), cultureInfo) ?? string.Empty                                                                                           );
        // yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Raw, "allowedHTMLLoadURIs[]",             RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ServerBehavior), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_AllowedHTMLLoadURIsArray_Title), cultureInfo) ?? string.Empty,             Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_AllowedHTMLLoadURIsArray_Description), cultureInfo) ?? string.Empty                                                                                           );
        // yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Raw, "disconnectTimeout",                 RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ServerBehavior), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_DisconnectTimeout_Title), cultureInfo) ?? string.Empty,                    Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_DisconnectTimeout_Description), cultureInfo) ?? string.Empty                                                                                           );
        // yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Raw, "maxdesync",                         RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ServerBehavior), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_Maxdesync_Title), cultureInfo) ?? string.Empty,                            Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_Maxdesync_Description), cultureInfo) ?? string.Empty                                                                                           );
        // yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Raw, "maxping",                           RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ServerBehavior), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_Maxping_Title), cultureInfo) ?? string.Empty,                              Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_Maxping_Description), cultureInfo) ?? string.Empty                                                                                           );
        // yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Raw, "maxpacketloss",                     RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ServerBehavior), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_Maxpacketloss_Title), cultureInfo) ?? string.Empty,                        Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_Maxpacketloss_Description), cultureInfo) ?? string.Empty                                                                                           );
        // yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Raw, "kickClientsOnSlowNetwork[]",        RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ServerBehavior), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_KickClientsOnSlowNetworkArray_Title), cultureInfo) ?? string.Empty,        Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_KickClientsOnSlowNetworkArray_Description), cultureInfo) ?? string.Empty                                                                                           );
        // yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Raw, "enablePlayerDiag",                  RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ServerBehavior), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_EnablePlayerDiag_Title), cultureInfo) ?? string.Empty,                     Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_EnablePlayerDiag_Description), cultureInfo) ?? string.Empty                                                                                           );
        // yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Raw, "callExtReportLimit",                RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ServerBehavior), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_CallExtReportLimit_Title), cultureInfo) ?? string.Empty,                   Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_CallExtReportLimit_Description), cultureInfo) ?? string.Empty                                                                                           );
        // yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Raw, "kickTimeout[]",                     RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ServerBehavior), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_KickTimeoutArray_Title), cultureInfo) ?? string.Empty,                     Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_KickTimeoutArray_Description), cultureInfo) ?? string.Empty                                                                                           );
        // yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Raw, "votingTimeOut",                     RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ServerBehavior), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_VotingTimeOut_Title), cultureInfo) ?? string.Empty,                        Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_VotingTimeOut_Description), cultureInfo) ?? string.Empty                                                                                           );
        // yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Raw, "votingTimeOut[]",                   RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ServerBehavior), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_VotingTimeOutArray_Title), cultureInfo) ?? string.Empty,                   Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_VotingTimeOutArray_Description), cultureInfo) ?? string.Empty                                                                                           );
        // yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Raw, "roleTimeOut",                       RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ServerBehavior), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_RoleTimeOut_Title), cultureInfo) ?? string.Empty,                          Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_RoleTimeOut_Description), cultureInfo) ?? string.Empty                                                                                           );
        // yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Raw, "roleTimeOut[]",                     RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ServerBehavior), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_RoleTimeOutArray_Title), cultureInfo) ?? string.Empty,                     Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_RoleTimeOutArray_Description), cultureInfo) ?? string.Empty                                                                                           );
        // yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Raw, "briefingTimeOut",                   RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ServerBehavior), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_BriefingTimeOut_Title), cultureInfo) ?? string.Empty,                      Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_BriefingTimeOut_Description), cultureInfo) ?? string.Empty                                                                                           );
        // yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Raw, "briefingTimeOut[]",                 RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ServerBehavior), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_BriefingTimeOutArray_Title), cultureInfo) ?? string.Empty,                 Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_BriefingTimeOutArray_Description), cultureInfo) ?? string.Empty                                                                                           );
        // yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Raw, "debriefingTimeOut",                 RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ServerBehavior), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_DebriefingTimeOut_Title), cultureInfo) ?? string.Empty,                    Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_DebriefingTimeOut_Description), cultureInfo) ?? string.Empty                                                                                           );
        // yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Raw, "debriefingTimeOut[]",               RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ServerBehavior), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_DebriefingTimeOutArray_Title), cultureInfo) ?? string.Empty,               Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_DebriefingTimeOutArray_Description), cultureInfo) ?? string.Empty                                                                                           );
        // yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Raw, "lobbyIdleTimeout",                  RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ServerBehavior), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_LobbyIdleTimeout_Title), cultureInfo) ?? string.Empty,                     Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_LobbyIdleTimeout_Description), cultureInfo) ?? string.Empty                                                                                           );
        // yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Raw, "missionsToServerRestart",           RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ServerBehavior), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_MissionsToServerRestart_Title), cultureInfo) ?? string.Empty,              Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_MissionsToServerRestart_Description), cultureInfo) ?? string.Empty                                                                                           );
        // yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Raw, "missionsToShutdown",                RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ServerBehavior), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_MissionsToShutdown_Title), cultureInfo) ?? string.Empty,                   Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_MissionsToShutdown_Description), cultureInfo) ?? string.Empty                                                                                           );
        // yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Raw, "autoSelectMission",                 RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ServerBehavior), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_AutoSelectMission_Title), cultureInfo) ?? string.Empty,                    Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_AutoSelectMission_Description), cultureInfo) ?? string.Empty                                                                                           );
        // yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Raw, "randomMissionOrder",                RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ServerBehavior), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_RandomMissionOrder_Title), cultureInfo) ?? string.Empty,                   Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_RandomMissionOrder_Description), cultureInfo) ?? string.Empty                                                                                           );
        // yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Raw, "disableChannels[]",                 RegExArrayOfStrings, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ServerBehavior), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_DisableChannelsArray_Title), cultureInfo) ?? string.Empty,                 Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_DisableChannelsArray_Description), cultureInfo) ?? string.Empty                                                                                           );
        // Other Options
        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Boolean, "battlEye", RegExBoolean, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_OtherOptions), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_BattlEye_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_BattlEye_Description), cultureInfo) ?? string.Empty, DefaultValue: "1");
        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Boolean, "disableVoN", RegExBoolean, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_OtherOptions), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_DisableVoN_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_DisableVoN_Description), cultureInfo) ?? string.Empty, DefaultValue: "0");
        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Boolean, "persistent", RegExBoolean, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_OtherOptions), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_Persistent_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_Persistent_Description), cultureInfo) ?? string.Empty, DefaultValue: "0");
        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Number, "steamProtocolMaxDataSize", RegExNumber, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_OtherOptions), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_SteamProtocolMaxSize_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_SteamProtocolMaxSize_Description), cultureInfo) ?? string.Empty);
        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Boolean, "advancedOptions/logObjectNotFound", RegExBoolean, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_OtherOptions), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_AdvancedOptionsLogObjectNotFound_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_AdvancedOptionsLogObjectNotFound_Description), cultureInfo) ?? string.Empty, DefaultValue: "1");
        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Boolean, "advancedOptions/skipDescriptionParsing", RegExBoolean, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_OtherOptions), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_AdvancedOptionsSkipDescriptionParsing_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_AdvancedOptionsSkipDescriptionParsing_Description), cultureInfo) ?? string.Empty, DefaultValue: "0");
        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Boolean, "advancedOptions/ignoreMissionLoadErrors", RegExBoolean, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_OtherOptions), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_AdvancedOptionsIgnoreMissionLoadErrors_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_AdvancedOptionsIgnoreMissionLoadErrors_Description), cultureInfo) ?? string.Empty, DefaultValue: "0");
        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Boolean, "advancedOptions/queueSizeLogG", RegExNumber, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_OtherOptions), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_AdvancedOptionsQueueSizeLogG_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_AdvancedOptionsQueueSizeLogG_Description), cultureInfo) ?? string.Empty, DefaultValue: "1000000");
        yield return new ConfigurationEntryDefinition(false, RealmServerCfg, EConfigurationEntryKind.Boolean, "forcedDifficulty", null, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_OtherOptions), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ForcedDifficulty_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_ServerCfg_ForcedDifficulty_Description), cultureInfo) ?? string.Empty);
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
        // BattlEye config
        yield return new ConfigurationEntryDefinition(false, RealmBattlEyeCfg, EConfigurationEntryKind.Password, "RConPassword", null, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_BattlEyeCfg_Rcon), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_BattlEyeCfg_Rcon_RconPassword_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_BattlEyeCfg_Rcon_RconPassword_Description), cultureInfo) ?? string.Empty);
        yield return new ConfigurationEntryDefinition(false, RealmBattlEyeCfg, EConfigurationEntryKind.Password, "RConPort", null, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_BattlEyeCfg_Rcon), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_BattlEyeCfg_Rcon_RconPort_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_BattlEyeCfg_Rcon_RconPort_Description), cultureInfo) ?? string.Empty);
        yield return new ConfigurationEntryDefinition(false, RealmBattlEyeCfg, EConfigurationEntryKind.Password, "RConIP", null, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_BattlEyeCfg_Rcon), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_BattlEyeCfg_Rcon_RConIP_Title), cultureInfo) ?? string.Empty, Language.ResourceManager.GetString(nameof(Language.ServerController_Arma3_BattlEyeCfg_Rcon_RConIP_Description), cultureInfo) ?? string.Empty);
        // ReSharper restore StringLiteralTypo
        // @formatter:max_line_length restore
    }

    /// <inheritdoc />
    public override Task<IEnumerable<GameFolder>> GetGameFoldersAsync(
        CultureInfo cultureInfo,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<GameFolder> Yield()
        {
            yield return new GameFolder
            {
                Identifier = MpMissionsFolder,
                Name = Language.ResourceManager.GetString(
                    nameof(Language.ServerController_Arma3_GameFolders_MPMissions_Name),
                    cultureInfo) ?? string.Empty,
                Description = Language.ResourceManager.GetString(
                    nameof(Language.ServerController_Arma3_GameFolders_MPMissions_Description),
                    cultureInfo) ?? string.Empty,
                AllowedExtensions = new[] {".pbo"},
            };
        }

        return Task.FromResult(Yield());
    }

    /// <inheritdoc />
    public override Task<IEnumerable<GameFileInfo>> GetGameFolderFilesAsync(
        GameFolder folder,
        CultureInfo cultureInfo,
        CancellationToken cancellationToken = default)
    {
        switch (folder.Identifier)
        {
            case MpMissionsFolder:
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    Directory.CreateDirectory(MpMissionsPath, DefaultUnixFileMode);
                else
                    Directory.CreateDirectory(MpMissionsPath);
                return Task.FromResult<IEnumerable<GameFileInfo>>(
                    Directory.GetFiles(MpMissionsPath, "*.pbo", SearchOption.TopDirectoryOnly)
                        .Select(
                            pboPath => new GameFileInfo(
                                Path.GetFileName(pboPath),
                                new FileInfo(pboPath).Length,
                                "application/octet-stream"))
                        .ToArray());
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(folder));
        }
    }

    /// <inheritdoc />
    public override bool CanModifyGameFiles => CanStart;

    /// <inheritdoc />
    public override Task<Stream> GetGameFolderFileAsync(
        GameFolder folder,
        GameFileInfo file,
        CancellationToken cancellationToken = default)
    {
        var filePath = Path.Combine(MpMissionsPath, file.Name);
        if (!file.Name.EndsWith(".pbo", StringComparison.OrdinalIgnoreCase))
            // ReSharper disable once LocalizableElement
            throw new ArgumentException("Invalid file", nameof(file));
        if (file.Name.Contains('/', StringComparison.OrdinalIgnoreCase)
            || file.Name.Contains('\\', StringComparison.OrdinalIgnoreCase))
            // ReSharper disable once LocalizableElement
            throw new ArgumentException("Invalid file", nameof(file));
        switch (folder.Identifier)
        {
            case MpMissionsFolder:
            {
                if (!File.Exists(filePath))
                    throw new IOException("File not found");
                return Task.FromResult<Stream>(File.OpenRead(filePath));
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(folder));
        }
    }

    /// <inheritdoc />
    public override async Task UploadFileAsync(GameFolder folder, GameFileInfo file, Stream stream)
    {
        if (!CanStart)
            throw new InvalidOperationException("Server is running");
        switch (folder.Identifier)
        {
            case MpMissionsFolder:
            {
                if (!file.Name.EndsWith(".pbo", StringComparison.OrdinalIgnoreCase))
                    // ReSharper disable once LocalizableElement
                    throw new ArgumentException("Invalid file", nameof(file));
                if (file.Name.Contains('/', StringComparison.OrdinalIgnoreCase)
                    || file.Name.Contains('\\', StringComparison.OrdinalIgnoreCase))
                    // ReSharper disable once LocalizableElement
                    throw new ArgumentException("Invalid file", nameof(file));
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    Directory.CreateDirectory(MpMissionsPath, DefaultUnixFileMode);
                else
                    Directory.CreateDirectory(MpMissionsPath);
                await using var fileStream = File.OpenWrite(Path.Combine(MpMissionsPath, file.Name));
                await stream.CopyToAsync(fileStream)
                    .ConfigureAwait(false);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(folder));
        }
    }

    /// <inheritdoc />
    public override Task DeleteFileAsync(GameFolder folder, GameFileInfo file)
    {
        if (!CanStart)
            throw new InvalidOperationException("Server is running");
        switch (folder.Identifier)
        {
            case MpMissionsFolder:
            {
                if (!file.Name.EndsWith(".pbo", StringComparison.OrdinalIgnoreCase))
                    // ReSharper disable once LocalizableElement
                    throw new ArgumentException("Invalid file", nameof(file));
                if (file.Name.Contains('/', StringComparison.OrdinalIgnoreCase)
                    || file.Name.Contains('\\', StringComparison.OrdinalIgnoreCase))
                    // ReSharper disable once LocalizableElement
                    throw new ArgumentException("Invalid file", nameof(file));
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    Directory.CreateDirectory(MpMissionsPath, DefaultUnixFileMode);
                else
                    Directory.CreateDirectory(MpMissionsPath);
                if (File.Exists(Path.Combine(MpMissionsPath, file.Name)))
                    File.Delete(Path.Combine(MpMissionsPath, file.Name));
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(folder));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override async Task<string?> GetCommonConfigurationAsync(
        ECommonConfiguration commonConfig,
        CultureInfo cultureInfo,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await DbContextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        var gameServerId = GameServerPrimaryKey;
        var baseQuery = dbContext.ConfigurationEntries
            .Where((q) => q.IsActive)
            .Where((q) => q.GameServerFk == gameServerId);
        switch (commonConfig)
        {
            case ECommonConfiguration.Title:
                baseQuery = baseQuery
                    .Where((q) => q.Realm == RealmServerCfg)
                    .Where((q) => q.Path == "hostname");
                break;
            case ECommonConfiguration.Port:
                baseQuery = baseQuery
                    .Where((q) => q.Realm == RealmHost)
                    .Where((q) => q.Path == "port");
                break;
            case ECommonConfiguration.Password:
                baseQuery = baseQuery
                    .Where((q) => q.Realm == RealmServerCfg)
                    .Where((q) => q.Path == "password");
                break;
            default:
                return null;
        }

        return await baseQuery
            .Select((q) => q.Value)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private StreamWriter CreateBattlEyeConfigurationWriter()
    {
        CreateDirectory(BattlEyePath);

        Stream Create(string path)
        {
            var fileStreamOptions = new FileStreamOptions
            {
                Mode    = FileMode.Create,
                Access  = FileAccess.Write,
                Share   = FileShare.Read,
                Options = FileOptions.Asynchronous | FileOptions.WriteThrough,
            };
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                fileStreamOptions.UnixCreateMode = DefaultUnixFileMode;

            return new FileStream(path, fileStreamOptions);
        }

        return new StreamWriter(
            new MultiStream(new[] {Create(BattlEyeConfigurationPath), Create(BattlEyeConfigurationX64Path)}),
            Encoding.UTF8);
    }

    private StreamWriter CreateServerConfigurationWriter()
    {
        CreateDirectory(GameServerPath);
        var fileStreamOptions = new FileStreamOptions
        {
            Mode    = FileMode.Create,
            Access  = FileAccess.Write,
            Share   = FileShare.Read,
            Options = FileOptions.Asynchronous | FileOptions.WriteThrough,
        };
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            fileStreamOptions.UnixCreateMode = DefaultUnixFileMode;

        return new StreamWriter(
            ServerConfigurationPath,
            Encoding.UTF8,
            fileStreamOptions);
    }

    private StreamWriter CreateBasicConfigurationWriter()
    {
        CreateDirectory(GameServerPath);
        var fileStreamOptions = new FileStreamOptions
        {
            Mode    = FileMode.Create,
            Access  = FileAccess.Write,
            Share   = FileShare.Read,
            Options = FileOptions.Asynchronous | FileOptions.WriteThrough,
        };
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            fileStreamOptions.UnixCreateMode = DefaultUnixFileMode;

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

    private string BattlEyePath => Path.Combine(GameServerPath, "battl-eye");

    private string BattlEyeConfigurationPath => Path.Combine(BattlEyePath, "beserver.cfg");
    private string BattlEyeConfigurationX64Path => Path.Combine(BattlEyePath, "beserver_x64.cfg");
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
        var gameServerPk = GameServerPrimaryKey;
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
                RealmHost        => throw new Exception($"Query fault. Query should not return {group.Key} but did."),
                RealmBasicCfg    => CreateBasicConfigurationWriter(),
                RealmServerCfg   => CreateServerConfigurationWriter(),
                RealmBattlEyeCfg => CreateBattlEyeConfigurationWriter(),
                _                => throw new InvalidDataException($"Unknown realm {group.Key}"),
            };

            switch (group.Key)
            {
                default:
                    foreach (var configurationEntry in group
                                 .Where((q) => q.Value.IsNotNullOrEmpty())
                                 .OrderBy((q) => q.Path))
                    {
                        var splattedPath = configurationEntry.Path.Split('/');
                        var pathSegments = splattedPath.SkipLast(1).ToArray();
                        var actualPath = splattedPath.Last();
                        await WritePathSegmentsIfNeeded(streamWriter, pathSegments)
                            .ConfigureAwait(false);

                        var definition = entryDefinitions.GetValueOrDefault(
                            (configurationEntry.Realm, configurationEntry.Path));
                        var value = definition?.Kind switch
                        {
                            EConfigurationEntryKind.String or EConfigurationEntryKind.Password => ToArmaString(
                                configurationEntry.Value),
                            EConfigurationEntryKind.Boolean => configurationEntry.Value.FirstOrDefault()
                                    .ToLowerInvariant()
                                switch
                                {
                                    't' => "1",
                                    'f' => "0",
                                    _ => throw new InvalidDataException(
                                        $"Invalid boolean value {configurationEntry.Value}"),
                                },
                            _ => configurationEntry.Value,
                        };
                        await streamWriter.WriteLineAsync($"{Tab(pathSegments.Length)}{actualPath} = {value};");
                    }

                    break;
                case RealmBattlEyeCfg:
                    foreach (var configurationEntry in group
                                 .Where((q) => q.Value.IsNotNullOrEmpty())
                                 .OrderBy((q) => q.Path))
                    {
                        await streamWriter.WriteLineAsync($"{configurationEntry.Path} {configurationEntry.Value}");
                    }

                    break;
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
        var modList = await GetWorkshopPathsAsync(dbContext, true)
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
            WorkingDirectory       = GameInstallPath,
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

        CreateDirectory(BattlEyePath);
        psi.ArgumentList.Add($"-bepath={BattlEyePath}");

        CreateDirectory(ProfilesPath);
        psi.ArgumentList.Add($"-profiles={ProfilesPath}");
        var headlessClientIp = await GetAsync("host://headless-client-ip").ConfigureAwait(false);
        var headlessClientPassword = await GetAsync("host://headless-client-password").ConfigureAwait(false);
        if (headlessClientIp.IsNotNullOrWhiteSpace())
        {
            psi.ArgumentList.Add("-client");
            psi.ArgumentList.Add($"-connect={headlessClientIp}");
            if (headlessClientPassword.IsNotNullOrWhiteSpace())
            {
                psi.ArgumentList.Add($"-password={headlessClientPassword}");
            }
        }

        var port = await GetAsync("host://port", -1).ConfigureAwait(false);
        if (port > 0)
            psi.ArgumentList.Add($"-port={port.ToString()}");
        var serverMod = await GetAsync("host://serverMod").ConfigureAwait(false);
        if (serverMod.IsNotNullOrWhiteSpace())
            psi.ArgumentList.Add($"-serverMod={serverMod}");
        var additionalMods = await GetAsync("host://mod").ConfigureAwait(false);
        if (additionalMods.IsNotNullOrEmpty())
            modList = modList.Append(additionalMods).ToImmutableArray();

        if (modList.Any())
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var modsPath = Path.Combine(GameInstallPath, $"{GameServerPrimaryKey}-mods");

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    Directory.CreateDirectory(modsPath, DefaultUnixFileMode);
                else
                    Directory.CreateDirectory(modsPath);
                var symLinks = await CreateOrReplaceSymlinksForAsync(
                        modList,
                        modsPath)
                    .ConfigureAwait(false);
                modList = symLinks.Select((q) => q.PathRelativeTo(GameInstallPath)).ToImmutableArray();
            }

            psi.ArgumentList.Add($"-mod={string.Join(';', modList)}");
        }
        // ReSharper restore StringLiteralTypo

        return psi;
    }

    [SupportedOSPlatform("linux")]
    private Task<IReadOnlyCollection<string>> CreateOrReplaceSymlinksForAsync(
        IReadOnlyCollection<string> directories,
        string target)
    {
        var result = new List<string>(directories.Count);
        foreach (var directory in directories)
        {
            var relative = directory.PathRelativeTo(target);
            var directoryName = Path.GetFileName(directory);
            var file = Path.Combine(target, directoryName);
            var fileInfo = new FileInfo(file);
            result.Add(file);
            if (Directory.Exists(file) || fileInfo.Exists)
            {
                if (fileInfo.LinkTarget != relative)
                {
                    Logger.LogDebug(
                        "Deleting SymLink at {FilePath} pointing towards {SymLinkTarget}",
                        file,
                        fileInfo.LinkTarget);
                    fileInfo.Delete();
                }
                else
                {
                    Logger.LogTrace(
                        "Skipping {FilePath} as SymLink already points towards {SymLinkTarget}",
                        file,
                        relative);
                    continue;
                }
            }
            else
            {
                Logger.LogTrace(
                    "No file exists at {FilePath}, proceeding to create SymLink",
                    file);
            }

            Logger.LogDebug(
                "Creating SymLink at {FilePath} pointing towards {SymLinkTarget}",
                file,
                relative);
            fileInfo.CreateAsSymbolicLink(relative);
        }

        return Task.FromResult<IReadOnlyCollection<string>>(result.AsReadOnly());
    }

    /// <inheritdoc />
    protected override async Task OnInstallOrUpgradeAsync(User? executingUser)
    {
        await using var dbContext = await DbContextFactory.CreateDbContextAsync().ConfigureAwait(false);
        await UpdateWorkshopItemsOfActiveModPackAsync(dbContext, executingUser)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async Task SteamCmdGameUpdateInstructions(ProcessStartInfo psi)
    {
        var branch = await GetAsync("host://branch").ConfigureAwait(false);
        if (branch.IsNotNullOrEmpty())
        {
            psi.ArgumentList.Add("-beta");
            psi.ArgumentList.Add(branch);
        }
    }

    private async Task<IReadOnlyCollection<long>> GetWorkshopIdsAsync(ApiDbContext dbContext)
    {
        const string workshopBase = "https://steamcommunity.com/sharedfiles/filedetails/?id=";
        var modPackData = await dbContext.GameServers
            .Where((q) => q.PrimaryKey == GameServerPrimaryKey)
            .Select(
                (q) => new
                {
                    q.ActiveModPackFk,
                    q.SelectedModPackFk,
                })
            .SingleOrDefaultAsync()
            .ConfigureAwait(false);
        if (modPackData?.ActiveModPackFk is { } modPackRevisionId)
        {
            var modPackRevision = await dbContext.ModPackRevisions
                .SingleOrDefaultAsync((q) => q.PrimaryKey == modPackRevisionId)
                .ConfigureAwait(false);
            if (modPackRevision is null)
                throw new NullReferenceException(
                    $"Failed to receive mod pack revision with the id {modPackRevisionId} from database");

            var result = Arma3ModPackParser.FromHtml(modPackRevision.Html);
            var workshopIds = new List<long>();
            foreach (var (_, href) in result.Mods)
            {
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

        if (modPackData?.SelectedModPackFk is { } selectedModPackFk)
        {
            var modPackRevisions = await dbContext.ModPackRevisions
                .Where(
                    (modPackRevision) => modPackRevision.ModPackDefinitions!.Any(
                        (modPackDefinition) => modPackDefinition.PrimaryKey == selectedModPackFk))
                .ToArrayAsync();
            if (modPackRevisions is null)
                throw new NullReferenceException(
                    $"Failed to receive mod pack definition with the id {selectedModPackFk} from database");

            var workshopIds = new List<long>();
            foreach (var modPackRevision in modPackRevisions)
            {
                var result = Arma3ModPackParser.FromHtml(modPackRevision.Html);
                foreach (var (_, href) in result.Mods)
                {
                    if (!Uri.IsWellFormedUriString(href, UriKind.Absolute))
                        continue;
                    if (!href.StartsWith(workshopBase))
                        continue;
                    var workshopItemIdString = href[workshopBase.Length..];
                    if (!long.TryParse(workshopItemIdString, out var workshopItemId))
                        continue;
                    workshopIds.Add(workshopItemId);
                }
            }

            return workshopIds;
        }

        return Array.Empty<long>();
    }

    private async Task<IReadOnlyCollection<string>> GetWorkshopPathsAsync(
        ApiDbContext dbContext,
        bool lowercased)
    {
        var workshopIds = await GetWorkshopIdsAsync(dbContext).ConfigureAwait(false);
        return workshopIds.Select(
                (q) => Path.Combine(
                    GameServerPath,
                    "steamapps",
                    "workshop",
                    "content",
                    GameAppId.ToString(),
                    lowercased ? $"{q}-lowercased" : q.ToString()))
            .ToImmutableArray();
    }


    protected override void ProcessOnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data is not { } message)
            return;
        BeginWritingServerLog(LogLevel.Information, message);
    }

    private async Task UpdateWorkshopItemsOfActiveModPackAsync(
        ApiDbContext dbContext,
        User? executingUser)
    {
        var workshopItemIds = await GetWorkshopIdsAsync(dbContext).ConfigureAwait(false);
        await dbContext.GameServerLogs.AddAsync(
                new GameServerLog
                {
                    GameServerFk = GameServerPrimaryKey,
                    Source       = "GameServerUpdate",
                    Message      = $"Updating {workshopItemIds.Count} workshop items",
                    LogLevel     = LogLevel.Information,
                    TimeStamp    = DateTimeOffset.Now,
                })
            .ConfigureAwait(false);
        await dbContext.SaveChangesAsync()
            .ConfigureAwait(false);
        var workshopPaths = await DoUpdateWorkshopMods(
                workshopItemIds,
                GameServerPath,
                executingUser,
                RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? workshopItemIds.Count : 0)
            .ConfigureAwait(false);
        // ReSharper disable once InvertIf
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            foreach (var ((_, workshopPath), index) in workshopPaths.Indexed())
            {
                var lowerCaseWorkshopPath = $"{workshopPath}-lowercased";
                CopyAndReplaceFiles(workshopPath, lowerCaseWorkshopPath);
                LowercaseFiles(lowerCaseWorkshopPath);
                await UpdateStreamService.SendUpdateAsync(
                        $"{Constants.Routes.GameServers}/{GameServerPrimaryKey}/lifetime-status",
                        new Contract.UpdateStream.GameServer.LifetimeStatusHasChanged
                        {
                            LifetimeStatus = ELifetimeStatus.Updating,
                            GameServerId   = GameServerPrimaryKey,
                            Progress = index / (double)workshopItemIds.Count * 2,
                        })
                    .ConfigureAwait(false);
            }
        }
    }

    [SupportedOSPlatform("linux")]
    private void CopyAndReplaceFiles(string source, string target)
    {
        using var logScope = Logger.BeginScope(source);
        if (!Directory.Exists(target))
        {
            // Create directory
            var di = new DirectoryInfo(source).UnixFileMode;

            Logger.LogTrace("Creating directory {NewFilePath}", target);
            Directory.CreateDirectory(target, di);
        }

        var files = Directory.GetFiles(source, "*", SearchOption.TopDirectoryOnly);
        foreach (var fullFileName in files)
        {
            var fileName = fullFileName[source.Length..].TrimStart('/', '\\');
            var newFullFileName = Path.Combine(target, fileName);
            var fi = new FileInfo(fullFileName).UnixFileMode;
            if (File.Exists(newFullFileName))
            {
                Logger.LogTrace("Deleting file {FilePath}", newFullFileName);
                File.Delete(newFullFileName);
            }

            Logger.LogTrace("Copying file {FilePath} to {NewFilePath}", fullFileName, newFullFileName);
            File.Copy(fullFileName, newFullFileName);
            File.SetUnixFileMode(newFullFileName, fi);
        }

        var directories = Directory.GetDirectories(source, "*", SearchOption.TopDirectoryOnly);
        foreach (var fullDirectoryName in directories)
        {
            var directoryName = fullDirectoryName[source.Length..].TrimStart('/', '\\');
            var newFullDirectoryName = Path.Combine(target, directoryName);
            CopyAndReplaceFiles(fullDirectoryName, newFullDirectoryName);
        }
    }

    [SupportedOSPlatform("linux")]
    private void LowercaseFiles(string source)
    {
        using var logScope = Logger.BeginScope(source);
        var files = Directory.GetFiles(source, "*", SearchOption.TopDirectoryOnly);
        foreach (var fullFileName in files)
        {
            var fileName = fullFileName[source.Length..].TrimStart('/', '\\');
            var lowerCasedFileName = fileName.ToLower();
            var lowerCasedFullFileName = Path.Combine(source, lowerCasedFileName);
            if (fullFileName == lowerCasedFullFileName)
                continue;
            if (File.Exists(lowerCasedFullFileName))
            {
                Logger.LogTrace("Deleting file {FilePath}", lowerCasedFullFileName);
                File.Delete(lowerCasedFullFileName);
            }

            Logger.LogTrace("Moving file {FilePath} to {NewFilePath}", fullFileName, lowerCasedFullFileName);
            File.Move(fullFileName, lowerCasedFullFileName);
        }

        var directories = Directory.GetDirectories(source, "*", SearchOption.TopDirectoryOnly);
        foreach (var fullDirectoryName in directories)
        {
            var directoryName = fullDirectoryName[source.Length..].TrimStart('/', '\\');
            var lowerCasedDirectoryName = directoryName.ToLower();
            var lowerCasedFullDirectoryName = Path.Combine(source, lowerCasedDirectoryName);
            if (fullDirectoryName != lowerCasedFullDirectoryName)
            {
                if (Directory.Exists(lowerCasedFullDirectoryName))
                {
                    Logger.LogTrace("Deleting directory {DirectoryPath}", lowerCasedFullDirectoryName);
                    Directory.Delete(lowerCasedFullDirectoryName, true);
                }

                Logger.LogTrace(
                    "Moving file {FilePath} to {NewFilePath}",
                    fullDirectoryName,
                    lowerCasedFullDirectoryName);
                Directory.Move(fullDirectoryName, lowerCasedFullDirectoryName);
            }

            LowercaseFiles(lowerCasedFullDirectoryName);
        }
    }
}
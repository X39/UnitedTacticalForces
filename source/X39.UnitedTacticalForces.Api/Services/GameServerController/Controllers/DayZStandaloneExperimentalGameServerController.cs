using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.Data.Hosting;
using X39.UnitedTacticalForces.Api.Services.UpdateStreamService;

namespace X39.UnitedTacticalForces.Api.Services.GameServerController.Controllers;

/// <summary>
/// Implementation of <see cref="IGameServerController"/> for DayZ Standalone game servers.
/// </summary>
[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
public class DayZStandaloneExperimentalGameServerController : SteamGameServerControllerBase, IGameServerControllerCreatable
{
    /// <inheritdoc />
    public DayZStandaloneExperimentalGameServerController(
        GameServer gameServer,
        IDbContextFactory<ApiDbContext> dbContextFactory,
        ILogger<DayZStandaloneExperimentalGameServerController> logger,
        IConfiguration configuration,
        IUpdateStreamService updateStreamService)
        : base(configuration, gameServer, dbContextFactory, updateStreamService, logger)
    {
    }

    /// <inheritdoc />
    public override bool AllowAnyConfigurationEntry => false;

    /// <inheritdoc />
    public override bool CanModifyGameFiles => false;

    /// <inheritdoc />
    protected override long ServerAppId => Constants.Steam.AppId.DayZStandaloneExperimentalServer;

    /// <inheritdoc />
    protected override long GameAppId => Constants.Steam.AppId.DayZStandalone;

    /// <inheritdoc />
    protected override bool RequireLogin => true;

    /// <inheritdoc />
    protected override bool RequirePurchaseForWorkshop => true;

    /// <inheritdoc />
    public static string Identifier => $"dayz-experimental-{Constants.Steam.AppId.DayZStandaloneExperimentalServer}";

    /// <inheritdoc />
    public override IEnumerable<ConfigurationEntryDefinition> GetConfigurationEntryDefinitions(CultureInfo cultureInfo)
    {
        return Enumerable.Empty<ConfigurationEntryDefinition>();
    }

    /// <inheritdoc />
    public override Task<IEnumerable<GameFolder>> GetGameFoldersAsync(
        CultureInfo cultureInfo,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IEnumerable<GameFolder>>(Array.Empty<GameFolder>());
    }

    /// <inheritdoc />
    public override Task<IEnumerable<GameFileInfo>> GetGameFolderFilesAsync(
        GameFolder folder,
        CultureInfo cultureInfo,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IEnumerable<GameFileInfo>>(Array.Empty<GameFileInfo>());
    }

    /// <inheritdoc />
    public override Task<Stream> GetGameFolderFileAsync(
        GameFolder folder,
        GameFileInfo file,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<Stream>(new MemoryStream());
    }

    /// <inheritdoc />
    public override Task UploadFileAsync(GameFolder folder, GameFileInfo file, Stream stream)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override Task DeleteFileAsync(GameFolder folder, GameFileInfo file)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override Task<string?> GetCommonConfigurationAsync(
        ECommonConfiguration commonConfig,
        CultureInfo cultureInfo,
        CancellationToken cancellationToken = default)
    {
        return commonConfig switch
        {
            ECommonConfiguration.Title => Task.FromResult<string?>("DayZ Standalone Experimental"),
            ECommonConfiguration.Port => Task.FromResult<string?>("2302"),
            ECommonConfiguration.Password => Task.FromResult<string?>(""),
            _ => throw new ArgumentOutOfRangeException(nameof(commonConfig), commonConfig, null)
        };
    }

    /// <inheritdoc />
    protected override Task DoUpdateConfigurationAsync()
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override ValueTask<ProcessStartInfo> GetProcessStartInfoAsync(ApiDbContext dbContext, User? executingUser)
    {
        var fileName = Path.Combine(
            GameInstallPath,
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "DayZServer_x64.exe"
                : "DayZServer");
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
        return ValueTask.FromResult(psi);
    }

    /// <inheritdoc />
    public static Task<IGameServerController> CreateAsync(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        GameServer gameServer,
        IUpdateStreamService updateStreamService)
    {
        var controller = new DayZStandaloneExperimentalGameServerController(
            gameServer,
            serviceProvider.GetRequiredService<IDbContextFactory<ApiDbContext>>(),
            serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<DayZStandaloneExperimentalGameServerController>(),
            configuration,
            updateStreamService);
        return Task.FromResult<IGameServerController>(controller);
    }
}
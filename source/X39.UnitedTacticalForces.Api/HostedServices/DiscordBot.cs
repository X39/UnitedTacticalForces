using System.Globalization;
using System.Net.NetworkInformation;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Data.Hosting;
using X39.UnitedTacticalForces.Api.Properties;
using X39.UnitedTacticalForces.Api.Services.GameServerController;
using X39.UnitedTacticalForces.Common;
using X39.UnitedTacticalForces.Contract.GameServer;
using X39.Util;

namespace X39.UnitedTacticalForces.Api.HostedServices;

public class DiscordBot : BackgroundService, IAsyncDisposable
{
    private readonly ILogger<DiscordBot>             _logger;
    private readonly IConfiguration                  _configuration;
    private readonly IGameServerControllerFactory    _gameServerControllerFactory;
    private readonly IDbContextFactory<ApiDbContext> _dbContextFactory;
    private readonly DiscordSocketClient             _discordSocketClient;

    public DiscordBot(
        ILogger<DiscordBot> logger,
        IConfiguration configuration,
        IGameServerControllerFactory gameServerControllerFactory,
        IDbContextFactory<ApiDbContext> dbContextFactory)
    {
        _logger                      = logger;
        _configuration               = configuration;
        _gameServerControllerFactory = gameServerControllerFactory;
        _dbContextFactory            = dbContextFactory;
        _discordSocketClient         = new DiscordSocketClient();
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _discordSocketClient.Log += (logMessage) =>
        {
#pragma warning disable CA2254
            // ReSharper disable TemplateIsNotCompileTimeConstantProblem
            _logger.Log(
                logMessage.Severity switch
                {
                    LogSeverity.Critical => LogLevel.Critical,
                    LogSeverity.Error    => LogLevel.Error,
                    LogSeverity.Warning  => LogLevel.Warning,
                    LogSeverity.Info     => LogLevel.Information,
                    LogSeverity.Verbose  => LogLevel.Debug,
                    LogSeverity.Debug    => LogLevel.Trace,
                    _                    => throw new ArgumentOutOfRangeException(),
                },
                logMessage.Exception,
                logMessage.Message ?? string.Empty);
            // ReSharper restore TemplateIsNotCompileTimeConstantProblem
#pragma warning restore CA2254
            return Task.CompletedTask;
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // ReSharper disable once AccessToDisposedClosure
                await Fault.IgnoreAsync(async () => await _discordSocketClient.LogoutAsync().ConfigureAwait(false))
                    .ConfigureAwait(false);
                _logger.LogInformation("Logging in to Discord");
                await _discordSocketClient.LoginAsync(
                        TokenType.Bot,
                        _configuration[Constants.Configuration.Discord.Bot.BotToken])
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while logging in to Discord, retrying in 60 seconds");
                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken)
                    .ConfigureAwait(false);
                continue;
            }

            try
            {
                _logger.LogInformation("Registering DiscordBot events");
                _discordSocketClient.SlashCommandExecuted += DiscordSocketClientOnSlashCommandExecuted;
                _discordSocketClient.GuildAvailable       += DiscordSocketClientOnGuildAvailable;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while logging in to Discord, retrying in 60 seconds");
                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken)
                    .ConfigureAwait(false);
                continue;
            }

            try
            {
                _logger.LogInformation("Starting DiscordBot");
                await _discordSocketClient.StartAsync()
                    .ConfigureAwait(false);
                await Task.Delay(-1, stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while executing DiscordBot");
            }
            finally
            {
                await Fault.IgnoreAsync(
                    async () => await _discordSocketClient.StopAsync()
                        .ConfigureAwait(false));
            }
        }
    }

    private async Task DiscordSocketClientOnGuildAvailable(SocketGuild arg)
    {
        _logger.LogInformation("Guid {GuildId} ({GuildName}) became available", arg.Id, arg.Name);
        var commands = await arg.GetApplicationCommandsAsync()
            .ConfigureAwait(false);
        var dictionary = new Dictionary<string, Func<Task>>
        {
            [Constants.Discord.Commands.Teamspeak] = async () => await arg.CreateApplicationCommandAsync(
                new SlashCommandBuilder()
                    .WithName(Constants.Discord.Commands.Teamspeak)
                    .WithDescription("Display the teamspeak information.")
                    .WithNsfw(false)
                    .WithDescriptionLocalizations(
                        new Dictionary<string, string>
                        {
                            ["en-US"] = "Show teamspeak information.",
                            ["en-GB"] = "Display the teamspeak information.",
                            ["de"]    = "Zeigt die Teamspeak Informationen.",
                        })
                    .Build()),
            [Constants.Discord.Commands.GameServers] = async () => await arg.CreateApplicationCommandAsync(
                new SlashCommandBuilder()
                    .WithName(Constants.Discord.Commands.GameServers)
                    .WithDescription("Display the game server information.")
                    .WithNsfw(false)
                    .WithDescriptionLocalizations(
                        new Dictionary<string, string>
                        {
                            ["en-US"] = "Show game server information.",
                            ["en-GB"] = "Display the game server information.",
                            ["de"]    = "Zeigt die Spieleserver Informationen.",
                        })
                    .Build()),
        };
        _logger.LogInformation("Checking if all commands are registered");
        var set = commands.Select((q) => q.Name).ToHashSet();
        var registerAll = false;
        if (set.Any(commandName => !dictionary.ContainsKey(commandName)))
        {
            _logger.LogInformation("One or more commands are not existing anymore, deleting all commands");
            await arg.DeleteApplicationCommandsAsync(
                    new RequestOptions {AuditLogReason = "One or more commands are not existing anymore"})
                .ConfigureAwait(false);
            registerAll = true;
        }

        foreach (var (key, func) in dictionary)
        {
            if (!registerAll && set.Contains(key))
                continue;
            _logger.LogInformation("Registering command {CommandName}", key);
            await func().ConfigureAwait(false);
        }
    }

    private async Task DiscordSocketClientOnSlashCommandExecuted(SocketSlashCommand arg)
    {
        _logger.LogInformation("Executing command {CommandName} ({@Command})", arg.CommandName, arg);
        switch (arg.CommandName.ToLower())
        {
            case Constants.Discord.Commands.Teamspeak:
                await DiscordCommandTeamSpeak(arg)
                    .ConfigureAwait(false);
                break;
            case Constants.Discord.Commands.GameServers:
                await DiscordCommandGameServers(arg)
                    .ConfigureAwait(false);
                break;
            default:
                _logger.LogWarning("Command {CommandName} is not implemented", arg.CommandName);
                await arg.RespondAsync("Unknown command")
                    .ConfigureAwait(false);
                break;
        }
    }

    private Color GetEmbedColor()
    {
        var colorString = _configuration[Constants.Configuration.Discord.Bot.EmbedColor]?.TrimStart('#') ?? "FFFFFF";
        if (uint.TryParse(colorString, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var colorInt))
            return new Color(colorInt);
        return new Color();
    }

    private async Task DiscordCommandTeamSpeak(SocketSlashCommand socketSlashCommand)
    {
        var cultureInfo = new CultureInfo(socketSlashCommand.UserLocale);
        var embedBuilder = new EmbedBuilder();
        embedBuilder.WithColor(GetEmbedColor());
        if (_configuration[Constants.Configuration.TeamSpeak.Host] is { } ip
            && ip.IsNotNullOrWhiteSpace())
            embedBuilder.AddField(
                Language.ResourceManager.GetString(nameof(Language.DiscordCommand_TeamSpeak_IpAddress), cultureInfo),
                ip,
                true);
        else
        {
            await socketSlashCommand.RespondAsync(
                    Language.ResourceManager.GetString(
                        nameof(Language.DiscordCommand_TeamSpeak_NotConfigured),
                        cultureInfo))
                .ConfigureAwait(false);
            return;
        }

        if (_configuration[Constants.Configuration.TeamSpeak.Port] is { } portString
            && ushort.TryParse(portString, out var port))
            embedBuilder.AddField(
                Language.ResourceManager.GetString(nameof(Language.DiscordCommand_TeamSpeak_Port), cultureInfo),
                port,
                true);
        else
            port = 9987;
        if (_configuration[Constants.Configuration.TeamSpeak.Password] is { } password
            && password.IsNotNullOrWhiteSpace())
            embedBuilder.AddField(
                Language.ResourceManager.GetString(nameof(Language.DiscordCommand_TeamSpeak_Password), cultureInfo),
                password,
                true);
        else
            password = null;
        await socketSlashCommand.RespondAsync(
                null,
                embedBuilder
                    .Build()
                    .MakeArray())
            .ConfigureAwait(false);
    }

    private async Task DiscordCommandGameServers(SocketSlashCommand socketSlashCommand)
    {
        var cultureInfo = new CultureInfo(socketSlashCommand.UserLocale);
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync().ConfigureAwait(false);
        var embedBuilder = new EmbedBuilder();
        await foreach (var gameServer in dbContext.GameServers
                           .Include((e) => e.ConfigurationEntries!.Where((q) => q.Path == "host//port"))
                           .AsAsyncEnumerable()
                           .ConfigureAwait(false))
        {
            var gameServerController = await _gameServerControllerFactory.GetGameControllerAsync(gameServer)
                .ConfigureAwait(false);
            embedBuilder.AddField(
                Language.ResourceManager.GetString(nameof(Language.DiscordCommand_GameServers_Title), cultureInfo),
                gameServer.Title);
            embedBuilder.AddField(
                Language.ResourceManager.GetString(nameof(Language.DiscordCommand_GameServers_Status), cultureInfo),
                gameServer.Status switch
                {
                    ELifetimeStatus.Stopped  => ":red_square:",
                    ELifetimeStatus.Starting => ":yellow_square:",
                    ELifetimeStatus.Stopping => ":yellow_square:",
                    ELifetimeStatus.Running  => ":green_square:",
                    _                        => throw new ArgumentOutOfRangeException(),
                },
                true);
            if (await gameServerController.GetCommonConfigurationAsync(ECommonConfiguration.Title, cultureInfo) is
                    { } title
                && title != gameServer.Title)
                embedBuilder.AddField(
                    Language.ResourceManager.GetString(
                        nameof(Language.DiscordCommand_GameServers_Hostname),
                        cultureInfo),
                    title,
                    true);
            embedBuilder.AddField(
                Language.ResourceManager.GetString(nameof(Language.DiscordCommand_GameServers_IpAddress), cultureInfo),
                _configuration[Constants.Configuration.General.GameServerHostAddress] ?? "unset",
                true);
            if (await gameServerController.GetCommonConfigurationAsync(ECommonConfiguration.Port, cultureInfo) is
                { } port)
                embedBuilder.AddField(
                    Language.ResourceManager.GetString(nameof(Language.DiscordCommand_GameServers_Port), cultureInfo),
                    port,
                    true);
            if (await gameServerController.GetCommonConfigurationAsync(ECommonConfiguration.Password, cultureInfo) is
                { } password)
                embedBuilder.AddField(
                    Language.ResourceManager.GetString(
                        nameof(Language.DiscordCommand_GameServers_Password),
                        cultureInfo),
                    password,
                    true);
        }

        await socketSlashCommand.RespondAsync(embeds: embedBuilder.Build().MakeArray())
            .ConfigureAwait(false);
    }

    /// <summary>
    ///    Executes the given action with the <see cref="DiscordSocketClient"/> if the client is connected.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    public async Task WithDiscordAsync(Func<DiscordSocketClient, Task> action)
    {
        if (_discordSocketClient.ConnectionState is ConnectionState.Connected)
        {
            await action(_discordSocketClient)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _discordSocketClient.DisposeAsync()
            .ConfigureAwait(false);
    }
}
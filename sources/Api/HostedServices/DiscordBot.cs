using System.Globalization;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using X39.UnitedTacticalForces.Api.Properties;
using X39.UnitedTacticalForces.Api.Services.Notifications;
using X39.UnitedTacticalForces.Common;
using X39.Util;

namespace X39.UnitedTacticalForces.Api.HostedServices;

public class DiscordBot : BackgroundService, IAsyncDisposable
{
    private readonly ILogger<DiscordBot>             _logger;
    private readonly IConfiguration                  _configuration;
    private readonly DiscordSocketClient             _discordSocketClient;

    public DiscordBot(
        ILogger<DiscordBot> logger,
        IConfiguration configuration)
    {
        _logger              = logger;
        _configuration       = configuration;
        _discordSocketClient = new DiscordSocketClient();
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!(_configuration[Constants.Configuration.Discord.Enabled]?.ToBoolean() ?? true))
            return;
        _discordSocketClient.Log += logMessage =>
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
        };
        _logger.LogInformation("Checking if all commands are registered");
        var set = commands.Select(q => q.Name).ToHashSet();
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
        await socketSlashCommand.RespondAsync(
                null,
                embedBuilder
                    .Build()
                    .MakeArray())
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

    /// <summary>
    ///    Attempts to send a direct message to a Discord user and reports a truthful outcome.
    /// </summary>
    /// <remarks>
    ///    <see cref="WithDiscordAsync"/> is intentionally not used: it silently does nothing while the client is not
    ///    connected, which would let a delivery be recorded as sent although no message left the process.
    /// </remarks>
    /// <param name="discordUserId">The Discord snowflake of the recipient.</param>
    /// <param name="text">The message text. Mentions in it never notify anyone.</param>
    /// <param name="cancellationToken">Cancels the attempt.</param>
    /// <returns>
    ///    <see cref="EDeliveryAttemptOutcome.NotConfigured"/> while the bot is disabled,
    ///    <see cref="EDeliveryAttemptOutcome.Transient"/> while the client is not connected or on an unexpected
    ///    error, <see cref="EDeliveryAttemptOutcome.Permanent"/> when the user cannot receive direct messages,
    ///    and <see cref="EDeliveryAttemptOutcome.Sent"/> only after Discord accepted the message.
    /// </returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    internal async Task<DeliveryAttemptResult> TrySendDirectMessageAsync(
        ulong discordUserId,
        string text,
        CancellationToken cancellationToken)
    {
        if (!(_configuration[Constants.Configuration.Discord.Enabled]?.ToBoolean() ?? true))
            return DeliveryAttemptResult.NotConfigured("discord-disabled");
        if (_discordSocketClient.ConnectionState is not ConnectionState.Connected)
            return DeliveryAttemptResult.Transient("discord-not-connected");

        try
        {
            var requestOptions = new RequestOptions {CancelToken = cancellationToken};
            var user = await _discordSocketClient.GetUserAsync(discordUserId, requestOptions)
                .ConfigureAwait(false);
            if (user is null)
                return DeliveryAttemptResult.Permanent("discord-user-not-found");
            await user.SendMessageAsync(text, allowedMentions: AllowedMentions.None, options: requestOptions)
                .ConfigureAwait(false);
            return DeliveryAttemptResult.Sent();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpException ex) when (ex.DiscordCode
                                           is DiscordErrorCode.CannotSendMessageToUser
                                           or DiscordErrorCode.CannotSendMessagesToThisUserDueToHavingNoMutualGuilds)
        {
            _logger.LogWarning(
                "Discord refused a direct message ({DiscordCode})",
                ex.DiscordCode);
            return DeliveryAttemptResult.Permanent("discord-dm-blocked");
        }
        catch (Exception ex)
        {
            // Neither the message text nor the bot token may reach the log.
            _logger.LogWarning(
                "Sending a Discord direct message failed ({ExceptionType}, {DiscordCode})",
                ex.GetType().Name,
                (ex as HttpException)?.DiscordCode);
            return DeliveryAttemptResult.Transient("discord-error");
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _discordSocketClient.DisposeAsync()
            .ConfigureAwait(false);
    }
}

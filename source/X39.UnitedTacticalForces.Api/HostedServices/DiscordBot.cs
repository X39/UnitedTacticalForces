using System.Globalization;
using Discord;
using Discord.WebSocket;
using X39.UnitedTacticalForces.Api.Properties;
using X39.UnitedTacticalForces.Common;
using X39.Util;

namespace X39.UnitedTacticalForces.Api.HostedServices;

public class DiscordBot : BackgroundService, IAsyncDisposable
{
    private readonly ILogger<DiscordBot> _logger;
    private readonly IConfiguration      _configuration;
    private readonly DiscordSocketClient _discordSocketClient;

    public DiscordBot(ILogger<DiscordBot> logger, IConfiguration configuration)
    {
        _logger              = logger;
        _configuration       = configuration;
        _discordSocketClient = new DiscordSocketClient();
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
                _discordSocketClient.SlashCommandExecuted += DiscordSocketClientOnSlashCommandExecuted;
                await _discordSocketClient.CreateGlobalApplicationCommandAsync(
                    new UserCommandProperties
                    {
                        Name   = "ts",
                        IsNsfw = false,
                        DescriptionLocalizations = new Dictionary<string, string>
                        {
                            ["en"] = "Shows the teamspeak url",
                            ["de"] = "Zeigt die Teamspeak URL",
                        },
                    });
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

    private async Task DiscordSocketClientOnSlashCommandExecuted(SocketSlashCommand arg)
    {
        switch (arg.CommandName.ToLower())
        {
            case "ts":
                await DiscordCommandTeamSpeak(arg)
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
        var componentBuilder = new ComponentBuilder()
            .WithButton(
                Language.ResourceManager.GetString(nameof(Language.DiscordCommand_TeamSpeak_JoinButton), cultureInfo),
                url: new TS3AddressBuilder
                {
                    Host            = ip ?? string.Empty,
                    Port            = port,
                    Password        = password,
                    Channel         = _configuration[Constants.Configuration.TeamSpeak.Channel],
                    ChannelPassword = _configuration[Constants.Configuration.TeamSpeak.ChannelPassword],
                });
        await socketSlashCommand.RespondAsync(
                null,
                embedBuilder
                    .Build()
                    .MakeArray(),
                components: componentBuilder.Build())
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
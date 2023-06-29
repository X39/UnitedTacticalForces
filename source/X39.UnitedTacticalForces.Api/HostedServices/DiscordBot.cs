using Discord;
using Discord.WebSocket;
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
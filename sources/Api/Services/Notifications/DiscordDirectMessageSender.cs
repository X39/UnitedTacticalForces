using X39.UnitedTacticalForces.Api.HostedServices;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.Notifications;

/// <summary>
/// Delivers direct messages through the existing <see cref="DiscordBot" />.
/// </summary>
/// <param name="discordBot">The bot that owns the Discord connection.</param>
[Singleton<DiscordDirectMessageSender, IDiscordDirectMessageSender>]
internal sealed class DiscordDirectMessageSender(DiscordBot discordBot) : IDiscordDirectMessageSender
{
    /// <inheritdoc />
    public Task<DeliveryAttemptResult> SendAsync(
        ulong discordUserId,
        string text,
        CancellationToken cancellationToken)
        => discordBot.TrySendDirectMessageAsync(discordUserId, text, cancellationToken);
}

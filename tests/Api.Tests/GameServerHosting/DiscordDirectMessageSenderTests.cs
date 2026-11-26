using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using X39.UnitedTacticalForces.Api.HostedServices;
using X39.UnitedTacticalForces.Api.Services.Notifications;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// Proves that the Discord direct message adapter reports a truthful outcome and never claims success while the
/// bot is disabled or disconnected.
/// </summary>
public sealed class DiscordDirectMessageSenderTests
{
    private static DiscordBot CreateBot(string? enabled)
    {
        var values = new Dictionary<string, string?>();
        if (enabled is not null)
            values["Discord:Enabled"] = enabled;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new DiscordBot(NullLogger<DiscordBot>.Instance, configuration);
    }

    /// <summary>
    /// A disabled bot reports <see cref="EDeliveryAttemptOutcome.NotConfigured" />.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task Send_BotDisabled_ReportsNotConfigured()
    {
        await using var bot = CreateBot("false");
        IDiscordDirectMessageSender sender = new DiscordDirectMessageSender(bot);

        var result = await sender.SendAsync(1UL, "text", CancellationToken.None);

        Assert.Equal(EDeliveryAttemptOutcome.NotConfigured, result.Outcome);
    }

    /// <summary>
    /// An enabled bot whose client is not connected reports <see cref="EDeliveryAttemptOutcome.Transient" />, never
    /// <see cref="EDeliveryAttemptOutcome.Sent" />.
    /// </summary>
    /// <param name="enabled">The configured Discord:Enabled value, or <see langword="null" /> when absent.</param>
    /// <returns>A task completing once the assertions ran.</returns>
    [Theory]
    [InlineData("true")]
    [InlineData(null)]
    public async Task Send_BotNotConnected_ReportsTransient(string? enabled)
    {
        await using var bot = CreateBot(enabled);
        IDiscordDirectMessageSender sender = new DiscordDirectMessageSender(bot);

        var result = await sender.SendAsync(1UL, "text", CancellationToken.None);

        Assert.Equal(EDeliveryAttemptOutcome.Transient, result.Outcome);
        Assert.Equal("discord-not-connected", result.Reason);
    }
}

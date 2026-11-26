using System.Net.Sockets;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;
using X39.UnitedTacticalForces.Api.Services.Notifications;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// Proves that the SMTP email sender is cleanly unavailable while unconfigured, builds a deterministic and
/// injection-safe message, classifies failures to fixed reason codes and never leaks configuration values.
/// </summary>
public sealed class SmtpEmailSenderTests
{
    private static EmailOptions ConfiguredOptions()
        => new()
        {
            Host            = "smtp.secret-host.example",
            FromAddress     = "alerts@utf.example",
            FromName        = "United Tactical Forces",
            Username        = "secret-user",
            Password        = "secret-password",
        };

    private static SmtpEmailSender CreateSender(EmailOptions options)
        => new(Options.Create(options), NullLogger<SmtpEmailSender>.Instance);

    /// <summary>
    /// A section without host or sender address is not configured.
    /// </summary>
    /// <param name="host">The configured host.</param>
    /// <param name="from">The configured sender address.</param>
    [Theory]
    [InlineData(null, "alerts@utf.example")]
    [InlineData("", "alerts@utf.example")]
    [InlineData("smtp.example", null)]
    [InlineData("smtp.example", "  ")]
    public async Task SendAsync_WithoutHostOrFromAddress_ReportsNotConfigured(string? host, string? from)
    {
        var sender = CreateSender(new EmailOptions {Host = host, FromAddress = from});

        var result = await sender.SendAsync(
            new EmailMessage("to@utf.example", "Subject", "Body", "id@utf.example"),
            CancellationToken.None);

        Assert.False(sender.IsConfigured);
        Assert.Equal(EDeliveryAttemptOutcome.NotConfigured, result.Outcome);
        Assert.Equal("not-configured", result.Reason);
    }

    /// <summary>
    /// A configured section with unusable values reports <c>invalid-configuration</c> without a connection attempt.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task SendAsync_WithInvalidConfiguration_ReportsNotConfigured()
    {
        var options = ConfiguredOptions();
        options.Port = 0;
        var sender = CreateSender(options);

        var result = await sender.SendAsync(
            new EmailMessage("to@utf.example", "Subject", "Body", "id@utf.example"),
            CancellationToken.None);

        Assert.Equal(EDeliveryAttemptOutcome.NotConfigured, result.Outcome);
        Assert.Equal("invalid-configuration", result.Reason);
    }

    /// <summary>
    /// A recipient address that cannot be parsed is a permanent failure.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task SendAsync_WithUnparsableRecipient_ReportsPermanent()
    {
        var sender = CreateSender(ConfiguredOptions());

        var result = await sender.SendAsync(
            new EmailMessage("not an address", "Subject", "Body", "id@utf.example"),
            CancellationToken.None);

        Assert.Equal(EDeliveryAttemptOutcome.Permanent, result.Outcome);
        Assert.Equal("invalid-recipient", result.Reason);
    }

    /// <summary>
    /// The MIME message carries the sender, recipient, Message-Id and a plain-text body, and its subject has no
    /// line break even when the input has.
    /// </summary>
    [Fact]
    public void BuildMimeMessage_SetsHeadersAndPlainBody()
    {
        var message = SmtpEmailSender.BuildMimeMessage(
            ConfiguredOptions(),
            new EmailMessage("to@utf.example", "Cost\r\nBcc: x", "Hello body", "alert.user@utf.example"));

        var from = Assert.IsType<MailboxAddress>(Assert.Single(message.From));
        var to = Assert.IsType<MailboxAddress>(Assert.Single(message.To));
        Assert.Equal("United Tactical Forces", from.Name);
        Assert.Equal("alerts@utf.example", from.Address);
        Assert.Equal("to@utf.example", to.Address);
        Assert.Equal("alert.user@utf.example", message.MessageId);
        Assert.Equal("Cost Bcc: x", message.Subject);
        Assert.DoesNotContain('\r', message.Subject);
        Assert.DoesNotContain('\n', message.Subject);
        var body = Assert.IsType<TextPart>(message.Body);
        Assert.True(body.IsPlain);
        Assert.Equal("Hello body", body.Text);
    }

    /// <summary>
    /// The Message-Id is built from alert and recipient ids and the sender domain, and repeated calls agree.
    /// </summary>
    [Fact]
    public void CreateMessageId_IsDeterministicAndUsesFromDomain()
    {
        var alertId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var sender = CreateSender(ConfiguredOptions());

        var first = sender.CreateMessageId(alertId, recipientId);
        var second = sender.CreateMessageId(alertId, recipientId);

        Assert.Equal($"{alertId:N}.{recipientId:N}@utf.example", first);
        Assert.Equal(first, second);
    }

    /// <summary>
    /// An explicit Message-Id domain wins over the sender domain.
    /// </summary>
    [Fact]
    public void CreateMessageId_PrefersConfiguredDomain()
    {
        var options = ConfiguredOptions();
        options.MessageIdDomain = "ids.utf.example";
        var alertId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();

        var id = CreateSender(options).CreateMessageId(alertId, recipientId);

        Assert.Equal($"{alertId:N}.{recipientId:N}@ids.utf.example", id);
    }

    /// <summary>
    /// A 5xx command failure is permanent and a 4xx command failure is transient, each with the status in the reason.
    /// </summary>
    /// <param name="status">The SMTP status code.</param>
    /// <param name="expectedOutcome">The expected outcome name.</param>
    /// <param name="expectedReason">The expected reason code.</param>
    [Theory]
    [InlineData(SmtpStatusCode.MailboxUnavailable, "Permanent", "smtp-rejected-550")]
    [InlineData(SmtpStatusCode.MailboxBusy, "Transient", "smtp-deferred-450")]
    [InlineData((SmtpStatusCode) 451, "Transient", "smtp-deferred-451")]
    public void Classify_CommandException_UsesStatusClass(
        SmtpStatusCode status,
        string expectedOutcome,
        string expectedReason)
    {
        var exception = new SmtpCommandException(SmtpErrorCode.MessageNotAccepted, status, "secret@host.example failed");

        var result = SmtpFailureClassifier.Classify(exception);

        Assert.Equal(expectedOutcome, result.Outcome.ToString());
        Assert.Equal(expectedReason, result.Reason);
    }

    /// <summary>
    /// Network, protocol, timeout and authentication failures are transient with fixed reasons, and unknown failures
    /// fall back to <c>smtp-error</c>.
    /// </summary>
    /// <param name="kind">Which exception to create.</param>
    /// <param name="expectedReason">The expected reason code.</param>
    [Theory]
    [InlineData("protocol", "smtp-unreachable")]
    [InlineData("io", "smtp-unreachable")]
    [InlineData("socket", "smtp-unreachable")]
    [InlineData("timeout", "smtp-unreachable")]
    [InlineData("auth", "smtp-authentication")]
    [InlineData("other", "smtp-error")]
    public void Classify_OtherFailures_AreTransientWithFixedReason(string kind, string expectedReason)
    {
        Exception exception = kind switch
        {
            "protocol" => new SmtpProtocolException("secret-password"),
            "io"       => new IOException("secret-password"),
            "socket"   => new SocketException(),
            "timeout"  => new TimeoutException("secret-password"),
            "auth"     => new AuthenticationException("secret-password"),
            _          => new InvalidOperationException("secret-password"),
        };

        var result = SmtpFailureClassifier.Classify(exception);

        Assert.Equal(EDeliveryAttemptOutcome.Transient, result.Outcome);
        Assert.Equal(expectedReason, result.Reason);
        Assert.DoesNotContain("secret", result.Reason);
    }

    /// <summary>
    /// A configured section with port 0, an out-of-range port, a non-positive timeout or an unparsable sender
    /// address fails validation, and the message contains none of the configured values.
    /// </summary>
    /// <param name="problem">Which setting is invalid.</param>
    [Theory]
    [InlineData("port-zero")]
    [InlineData("port-high")]
    [InlineData("timeout")]
    [InlineData("from")]
    public void Validate_InvalidSettings_ThrowsWithoutLeakingValues(string problem)
    {
        var options = ConfiguredOptions();
        switch (problem)
        {
            case "port-zero": options.Port = 0; break;
            case "port-high": options.Port = 70000; break;
            case "timeout": options.Timeout = TimeSpan.Zero; break;
            default: options.FromAddress = "not an address"; break;
        }

        var exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.DoesNotContain("secret-host", exception.Message);
        Assert.DoesNotContain("secret-user", exception.Message);
        Assert.DoesNotContain("secret-password", exception.Message);
        Assert.DoesNotContain("not an address", exception.Message);
    }

    /// <summary>
    /// Valid and unconfigured sections pass validation.
    /// </summary>
    [Fact]
    public void Validate_ValidOrUnconfigured_DoesNotThrow()
    {
        ConfiguredOptions().Validate();
        new EmailOptions {Port = 0}.Validate();
    }
}

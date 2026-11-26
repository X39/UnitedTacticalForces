using MailKit;
using MailKit.Net.Smtp;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using MimeKit;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.Notifications;

/// <summary>
/// Sends plain-text emails over SMTP with MailKit. Certificate validation stays at the library default, and logs
/// carry only the outcome and a fixed reason code, never an address, subject, host or credential.
/// </summary>
/// <param name="options">The <c>Email:Smtp</c> settings.</param>
/// <param name="logger">The logger.</param>
[Singleton<SmtpEmailSender, IEmailSender>]
internal sealed partial class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private const string FallbackMessageIdDomain = "localhost.invalid";

    private int _invalidConfigurationLogged;

    /// <inheritdoc />
    public bool IsConfigured => options.Value.IsConfigured;

    /// <inheritdoc />
    public string CreateMessageId(Guid costAlertId, Guid recipientUserId)
        => $"{costAlertId:N}.{recipientUserId:N}@{ResolveMessageIdDomain(options.Value)}";

    /// <inheritdoc />
    public async Task<DeliveryAttemptResult> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var settings = options.Value;
        if (!settings.IsConfigured)
            return DeliveryAttemptResult.NotConfigured("not-configured");
        try
        {
            settings.Validate();
        }
        catch (InvalidOperationException)
        {
            if (Interlocked.Exchange(ref _invalidConfigurationLogged, 1) == 0)
                logger.LogError("The Email:Smtp settings are invalid, so email delivery is skipped");
            return DeliveryAttemptResult.NotConfigured("invalid-configuration");
        }

        if (!MailboxAddress.TryParse(message.ToAddress, out _))
            return DeliveryAttemptResult.Permanent("invalid-recipient");

        try
        {
            var mimeMessage = BuildMimeMessage(settings, message);
            using var client = new SmtpClient();
            client.Timeout = (int) Math.Min(settings.Timeout.TotalMilliseconds, int.MaxValue);
            await client.ConnectAsync(settings.Host!, settings.Port, settings.SecureSocket, cancellationToken)
                .ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(settings.Username))
            {
                await client.AuthenticateAsync(settings.Username, settings.Password ?? string.Empty, cancellationToken)
                    .ConfigureAwait(false);
            }

            await client.SendAsync(new SendRequest(mimeMessage), cancellationToken)
                .ConfigureAwait(false);
            await DisconnectQuietlyAsync(client).ConfigureAwait(false);
            return DeliveryAttemptResult.Sent();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var result = SmtpFailureClassifier.Classify(exception);
            logger.LogWarning(
                "Email delivery attempt ended with {Outcome} ({Reason})",
                result.Outcome,
                result.Reason);
            return result;
        }
    }

    /// <summary>
    /// Builds the MIME message of one email.
    /// </summary>
    /// <param name="options">The SMTP settings that provide the sender.</param>
    /// <param name="message">The email to convert.</param>
    /// <returns>A plain-text message with the given Message-Id and a subject free of line breaks.</returns>
    /// <exception cref="FormatException">The recipient or sender address cannot be parsed.</exception>
    internal static MimeMessage BuildMimeMessage(EmailOptions options, EmailMessage message)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(message);
        var mimeMessage = new MimeMessage
        {
            Subject   = RemoveLineBreaks(message.Subject),
            MessageId = message.MessageId,
            Body      = new TextPart("plain") {Text = message.Body},
        };
        mimeMessage.From.Add(new MailboxAddress(RemoveLineBreaks(options.FromName), options.FromAddress));
        mimeMessage.To.Add(MailboxAddress.Parse(message.ToAddress));
        return mimeMessage;
    }

    private static string ResolveMessageIdDomain(EmailOptions settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.MessageIdDomain))
            return settings.MessageIdDomain.Trim();
        if (!string.IsNullOrWhiteSpace(settings.FromAddress)
            && MailboxAddress.TryParse(settings.FromAddress, out var from)
            && from is MailboxAddress {Domain.Length: > 0} mailbox)
        {
            return mailbox.Domain;
        }

        return FallbackMessageIdDomain;
    }

    private static string RemoveLineBreaks(string value)
        => LineBreakRegex().Replace(value, " ");

    [GeneratedRegex("[\\r\\n]+")]
    private static partial Regex LineBreakRegex();

    private static async Task DisconnectQuietlyAsync(SmtpClient client)
    {
        try
        {
            await client.DisconnectAsync(true, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The message was already accepted; a failing QUIT must not turn Sent into a failure.
        }
    }
}

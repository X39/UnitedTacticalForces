using System.Net.Sockets;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace X39.UnitedTacticalForces.Api.Services.Notifications;

/// <summary>
/// Reduces an SMTP failure to a delivery outcome and a fixed reason code. The exception message is never copied,
/// because it can contain addresses, host names or credentials.
/// </summary>
internal static class SmtpFailureClassifier
{
    /// <summary>
    /// Classifies a failure that happened while talking to an SMTP server.
    /// </summary>
    /// <param name="exception">The failure.</param>
    /// <returns>
    /// A permanent result for a 5xx server reply, otherwise a transient result. The reason is one of
    /// <c>smtp-rejected-NNN</c>, <c>smtp-deferred-NNN</c>, <c>smtp-unreachable</c>, <c>smtp-authentication</c> or
    /// <c>smtp-error</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="exception" /> is <see langword="null" />.</exception>
    public static DeliveryAttemptResult Classify(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        switch (exception)
        {
            case SmtpCommandException command:
            {
                var status = (int) command.StatusCode;
                return status >= 500
                    ? DeliveryAttemptResult.Permanent($"smtp-rejected-{status}")
                    : DeliveryAttemptResult.Transient($"smtp-deferred-{status}");
            }
            case AuthenticationException:
                return DeliveryAttemptResult.Transient("smtp-authentication");
            case SmtpProtocolException:
            case IOException:
            case SocketException:
            case TimeoutException:
                return DeliveryAttemptResult.Transient("smtp-unreachable");
            default:
                return DeliveryAttemptResult.Transient("smtp-error");
        }
    }
}

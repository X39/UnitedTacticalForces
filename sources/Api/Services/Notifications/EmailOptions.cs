using MailKit.Security;
using MimeKit;

namespace X39.UnitedTacticalForces.Api.Services.Notifications;

/// <summary>
/// Settings of the SMTP server used to send cost alert emails, bound from the <c>Email:Smtp</c> section.
/// Credentials belong in <c>appsettings.secret.json</c> or the environment and are never logged.
/// </summary>
public sealed class EmailOptions
{
    /// <summary>
    /// The configuration section these options are bound from.
    /// </summary>
    public const string SectionName = "Email:Smtp";

    /// <summary>
    /// Gets or sets the SMTP server host name. While blank, email delivery is not configured and is skipped.
    /// </summary>
    public string? Host { get; set; }

    /// <summary>
    /// Gets or sets the SMTP server port.
    /// </summary>
    public int Port { get; set; } = 587;

    /// <summary>
    /// Gets or sets the transport security. The default upgrades the connection with STARTTLS and validates the
    /// server certificate. The values <see cref="SecureSocketOptions.None" /> and
    /// <see cref="SecureSocketOptions.Auto" /> are only intended for local development sinks.
    /// </summary>
    public SecureSocketOptions SecureSocket { get; set; } = SecureSocketOptions.StartTls;

    /// <summary>
    /// Gets or sets the SMTP user name. Authentication is attempted only when this is not blank.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the SMTP password.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets the sender address. While blank, email delivery is not configured and is skipped.
    /// </summary>
    public string? FromAddress { get; set; }

    /// <summary>
    /// Gets or sets the display name shown for the sender.
    /// </summary>
    public string FromName { get; set; } = "United Tactical Forces";

    /// <summary>
    /// Gets or sets the domain used on the right-hand side of generated Message-Id values. When blank, the domain
    /// of <see cref="FromAddress" /> is used.
    /// </summary>
    public string? MessageIdDomain { get; set; }

    /// <summary>
    /// Gets or sets the time allowed for each network operation against the SMTP server.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets a value indicating whether both <see cref="Host" /> and <see cref="FromAddress" /> are set.
    /// </summary>
    /// <value><see langword="true" /> when email delivery can be attempted.</value>
    public bool IsConfigured
        => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(FromAddress);

    /// <summary>
    /// Checks a configured section for unusable values. An unconfigured section is always valid.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The section is configured but the port, timeout, sender address or Message-Id domain is invalid. The message
    /// never contains a configured value.
    /// </exception>
    public void Validate()
    {
        if (!IsConfigured)
            return;
        if (Port is < 1 or > 65535
            || Timeout <= TimeSpan.Zero
            || !MailboxAddress.TryParse(FromAddress, out _)
            || HasInvalidMessageIdDomain())
        {
            throw new InvalidOperationException("Email:Smtp contains invalid settings.");
        }
    }

    private bool HasInvalidMessageIdDomain()
        => !string.IsNullOrWhiteSpace(MessageIdDomain)
           && MessageIdDomain.Any(character => character == '@' || char.IsWhiteSpace(character)
                                                                 || char.IsControl(character));
}

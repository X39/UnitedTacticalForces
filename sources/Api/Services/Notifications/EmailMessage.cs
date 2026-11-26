namespace X39.UnitedTacticalForces.Api.Services.Notifications;

/// <summary>
/// One plain-text email to one recipient.
/// </summary>
/// <param name="ToAddress">The recipient address.</param>
/// <param name="Subject">The subject. Line breaks are removed before sending.</param>
/// <param name="Body">The plain-text body.</param>
/// <param name="MessageId">
/// The deterministic Message-Id from <see cref="IEmailSender.CreateMessageId" />, so that an at-least-once resend of
/// the same alert is recognizable as the same message.
/// </param>
internal sealed record EmailMessage(string ToAddress, string Subject, string Body, string MessageId);

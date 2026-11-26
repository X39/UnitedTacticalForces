namespace X39.UnitedTacticalForces.Api.Services.Notifications;

/// <summary>
/// A rendered, localized cost alert ready to be handed to a delivery channel.
/// </summary>
/// <param name="Subject">The single-line subject, free of control characters.</param>
/// <param name="Body">The plain-text body.</param>
internal sealed record CostAlertMessage(string Subject, string Body);

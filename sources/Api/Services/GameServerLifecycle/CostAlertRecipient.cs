namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// One person a cost alert for a cost owner is addressed to, with the channels that can reach them.
/// </summary>
/// <param name="UserId">The primary key of the recipient user.</param>
/// <param name="HasDiscord">Whether the user has a linked Discord account.</param>
/// <param name="HasEmail">Whether the user has an email address.</param>
internal sealed record CostAlertRecipient(Guid UserId, bool HasDiscord, bool HasEmail);

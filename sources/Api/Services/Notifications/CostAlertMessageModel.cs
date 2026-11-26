using NodaTime;

namespace X39.UnitedTacticalForces.Api.Services.Notifications;

/// <summary>
/// The language-neutral facts of one cost alert that the renderer turns into text.
/// </summary>
/// <param name="ServerTitle">The title of the server whose session reached the threshold.</param>
/// <param name="ThresholdAmount">The configured alert amount that was reached.</param>
/// <param name="AccruedAmount">The accrued session cost at the time the alert was raised.</param>
/// <param name="Currency">The ISO 4217 currency code of both amounts.</param>
/// <param name="CreatedAt">The instant the alert was raised.</param>
/// <param name="ServerLink">An absolute link to the server in the app, or <see langword="null" /> when unknown.</param>
internal sealed record CostAlertMessageModel(
    string ServerTitle,
    decimal ThresholdAmount,
    decimal AccruedAmount,
    string Currency,
    Instant CreatedAt,
    string? ServerLink);

using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.Services.Notifications;

/// <summary>
/// The language-neutral facts of one automatic stop warning that the renderer turns into text.
/// </summary>
/// <param name="ServerTitle">The title of the server that is going to be stopped.</param>
/// <param name="ServerLink">An absolute link to the server in the app, or <see langword="null" /> when unknown.</param>
/// <param name="Reason">Why the stop was announced.</param>
/// <param name="Limit">
/// The limit that applied: minutes for an idle stop, hours for a maximum runtime stop, or <see langword="null" /> for
/// a schedule stop.
/// </param>
/// <param name="StopsAt">The instant the stop is requested unless it is cancelled first.</param>
internal sealed record AutoStopMessageModel(
    string ServerTitle,
    string? ServerLink,
    EAutoStopReason Reason,
    int? Limit,
    Instant StopsAt);

namespace X39.UnitedTacticalForces.WebSockets.Contracts.Hosting;

/// <summary>
/// Pushes the accrued cost of one costing session to the users who may view both the server and the session's cost
/// owner.
/// </summary>
/// <remarks>
/// Sent once per cost alert worker pass while a session may still cost, so the client never polls. The message
/// carries only flat scalar properties because the update stream serializer uses web defaults with reference
/// preservation and has no NodaTime or enum converter; the time is a <see cref="DateTimeOffset"/> and the amount a
/// plain number. It travels only on per-user paths of users in both audiences; the id-only
/// <see cref="GameServerFleetChangedMessage"/> remains the sole signal for everyone else.
/// </remarks>
public sealed class GameServerAccruedCostMessage
{
    /// <summary>
    /// Gets the server the accrued cost belongs to.
    /// </summary>
    /// <value>The primary key of the game server.</value>
    public required Guid ServerId { get; init; }

    /// <summary>
    /// Gets the session the accrued cost belongs to.
    /// </summary>
    /// <value>The primary key of the deployment.</value>
    public required Guid DeploymentId { get; init; }

    /// <summary>
    /// Gets the accrued amount of the session.
    /// </summary>
    /// <value>The amount in <see cref="Currency"/>, or <see langword="null"/> when it is unavailable; never zero as
    /// a stand-in for unavailable.</value>
    public decimal? Amount { get; init; }

    /// <summary>
    /// Gets the currency of <see cref="Amount"/>.
    /// </summary>
    /// <value>The ISO currency code.</value>
    public required string Currency { get; init; }

    /// <summary>
    /// Gets the instant the amount is valid for.
    /// </summary>
    /// <value>The calculation instant, serialized as an ISO-8601 string with offset.</value>
    public required DateTimeOffset AsOf { get; init; }

    /// <summary>
    /// Gets whether the amount can no longer advance.
    /// </summary>
    /// <value><see langword="true"/> once cost has stopped.</value>
    public bool IsFinal { get; init; }

    /// <summary>
    /// Gets why the amount is unavailable.
    /// </summary>
    /// <value>A short reason code, or <see langword="null"/> when <see cref="Amount"/> is available.</value>
    public string? UnavailableReason { get; init; }

    /// <summary>
    /// Gets the length of the provider's billing unit.
    /// </summary>
    /// <value>
    /// The whole seconds the provider bills in full for every started unit, for example <c>3600</c> for a provider
    /// that bills every started hour; <see langword="null"/> when <see cref="Amount"/> is <see langword="null"/>.
    /// </value>
    public int? BillingUnitSeconds { get; init; }

    /// <summary>
    /// Gets the instant the last started billing unit ends at.
    /// </summary>
    /// <value>
    /// The end of the last started billing unit at <see cref="AsOf"/>, which <see cref="Amount"/> covers, serialized
    /// as an ISO-8601 string with offset; <see langword="null"/> when <see cref="Amount"/> is
    /// <see langword="null"/>.
    /// </value>
    public DateTimeOffset? BilledThrough { get; init; }
}

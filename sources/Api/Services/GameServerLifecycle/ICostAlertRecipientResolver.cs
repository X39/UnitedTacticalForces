namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Resolves who a cost alert for a cost owner is addressed to.
/// </summary>
internal interface ICostAlertRecipientResolver
{
    /// <summary>
    /// Resolves the recipients of the given cost owners.
    /// </summary>
    /// <param name="costOwnerIds">The cost owners whose recipients are resolved.</param>
    /// <param name="cancellationToken">A token that cancels the database lookups.</param>
    /// <returns>
    /// A task resolving to the recipients per requested cost owner id. Every requested id has an entry, which is
    /// empty when nobody can be addressed.
    /// </returns>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<CostAlertRecipient>>> ResolveAsync(
        IReadOnlyCollection<Guid> costOwnerIds,
        CancellationToken cancellationToken);
}

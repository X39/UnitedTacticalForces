using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Resolves cost alert recipients by the type of the cost owner.
/// </summary>
/// <remarks>
/// A personal owner resolves only to its own user. A Verein or external organization owner resolves to the active
/// users who hold the <c>co:use</c> claim for that owner, directly or through a role. Holders of <c>co:view</c> and
/// finance staff can see the cost but are deliberately not messaged (D-13), because only the people who manage the
/// owner's spending act on a cost alert. Users who are deleted or banned are never addressed.
/// </remarks>
/// <param name="dbContext">The scoped database context the owners and users are read from.</param>
[Scoped<CostAlertRecipientResolver, ICostAlertRecipientResolver>]
internal sealed class CostAlertRecipientResolver(DatabaseContext dbContext) : ICostAlertRecipientResolver
{
    /// <summary>
    /// The lower-cased identifier of the cost-owner use claim, compared case-insensitively like the principal rule.
    /// </summary>
    private static readonly string CostOwnerUseIdentifier = Claims.ResourceBased.CostOwner.Use.ToLowerInvariant();

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<CostAlertRecipient>>> ResolveAsync(
        IReadOnlyCollection<Guid> costOwnerIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(costOwnerIds);
        var requested = costOwnerIds.Distinct().ToArray();
        var recipients = requested.ToDictionary(
            q => q,
            _ => new List<CostAlertRecipient>());
        if (requested.Length == 0)
            return ToResult(recipients);

        var personal = await dbContext.CostOwners
            .AsNoTracking()
            .Where(q => requested.Contains(q.PrimaryKey)
                        && q.Kind == ECostOwnerKind.User
                        && q.UserFk != null
                        && !q.User!.IsDeleted
                        && !q.User.IsBanned)
            .Select(q => new
            {
                CostOwnerId = q.PrimaryKey,
                UserId = q.UserFk!.Value,
                q.User!.EMail,
                DiscordId = q.User.Discord.Id,
            })
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var owner in personal)
        {
            recipients[owner.CostOwnerId].Add(new CostAlertRecipient(
                owner.UserId,
                owner.DiscordId != 0,
                !string.IsNullOrWhiteSpace(owner.EMail)));
        }

        await AddGroupRecipientsAsync(requested, recipients, cancellationToken).ConfigureAwait(false);
        return ToResult(recipients);
    }

    /// <summary>
    /// Adds the active <c>co:use</c> holders of every requested Verein and external organization owner.
    /// </summary>
    /// <param name="requested">The requested cost owner ids; personal owners among them are ignored.</param>
    /// <param name="recipients">The working lists the recipients are added to.</param>
    /// <param name="cancellationToken">A token that cancels the database lookups.</param>
    /// <returns>A task completing once the group recipients were added.</returns>
    private async Task AddGroupRecipientsAsync(
        Guid[] requested,
        Dictionary<Guid, List<CostAlertRecipient>> recipients,
        CancellationToken cancellationToken)
    {
        var groupOwnerIds = await dbContext.CostOwners
            .AsNoTracking()
            .Where(q => requested.Contains(q.PrimaryKey) && q.Kind != ECostOwnerKind.User)
            .Select(q => q.PrimaryKey)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (groupOwnerIds.Length == 0)
            return;

        var idsByValue = groupOwnerIds.ToDictionary(q => q.ToString(), q => q, StringComparer.Ordinal);
        var ownerValues = idsByValue.Keys.ToArray();
        var users = dbContext.Users.AsNoTracking().Where(q => !q.IsDeleted && !q.IsBanned);
        // Identifiers are compared case-insensitively and values ordinally, like ClaimsPrincipal.HasClaim.
        var directGrants = await users
            .SelectMany(
                q => q.Claims!.Where(claim =>
                    claim.Identifier.ToLower() == CostOwnerUseIdentifier
                    && claim.Value != null
                    && ownerValues.Contains(claim.Value)),
                (user, claim) => new { UserId = user.PrimaryKey, claim.Value })
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var roleGrants = await users
            .SelectMany(
                q => q.Roles!.SelectMany(role => role.Claims!.Where(claim =>
                    claim.Identifier.ToLower() == CostOwnerUseIdentifier
                    && claim.Value != null
                    && ownerValues.Contains(claim.Value))),
                (user, claim) => new { UserId = user.PrimaryKey, claim.Value })
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var holders = directGrants
            .Concat(roleGrants)
            .Where(q => q.Value is not null && idsByValue.ContainsKey(q.Value))
            .Select(q => (CostOwnerId: idsByValue[q.Value!], q.UserId))
            .Distinct()
            .ToArray();
        if (holders.Length == 0)
            return;

        var userIds = holders.Select(q => q.UserId).Distinct().ToArray();
        var contacts = (await dbContext.Users
                .AsNoTracking()
                .Where(q => userIds.Contains(q.PrimaryKey))
                .Select(q => new { q.PrimaryKey, q.EMail, DiscordId = q.Discord.Id })
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false))
            .ToDictionary(q => q.PrimaryKey);
        foreach (var holder in holders.OrderBy(q => q.UserId))
        {
            var contact = contacts[holder.UserId];
            recipients[holder.CostOwnerId].Add(new CostAlertRecipient(
                holder.UserId,
                contact.DiscordId != 0,
                !string.IsNullOrWhiteSpace(contact.EMail)));
        }
    }

    /// <summary>
    /// Converts the working lists to the read-only result shape.
    /// </summary>
    /// <param name="recipients">The recipients collected per cost owner id.</param>
    /// <returns>The same data as read-only collections.</returns>
    private static IReadOnlyDictionary<Guid, IReadOnlyList<CostAlertRecipient>> ToResult(
        Dictionary<Guid, List<CostAlertRecipient>> recipients)
        => recipients.ToDictionary(
            q => q.Key,
            q => (IReadOnlyList<CostAlertRecipient>) q.Value);
}

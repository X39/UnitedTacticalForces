using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Authority.Extern;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// CST-05 proof that cost alert recipients follow the owner type: a personal owner resolves to its own user, and a
/// group owner resolves to the active users who hold <c>co:use</c> for it, directly or through a role.
/// </summary>
public sealed class CostAlertRecipientResolverTests
{
    /// <summary>
    /// Direct and role-based co:use holders are recipients; co:view holders, banned holders, deleted holders and
    /// holders of another owner are not.
    /// </summary>
    /// <returns>A task completing after the recipient set was verified.</returns>
    [Fact]
    public async Task VereinOwner_ResolvesActiveCoUseHoldersOnly()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var owner = CreateOwner(ECostOwnerKind.Verein, "Verein");
        var otherOwner = CreateOwner(ECostOwnerKind.ExternalOrganization, "Other");
        var use = CreateClaim(Claims.ResourceBased.CostOwner.Use, owner.PrimaryKey.ToString());
        var view = CreateClaim(Claims.ResourceBased.CostOwner.View, owner.PrimaryKey.ToString());
        var useOther = CreateClaim(Claims.ResourceBased.CostOwner.Use, otherOwner.PrimaryKey.ToString());
        var direct = CreateUser("direct", discordId: 11, email: "direct@example.org");
        direct.Claims!.Add(use);
        var viaRole = CreateUser("role", discordId: 0, email: null);
        viaRole.Roles!.Add(CreateRole("managers", use));
        var viewOnly = CreateUser("view", discordId: 12, email: "view@example.org");
        viewOnly.Claims!.Add(view);
        var banned = CreateUser("banned", discordId: 13, email: "banned@example.org");
        banned.IsBanned = true;
        banned.Claims!.Add(use);
        var deleted = CreateUser("deleted", discordId: 14, email: "deleted@example.org");
        deleted.IsDeleted = true;
        deleted.Roles!.Add(CreateRole("former managers", use));
        var elsewhere = CreateUser("elsewhere", discordId: 15, email: "elsewhere@example.org");
        elsewhere.Claims!.Add(useOther);
        store.DbContext.AddRange(owner, otherOwner, direct, viaRole, viewOnly, banned, deleted, elsewhere);
        await store.DbContext.SaveChangesAsync();

        var result = await new CostAlertRecipientResolver(store.DbContext)
            .ResolveAsync([owner.PrimaryKey, otherOwner.PrimaryKey], CancellationToken.None);

        var recipients = result[owner.PrimaryKey].OrderBy(q => q.UserId).ToArray();
        Assert.Equal(
            new[] { direct.PrimaryKey, viaRole.PrimaryKey }.Order().ToArray(),
            recipients.Select(q => q.UserId).ToArray());
        Assert.True(recipients.Single(q => q.UserId == direct.PrimaryKey) is { HasDiscord: true, HasEmail: true });
        Assert.True(recipients.Single(q => q.UserId == viaRole.PrimaryKey) is { HasDiscord: false, HasEmail: false });
        Assert.Equal([elsewhere.PrimaryKey], result[otherOwner.PrimaryKey].Select(q => q.UserId).ToArray());
    }

    /// <summary>
    /// A user who holds co:use directly and through a role is listed once.
    /// </summary>
    /// <returns>A task completing after the deduplication was verified.</returns>
    [Fact]
    public async Task HolderThroughClaimAndRole_IsListedOnce()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var owner = CreateOwner(ECostOwnerKind.Verein, "Verein");
        var use = CreateClaim(Claims.ResourceBased.CostOwner.Use, owner.PrimaryKey.ToString());
        var user = CreateUser("both", discordId: 1, email: "both@example.org");
        user.Claims!.Add(use);
        user.Roles!.Add(CreateRole("managers", use));
        store.DbContext.AddRange(owner, user);
        await store.DbContext.SaveChangesAsync();

        var result = await new CostAlertRecipientResolver(store.DbContext)
            .ResolveAsync([owner.PrimaryKey], CancellationToken.None);

        Assert.Equal(user.PrimaryKey, Assert.Single(result[owner.PrimaryKey]).UserId);
    }

    /// <summary>
    /// An external organization without any co:use holder resolves to an empty list, and an unknown owner id still
    /// has an entry.
    /// </summary>
    /// <returns>A task completing after the empty entries were verified.</returns>
    [Fact]
    public async Task ExternalOrganizationWithoutHolder_ResolvesEmptyList()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var owner = CreateOwner(ECostOwnerKind.ExternalOrganization, "Sponsor");
        var unknown = Guid.NewGuid();
        store.DbContext.Add(owner);
        await store.DbContext.SaveChangesAsync();

        var result = await new CostAlertRecipientResolver(store.DbContext)
            .ResolveAsync([owner.PrimaryKey, unknown], CancellationToken.None);

        Assert.Empty(result[owner.PrimaryKey]);
        Assert.Empty(result[unknown]);
    }

    /// <summary>
    /// A personal owner resolves only to its own user with that user's reachability, and not at all once the user
    /// is banned.
    /// </summary>
    /// <returns>A task completing after both outcomes were verified.</returns>
    [Fact]
    public async Task PersonalOwner_ResolvesOwnUserUnlessBanned()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.User.Discord.Id = 99;
        seed.User.EMail = "owner@example.org";
        await store.DbContext.SaveChangesAsync();
        var resolver = new CostAlertRecipientResolver(store.DbContext);

        var active = await resolver.ResolveAsync([seed.CostOwner.PrimaryKey], CancellationToken.None);
        seed.User.IsBanned = true;
        await store.DbContext.SaveChangesAsync();
        var banned = await resolver.ResolveAsync([seed.CostOwner.PrimaryKey], CancellationToken.None);

        var recipient = Assert.Single(active[seed.CostOwner.PrimaryKey]);
        Assert.Equal(new CostAlertRecipient(seed.User.PrimaryKey, true, true), recipient);
        Assert.Empty(banned[seed.CostOwner.PrimaryKey]);
    }

    /// <summary>
    /// Creates an unsaved group cost owner.
    /// </summary>
    /// <param name="kind">The owner kind; it must not be the personal kind.</param>
    /// <param name="displayName">The display name of the owner.</param>
    /// <returns>The created cost owner.</returns>
    private static CostOwner CreateOwner(ECostOwnerKind kind, string displayName)
        => new()
        {
            PrimaryKey = Guid.NewGuid(),
            Kind = kind,
            DisplayName = displayName,
            Currency = "EUR",
            TimeStampCreated = Instant.FromUnixTimeSeconds(1_700_000_000),
            TimeStampUpdated = Instant.FromUnixTimeSeconds(1_700_000_000),
        };

    /// <summary>
    /// Creates an unsaved claim row.
    /// </summary>
    /// <param name="identifier">The claim identifier.</param>
    /// <param name="value">The claim value.</param>
    /// <returns>The created claim.</returns>
    private static Claim CreateClaim(string identifier, string value)
        => new()
        {
            Identifier = identifier,
            Value = value,
            Category = "test",
            Title = identifier,
            Description = "test claim",
        };

    /// <summary>
    /// Creates an unsaved, active user.
    /// </summary>
    /// <param name="nickname">The nickname of the user.</param>
    /// <param name="discordId">The Discord id; zero means not linked.</param>
    /// <param name="email">The email address, or <see langword="null"/> for none.</param>
    /// <returns>The created user with empty claim and role collections.</returns>
    private static User CreateUser(string nickname, ulong discordId, string? email)
        => new()
        {
            PrimaryKey = Guid.NewGuid(),
            Nickname = nickname,
            EMail = email,
            AvatarMimeType = string.Empty,
            Claims = [],
            Roles = [],
            Discord = new DiscordUser { Username = nickname, Id = discordId },
            Steam = new SteamUser(),
        };

    /// <summary>
    /// Creates an unsaved role carrying the given claims.
    /// </summary>
    /// <param name="title">The title of the role.</param>
    /// <param name="claims">The claims the role grants.</param>
    /// <returns>The created role.</returns>
    private static Role CreateRole(string title, params Claim[] claims)
        => new()
        {
            Title = title,
            Description = "test role",
            Claims = claims.ToList(),
        };
}

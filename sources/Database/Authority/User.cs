using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using X39.UnitedTacticalForces.Database.Authority.Extern;
using X39.UnitedTacticalForces.Database.Eventing;
using X39.UnitedTacticalForces.Database.Meta;

namespace X39.UnitedTacticalForces.Database.Authority;

/// <summary>
/// Represents a user of the system.
/// </summary>
[Index(nameof(Nickname))]
public class User : IPrimaryKey<Guid>
{
    /// <summary>
    /// Database index enforcing one active user per non-zero Steam ID.
    /// </summary>
    public const string ActiveSteamId64IndexName = "IX_Users_Steam_Id64_Active";

    /// <summary>
    /// Database filter excluding deleted/anonymized users and users without a linked Steam account.
    /// </summary>
    public const string ActiveSteamId64IndexFilter = "\"IsDeleted\" = FALSE AND \"Steam_Id64\" <> 0";

    /// <inheritdoc />
    [Key]
    public Guid PrimaryKey { get; set; }

    /// <summary>
    /// The nickname of this user.
    /// </summary>
    [MaxLength(1024)]
    public string Nickname { get; set; } = string.Empty;

    /// <summary>
    /// The email address of this user.
    /// </summary>
    [MaxLength(1024)]
    public string? EMail { get; set; }

    /// <summary>
    /// The user's legal first name for formal Verein membership records.
    /// </summary>
    [MaxLength(256)]
    public string? FirstName { get; set; }

    /// <summary>
    /// The user's legal last name for formal Verein membership records.
    /// </summary>
    [MaxLength(256)]
    public string? LastName { get; set; }

    /// <summary>
    /// The user's date of birth for formal Verein membership records.
    /// </summary>
    public LocalDate? DateOfBirth { get; set; }

    /// <summary>
    /// Optional phone number for formal Verein membership contact.
    /// </summary>
    [MaxLength(64)]
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// First line of the user's postal address for formal Verein membership contact.
    /// </summary>
    [MaxLength(512)]
    public string? AddressLine1 { get; set; }

    /// <summary>
    /// Optional second line of the user's postal address for formal Verein membership contact.
    /// </summary>
    [MaxLength(512)]
    public string? AddressLine2 { get; set; }

    /// <summary>
    /// Postal code of the user's address for formal Verein membership contact.
    /// </summary>
    [MaxLength(32)]
    public string? PostalCode { get; set; }

    /// <summary>
    /// City of the user's address for formal Verein membership contact.
    /// </summary>
    [MaxLength(256)]
    public string? City { get; set; }

    /// <summary>
    /// Country of the user's address for formal Verein membership contact.
    /// </summary>
    [MaxLength(128)]
    public string? Country { get; set; }

    /// <summary>
    /// Whether this user is banned.
    /// </summary>
    public bool IsBanned { get; set; }

    /// <summary>
    /// The roles this user has.
    /// </summary>
    public ICollection<Role>? Roles { get; set; }

    /// <summary>
    /// The claims this user has.
    /// </summary>
    public ICollection<Claim>? Claims { get; set; }

    /// <summary>
    /// Meta data for the mod packs this user has interacted with.
    /// </summary>
    public ICollection<UserModPackMeta>? ModPackMetas { get; set; }

    /// <summary>
    /// Meta data for the events this user has interacted with.
    /// </summary>
    public ICollection<UserEventMeta>? EventMetas { get; set; }

    /// <summary>
    /// The event slots this user has interacted with.
    /// </summary>
    public ICollection<EventSlot>? EventSlots { get; set; }

    /// <summary>
    /// The users avatar.
    /// </summary>
    public byte[] Avatar { get; set; } = [];

    /// <summary>
    /// The mime type of the users avatar.
    /// </summary>
    [MaxLength(1024)]
    public string AvatarMimeType { get; set; } = string.Empty;

    /// <summary>
    /// Whether this user has verified their identity.
    /// </summary>
    public bool IsVerified { get; set; }

    /// <summary>
    /// Whether this user has been deleted.
    /// </summary>
    /// <remarks>
    /// Deleted users are not removed from the database but are marked as deleted to retain data integrity.
    /// The data must be anonymized when setting this property to true to comply with GDPR.
    /// </remarks>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// The steam user data.
    /// </summary>
    public SteamUser Steam { get; set; } = new();

    /// <summary>
    /// The discord user data.
    /// </summary>
    public DiscordUser Discord { get; set; } = new();

    /// <summary>
    /// Creates a <see cref="ClaimsIdentity"/> from this user.
    /// </summary>
    /// <param name="lazyLoader">The <see cref="ILazyLoader"/> to use to load the <see cref="Roles"/> and the corresponding <see cref="Role.Claims"/> if not loaded.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that resolves to a <see cref="ClaimsIdentity"/>.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="lazyLoader"/> is null and <see cref="Roles"/> is not loaded.</exception>
    /// <exception cref="InvalidOperationException">Thrown if this user is banned or deleted.</exception>
    [SuppressMessage("ReSharper", "LocalizableElement")]
    public async ValueTask<ClaimsIdentity> ToIdentityAsync(
        ILazyLoader? lazyLoader = default,
        CancellationToken cancellationToken = default)
    {
        if (IsBanned || IsDeleted)
            throw new InvalidOperationException(
                "Cannot create an authenticated identity for a banned or deleted user.");

        var identity = new ClaimsIdentity(AuthenticationSchemes.Api);
        if (Roles is null)
            if (lazyLoader is null)
                throw new ArgumentException(
                    "ILazyLoader must be provided if roles are not loaded",
                    nameof(lazyLoader));
            else
                await lazyLoader
                    .LoadAsync(this, cancellationToken, nameof(Roles))
                    .ConfigureAwait(false);
        if (Claims is null)
            if (lazyLoader is null)
                throw new ArgumentException(
                    "ILazyLoader must be provided if claims are not loaded",
                    nameof(lazyLoader));
            else
                await lazyLoader
                    .LoadAsync(this, cancellationToken, nameof(Claims))
                    .ConfigureAwait(false);
        foreach (var role in Roles!)
        {
            if (role.Claims is null)
                if (lazyLoader is null)
                    throw new ArgumentException(
                        "ILazyLoader must be provided if claims of roles are not loaded",
                        nameof(lazyLoader));
                else
                    await lazyLoader
                        .LoadAsync(role, cancellationToken, nameof(role.Claims))
                        .ConfigureAwait(false);
            foreach (var claim in role.Claims!)
            {
                identity.AddClaim(
                    new System.Security.Claims.Claim(
                        claim.Identifier,
                        claim.Value ?? string.Empty,
                        claim.ValueType,
                        AuthenticationSchemes.Api));
            }
        }
        foreach (var claim in Claims!)
        {
            identity.AddClaim(
                new System.Security.Claims.Claim(
                    claim.Identifier,
                    claim.Value ?? string.Empty,
                    claim.ValueType,
                    AuthenticationSchemes.Api));
        }

        identity.AddClaim(new System.Security.Claims.Claim(ClaimTypes.GivenName, Nickname, null, AuthenticationSchemes.Api));
        identity.AddClaim(new System.Security.Claims.Claim(ClaimTypes.Name, PrimaryKey.ToString(), null, AuthenticationSchemes.Api));
        if (EMail is not null)
            identity.AddClaim(new System.Security.Claims.Claim(ClaimTypes.Email, EMail, null, AuthenticationSchemes.Api));
        if (IsVerified)
            identity.AddClaim(
                new System.Security.Claims.Claim(
                    ClaimTypes.Role,
                    X39.UnitedTacticalForces.Claims.General.Verified,
                    null,
                    AuthenticationSchemes.Api));
        return identity;
    }
}

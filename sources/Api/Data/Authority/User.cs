using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using X39.UnitedTacticalForces.Api.Data.Authority.Extern;
using X39.UnitedTacticalForces.Api.Data.Eventing;
using X39.UnitedTacticalForces.Api.Data.Meta;

namespace X39.UnitedTacticalForces.Api.Data.Authority;

/// <summary>
/// Represents a user of the system.
/// </summary>
[Index(nameof(Nickname))]
public class User : IPrimaryKey<Guid>
{
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
    [SuppressMessage("ReSharper", "LocalizableElement")]
    public async ValueTask<ClaimsIdentity> ToIdentityAsync(
        ILazyLoader? lazyLoader = default,
        CancellationToken cancellationToken = default)
    {
        var identity = new ClaimsIdentity(
            IsBanned ? Constants.AuthorizationSchemas.Banned : Constants.AuthorizationSchemas.Api);
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
                        Constants.AuthorizationSchemas.Api));
            }
        }
        foreach (var claim in Claims!)
        {
            identity.AddClaim(
                new System.Security.Claims.Claim(
                    claim.Identifier,
                    claim.Value ?? string.Empty,
                    claim.ValueType,
                    Constants.AuthorizationSchemas.Api));
        }

        identity.AddClaim(new System.Security.Claims.Claim(ClaimTypes.GivenName, Nickname, null, Constants.AuthorizationSchemas.Api));
        identity.AddClaim(new System.Security.Claims.Claim(ClaimTypes.Name, PrimaryKey.ToString(), null, Constants.AuthorizationSchemas.Api));
        if (EMail is not null)
            identity.AddClaim(new System.Security.Claims.Claim(ClaimTypes.Email, EMail, null, Constants.AuthorizationSchemas.Api));
        if (IsVerified)
            identity.AddClaim(
                new System.Security.Claims.Claim(
                    ClaimTypes.Role,
                    X39.UnitedTacticalForces.Claims.General.Verified,
                    null,
                    Constants.AuthorizationSchemas.Api));
        return identity;
    }
}

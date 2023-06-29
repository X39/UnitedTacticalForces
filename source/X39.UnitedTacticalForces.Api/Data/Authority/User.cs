using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using X39.UnitedTacticalForces.Api.Data.Authority.Extern;
using X39.UnitedTacticalForces.Api.Data.Eventing;
using X39.UnitedTacticalForces.Api.Data.Meta;

namespace X39.UnitedTacticalForces.Api.Data.Authority;

[Index(nameof(Nickname))]
public class User : IPrimaryKey<Guid>
{
    [Key]
    public Guid PrimaryKey { get; set; }

    public string Nickname { get; set; } = string.Empty;
    public string? EMail { get; set; }
    public bool IsBanned { get; set; }
    public ICollection<Role>? Roles { get; set; }
    public ICollection<UserModPackMeta>? ModPackMetas { get; set; }

    public ICollection<UserEventMeta>? EventMetas { get; set; }

    public ICollection<EventSlot>? EventSlots { get; set; }

    public byte[] Avatar { get; set; } = Array.Empty<byte>();
    public string AvatarMimeType { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
    public bool IsDeleted { get; set; }
    public SteamUser Steam { get; set; } = new();
    public DiscordUser Discord { get; set; } = new();

    public async ValueTask<ClaimsIdentity> ToIdentityAsync(
        ILazyLoader? lazyLoader = default,
        CancellationToken cancellationToken = default)
    {
        var identity = new ClaimsIdentity(
            IsBanned ? Constants.AuthorizationSchemas.Banned : Constants.AuthorizationSchemas.Api);
        if (Roles is null)
            if (lazyLoader is null)
                throw new ArgumentException(
                    "ILazyLoader must be provided if Privileges is not loaded",
                    nameof(lazyLoader));
            else
                await lazyLoader
                    .LoadAsync(this, cancellationToken, nameof(Roles))
                    .ConfigureAwait(false);
        foreach (var role in Roles!)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role.Identifier, null, Constants.AuthorizationSchemas.Api));
        }

        identity.AddClaim(new Claim(ClaimTypes.GivenName, Nickname, null, Constants.AuthorizationSchemas.Api));
        identity.AddClaim(new Claim(ClaimTypes.Name, PrimaryKey.ToString(), null, Constants.AuthorizationSchemas.Api));
        if (EMail is not null)
            identity.AddClaim(new Claim(ClaimTypes.Email, EMail, null, Constants.AuthorizationSchemas.Api));
        if (IsVerified)
            identity.AddClaim(
                new Claim(
                    ClaimTypes.Role,
                    X39.UnitedTacticalForces.Roles.Verified,
                    null,
                    Constants.AuthorizationSchemas.Api));
        return identity;
    }
}
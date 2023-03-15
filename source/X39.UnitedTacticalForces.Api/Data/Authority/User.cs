using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using X39.UnitedTacticalForces.Api.Data.Eventing;

namespace X39.UnitedTacticalForces.Api.Data.Authority;

[Index(nameof(SteamId64), IsUnique = true)]
[Index(nameof(Nickname))]
public class User
{
    [Key] public Guid PrimaryKey { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public ulong SteamId64 { get; set; }
    public string? EMail { get; set; }
    public bool IsBanned { get; set; }
    public ICollection<Role>? Roles { get; set; }
    public ICollection<UserModPackMeta>? ModPackMetas { get; set; }
    
    public ICollection<UserEventMeta>? EventMetas { get; set; }
    
    public ICollection<EventSlot>? EventSlots { get; set; }

    public byte[] Avatar { get; set; } = Array.Empty<byte>();
    public string AvatarMimeType { get; set; } = string.Empty;
    public bool IsVerified { get; set; }

    public async ValueTask<ClaimsIdentity> ToIdentityAsync(
        ILazyLoader? lazyLoader = default,
        CancellationToken cancellationToken = default)
    {
        var identity = new ClaimsIdentity(IsBanned ? Constants.AuthorizationSchemas.Banned : Constants.AuthorizationSchemas.Api);
        if (Roles is null)
            if (lazyLoader is null)
                throw new ArgumentException("ILazyLoader must be provided if Privileges is not loaded",
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
            identity.AddClaim(new Claim(ClaimTypes.Role, X39.UnitedTacticalForces.Roles.Verified, null, Constants.AuthorizationSchemas.Api));
        return identity;
    }
}
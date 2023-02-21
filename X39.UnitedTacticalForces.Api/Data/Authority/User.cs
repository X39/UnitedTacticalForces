using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace X39.UnitedTacticalForces.Api.Data.Authority;

public class User
{
    [Key] public Guid PrimaryKey { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public ulong SteamId64 { get; set; }
    public string? EMail { get; set; }
    public ICollection<Role>? Roles { get; set; }
    public ICollection<UserModPackMeta>? ModPackMetas { get; set; }

    public byte[] Avatar { get; set; } = Array.Empty<byte>();
    public string AvatarMimeType { get; set; } = string.Empty;

    public async ValueTask<ClaimsIdentity> ToIdentityAsync(
        ILazyLoader? lazyLoader = default,
        CancellationToken cancellationToken = default)
    {
        var identity = new ClaimsIdentity(Constants.AuthorizationSchemas.Api);
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
        return identity;
    }
}
using X39.UnitedTacticalForces.WebApp.Api.Models;
using X39.UnitedTacticalForces.WebApp.Services.UserRepository;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services;

[Scoped<MeService>]
public class MeService(IUserRepository userRepository)
{
    private FullUserDto? _possessUser;
    private FullUserDto? _user;

    /// <summary>
    /// Returns the current <see cref="WebApp.User"/> object of the user.
    /// </summary>
    /// <remarks>
    /// If <see cref="IsAuthenticated"/> returns <see langword="false"/>, this will throw an exception.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    ///     Thrown if the current user is not authenticated.
    ///     Authentication status can be checked using <see cref="IsAuthenticated"/>.
    /// </exception>
    public FullUserDto User
        => _possessUser
           ?? _user
           ?? throw new InvalidOperationException(
               $"User is not authenticated. Check authentication status prior to using property using {nameof(IsAuthenticated)}."
           );

    public bool IsImposter => _possessUser is not null;

    public bool IsAuthenticated => _user is not null && !(_user.IsBanned ?? false);

    public bool IsVerified => _user?.IsVerified ?? false;

    public bool Eval(Func<FullUserDto, bool> func) => IsAuthenticated && func(User);

    public void PossessUser(FullUserDto? user)
    {
        _possessUser = user;
    }

    /// <summary>
    /// Initializes the service.
    /// </summary>
    /// <remarks>
    /// Called by <see cref="Program"/> at app-start.
    /// </remarks>
    public async Task InitializeAsync()
    {
        if (_user is not null)
            return;
        try
        {
            _user = await userRepository.GetMeAsync()
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            Console.WriteLine(ex.StackTrace);
            // empty
        }
    }

    /// <summary>
    /// Checks if the user has the specified claim or any of the owned claims starts with the specified claim.
    /// </summary>
    /// <param name="claim">The claim to check for.</param>
    /// <param name="claims">The claims to check for.</param>
    /// <returns>
    ///     <see langword="true"/> if the user has the specified claim
    ///     or any of the owned claims starts with the specified claim; otherwise <see langword="false"/>.
    /// </returns>
    public bool HasBaseClaim(string claim, params string[] claims)
    {
        if (!IsAuthenticated)
            return false;
        if (User.Claims?.Any((q) => q.Identifier?.StartsWith(claim) ?? false) ?? false)
            return true;
        return claims.Any(r => User.Claims?.Any((q) => q.Identifier?.StartsWith(r) ?? false) ?? false);
    }

    /// <summary>
    /// Checks if the user has the specified claim or any of the owned claims is the specified claim.
    /// </summary>
    /// <param name="claim">The claim to check for.</param>
    /// <param name="claims">The claims to check for.</param>
    /// <returns>
    ///     <see langword="true"/> if the user has the specified claim
    ///     or any of the owned claims is the specified claim; otherwise <see langword="false"/>.
    /// </returns>
    public bool HasClaim(string claim, params string[] claims)
    {
        if (!IsAuthenticated)
            return false;
        if (User.Claims?.Any((q) => q.Identifier == claim) ?? false)
            return true;
        return claims.Any(r => User.Claims?.Any((q) => q.Identifier == r) ?? false);
    }

    public bool HasClaim(string claim1, string claim2, params string[] claims)
    {
        if (!IsAuthenticated)
            return false;
        if (User.Claims?.Any((q) => q.Identifier == claim1) ?? false)
            return true;
        if (User.Claims?.Any((q) => q.Identifier == claim2) ?? false)
            return true;
        return claims.Any(r => User.Claims?.Any((q) => q.Identifier == r) ?? false);
    }

    public bool HasClaim(string claim1, string claim2, string claim3, params string[] claims)
    {
        if (!IsAuthenticated)
            return false;
        if (User.Claims?.Any((q) => q.Identifier == claim1) ?? false)
            return true;
        if (User.Claims?.Any((q) => q.Identifier == claim2) ?? false)
            return true;
        if (User.Claims?.Any((q) => q.Identifier == claim3) ?? false)
            return true;
        return claims.Any(r => User.Claims?.Any((q) => q.Identifier == r) ?? false);
    }

    public bool HasResourceClaim(string claim, string value)
    {
        return User.Claims?.Any((q) => q.Identifier == claim && q.Value == value) ?? false;
    }

    public bool HasClaimOrAdmin(string claim, params string[] claims)
        => HasClaim(Claims.Administrative.All, claim, claims);

    public bool HasClaimOrEventAdmin() => HasClaim(Claims.Administrative.All, Claims.Administrative.Event);

    public bool HasClaimOrTerrainAdmin() => HasClaim(Claims.Administrative.All, Claims.Administrative.Terrain);

    public bool HasClaimOrModPackAdmin() => HasClaim(Claims.Administrative.All, Claims.Administrative.ModPack);

    public bool HasClaimOrUserAdmin() => HasClaim(Claims.Administrative.All, Claims.Administrative.User);

    public bool HasClaimOrRoleAdmin() => HasClaim(Claims.Administrative.All, Claims.Administrative.Role);

    public bool HasClaimOrServerAdmin() => HasClaim(Claims.Administrative.All, Claims.Administrative.Server);

    public bool HasClaimOrWikiAdmin() => HasClaim(Claims.Administrative.All, Claims.Administrative.Wiki);

    public bool HasClaimOrEventAdmin(string claim, params string[] claims)
        => HasClaim(Claims.Administrative.All, Claims.Administrative.Event, claim, claims);

    public bool HasClaimOrTerrainAdmin(string claim, params string[] claims)
        => HasClaim(Claims.Administrative.All, Claims.Administrative.Terrain, claim, claims);

    public bool HasClaimOrModPackAdmin(string claim, params string[] claims)
        => HasClaim(Claims.Administrative.All, Claims.Administrative.ModPack, claim, claims);

    public bool HasClaimOrUserAdmin(string claim, params string[] claims)
        => HasClaim(Claims.Administrative.All, Claims.Administrative.User, claim, claims);

    public bool HasClaimOrRoleAdmin(string claim, params string[] claims)
        => HasClaim(Claims.Administrative.All, Claims.Administrative.Role, claim, claims);

    public bool HasClaimOrServerAdmin(string claim, params string[] claims)
        => HasClaim(Claims.Administrative.All, Claims.Administrative.Server, claim, claims);

    public bool HasClaimOrWikiAdmin(string claim, params string[] claims)
        => HasClaim(Claims.Administrative.All, Claims.Administrative.Wiki, claim, claims);

    public bool HasBaseClaimOrEventAdmin(string claim, params string[] claims)
        => HasClaim(Claims.Administrative.All, Claims.Administrative.Event) || HasBaseClaim(claim, claims);

    public bool HasBaseClaimOrTerrainAdmin(string claim, params string[] claims)
        => HasClaim(Claims.Administrative.All, Claims.Administrative.Terrain) || HasBaseClaim(claim, claims);

    public bool HasBaseClaimOrModPackAdmin(string claim, params string[] claims)
        => HasClaim(Claims.Administrative.All, Claims.Administrative.ModPack) || HasBaseClaim(claim, claims);

    public bool HasBaseClaimOrUserAdmin(string claim, params string[] claims)
        => HasClaim(Claims.Administrative.All, Claims.Administrative.User) || HasBaseClaim(claim, claims);

    public bool HasBaseClaimOrRoleAdmin(string claim, params string[] claims)
        => HasClaim(Claims.Administrative.All, Claims.Administrative.Role) || HasBaseClaim(claim, claims);

    public bool HasBaseClaimOrServerAdmin(string claim, params string[] claims)
        => HasClaim(Claims.Administrative.All, Claims.Administrative.Server) || HasBaseClaim(claim, claims);

    public bool HasBaseClaimOrWikiAdmin(string claim, params string[] claims)
        => HasClaim(Claims.Administrative.All, Claims.Administrative.Wiki) || HasBaseClaim(claim, claims);

    public bool HasResourceClaimOrEventAdmin(string claim, string value)
        => HasClaim(Claims.Administrative.All, Claims.Administrative.Event) || HasResourceClaim(claim, value);

    public bool HasResourceClaimOrTerrainAdmin(string claim, string value)
        => HasClaim(Claims.Administrative.All, Claims.Administrative.Terrain) || HasResourceClaim(claim, value);

    public bool HasResourceClaimOrModPackAdmin(string claim, string value)
        => HasClaim(Claims.Administrative.All, Claims.Administrative.ModPack) || HasResourceClaim(claim, value);

    public bool HasResourceClaimOrUserAdmin(string claim, string value)
        => HasClaim(Claims.Administrative.All, Claims.Administrative.User) || HasResourceClaim(claim, value);

    public bool HasResourceClaimOrRoleAdmin(string claim, string value)
        => HasClaim(Claims.Administrative.All, Claims.Administrative.Role) || HasResourceClaim(claim, value);

    public bool HasResourceClaimOrServerAdmin(string claim, string value)
        => HasClaim(Claims.Administrative.All, Claims.Administrative.Server) || HasResourceClaim(claim, value);

    public bool HasResourceClaimOrWikiAdmin(string claim, string value)
        => HasClaim(Claims.Administrative.All, Claims.Administrative.Wiki) || HasResourceClaim(claim, value);
}

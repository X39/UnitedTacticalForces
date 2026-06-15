using X39.UnitedTacticalForces.WebApp.UtfApi.Models;
using X39.UnitedTacticalForces.WebApp.Services.UserRepository;
using X39.Util.DependencyInjection.Attributes;

using System.Text.Json;

namespace X39.UnitedTacticalForces.WebApp.Services;

[Scoped<MeService>]
public class MeService(IUserRepository userRepository)
{
    private FullUserDto? _possessUser;
    private FullUserDto? _user;

    public event EventHandler? CurrentUserChanged;

    /// <summary>
    /// Returns the current <see cref="Claims.User"/> object of the user.
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

    public bool IsVereinMember
        => IsAuthenticated
           && (User.IsVereinMember == true
               || GetAdditionalBool(nameof(IsVereinMember))
               || string.Equals(VereinMembershipStatus, "Active", StringComparison.OrdinalIgnoreCase));

    public string? VereinMembershipStatus
        => !IsAuthenticated
            ? null
            : (string?) User.VereinMembershipStatus
              ?? GetAdditionalString("vereinMembershipStatus");

    public string? VereinMemberNumber
        => !IsAuthenticated
            ? null
            : (string?) User.VereinMemberNumber
              ?? GetAdditionalString("vereinMemberNumber");

    public bool Eval(Func<FullUserDto, bool> func) => IsAuthenticated && func(User);

    public void PossessUser(FullUserDto? user)
    {
        _possessUser = user;
        NotifyCurrentUserChanged();
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
            if (_user is not null)
                NotifyCurrentUserChanged();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(UserFacingError.ConsoleMessage(ex, "Current user could not be loaded."));
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        _user = await userRepository.GetMeAsync(cancellationToken)
            .ConfigureAwait(false);
        NotifyCurrentUserChanged();
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
        if (HasExactEmptyClaim(claim))
            return true;
        return claims.Any(HasExactEmptyClaim);
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
        if (!IsAuthenticated)
            return false;

        return User.Claims?.Any((q) => q.Identifier == claim && q.Value == value) ?? false;
    }

    public bool CanCreateGameServers()
        => HasClaim(
            Claims.Creation.Server,
            Claims.Administrative.Server,
            Claims.Administrative.Hosting,
            Claims.Administrative.All);

    public bool CanAccessGameServers()
        => CanCreateGameServers()
           || HasClaim(Claims.ResourceBased.Server.All, Claims.ResourceBased.Server.View);

    private bool HasExactEmptyClaim(string claim)
        => User.Claims?.Any((q) => q.Identifier == claim && (q.Value is null || q.Value == string.Empty)) ?? false;

    public bool HasClaimOrAdmin(string claim, params string[] claims)
        => HasClaim(Claims.Administrative.All, claim, claims);

    public bool HasClaimOrEventAdmin() => HasClaim(Claims.Administrative.All, Claims.Administrative.Event);

    public bool HasClaimOrTerrainAdmin() => HasClaim(Claims.Administrative.All, Claims.Administrative.Terrain);

    public bool HasClaimOrModPackAdmin() => HasClaim(Claims.Administrative.All, Claims.Administrative.ModPack);

    public bool HasClaimOrUserAdmin() => HasClaim(Claims.Administrative.All, Claims.Administrative.User);

    public bool HasClaimOrRoleAdmin() => HasClaim(Claims.Administrative.All, Claims.Administrative.Role);

    public bool HasClaimOrWikiAdmin() => HasClaim(Claims.Administrative.All, Claims.Administrative.Wiki);

    public bool HasClaimOrVereinAdmin() => HasClaim(Claims.Administrative.All, Claims.Administrative.Verein);

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

    public bool HasClaimOrWikiAdmin(string claim, params string[] claims)
        => HasClaim(Claims.Administrative.All, Claims.Administrative.Wiki, claim, claims);

    public bool HasClaimOrVereinAdmin(string claim, params string[] claims)
        => HasClaim(Claims.Administrative.All, Claims.Administrative.Verein, claim, claims);

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

    public bool HasBaseClaimOrWikiAdmin(string claim, params string[] claims)
        => HasClaim(Claims.Administrative.All, Claims.Administrative.Wiki) || HasBaseClaim(claim, claims);

    public bool HasBaseClaimOrVereinAdmin(string claim, params string[] claims)
        => HasClaim(Claims.Administrative.All, Claims.Administrative.Verein) || HasBaseClaim(claim, claims);

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

    public bool HasResourceClaimOrWikiAdmin(string claim, string value)
        => HasClaim(Claims.Administrative.All, Claims.Administrative.Wiki) || HasResourceClaim(claim, value);

    public bool HasResourceClaimOrVereinAdmin(string claim, string value)
        => HasClaim(Claims.Administrative.All, Claims.Administrative.Verein) || HasResourceClaim(claim, value);

    private bool GetAdditionalBool(string propertyName)
    {
        if (!IsAuthenticated || User.AdditionalData is null)
            return false;

        var camelCaseName = char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
        if (TryGetAdditionalBool(camelCaseName, out var value))
            return value;
        return TryGetAdditionalBool(propertyName, out value) && value;
    }

    private bool TryGetAdditionalBool(string propertyName, out bool value)
    {
        value = false;
        if (!User.AdditionalData.TryGetValue(propertyName, out var raw))
            return false;

        switch (raw)
        {
            case bool boolValue:
                value = boolValue;
                return true;
            case JsonElement { ValueKind: JsonValueKind.True }:
                value = true;
                return true;
            case JsonElement { ValueKind: JsonValueKind.False }:
                return true;
            case string stringValue when bool.TryParse(stringValue, out var parsed):
                value = parsed;
                return true;
            default:
                return false;
        }
    }

    private string? GetAdditionalString(string propertyName)
    {
        if (!IsAuthenticated || User.AdditionalData is null)
            return null;
        if (!User.AdditionalData.TryGetValue(propertyName, out var raw))
            return null;

        return raw switch
        {
            null => null,
            string value => value,
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
            JsonElement { ValueKind: JsonValueKind.Number } element => element.GetRawText(),
            _ => raw.ToString(),
        };
    }

    private void NotifyCurrentUserChanged()
        => CurrentUserChanged?.Invoke(this, EventArgs.Empty);
}

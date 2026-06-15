using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Authority.Extern;
using X39.UnitedTacticalForces.Api.ExtensionMethods;
using X39.UnitedTacticalForces.Api.Services;
using X39.Util;
using Claim = X39.UnitedTacticalForces.Database.Authority.Claim;

namespace X39.UnitedTacticalForces.Api.Helpers;

public class ValidationHelper
{
    public static async Task OnSignedIn(CookieSignedInContext? context)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));

        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var steamRepository = context.HttpContext.RequestServices.GetRequiredService<SteamRepository>();
        var httpClient = context.HttpContext.RequestServices.GetRequiredService<HttpClient>();
        var apiDbContext = context.HttpContext.RequestServices.GetRequiredService<DatabaseContext>();
        if (context.Principal is null)
            throw new NullReferenceException("CookieSignedInContext.Principal is null");
        if (context.Principal.TryDiscordIdentity(out _))
            // Skip, we can only register using Steam. Maybe change this in the future
            // (account merging would be required)
            return;
        if (!context.Principal.TrySteamIdentity(out var steamIdentity))
            throw new Exception();
        if (!steamIdentity.IsAuthenticated)
            throw new Exception();
        if (!steamIdentity.TryGetSteamId64(out var steamId64))
            throw new Exception();
        if (steamId64 is 0)
            throw new Exception();
        var user = await apiDbContext.Users
            .Include(e => e.Claims)!
            .Include(e => e.Roles)!
            .ThenInclude(e=> e.Claims)
            .SingleOrDefaultAsync(user => user.Steam.Id64 == steamId64 && !user.IsDeleted)
            .ConfigureAwait(false);
        if (user is null)
        {
            var profile = await steamRepository.GetProfileAsync(steamId64)
                .ConfigureAwait(false);
            var avatar = Array.Empty<byte>();
            var avatarMimeType = string.Empty;
            if (profile.AvatarUrl.IsNotNullOrWhiteSpace())
            {
                avatar = await httpClient.GetByteArrayAsync(profile.AvatarUrl)
                    .ConfigureAwait(false);
                avatarMimeType = "image/jpeg";
            }

            user = new User
            {
                PrimaryKey     = Guid.NewGuid(),
                Avatar         = avatar,
                AvatarMimeType = avatarMimeType,
                EMail          = null,
                Nickname       = profile.Nickname,
                Roles          = new List<Role>(),
                Steam          = new SteamUser {Id64 = steamId64},
                Discord        = new DiscordUser{Username = string.Empty},
                IsVerified = Convert.ToBoolean(
                    configuration[Constants.Configuration.General.AutoVerifyNewUsers] ?? "False"),
            };
            await apiDbContext.Users.AddAsync(user)
                .ConfigureAwait(false);
            await apiDbContext.SaveChangesAsync()
                .ConfigureAwait(false);
        }
    }

    public static async Task OnValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var apiDbContext = context.HttpContext.RequestServices.GetRequiredService<DatabaseContext>();

        if (context.Principal is null)
        {
            await RejectPrincipalAndSignOutAsync(context).ConfigureAwait(false);
            return;
        }

        User? user = null;
        if (context.Principal.TryDiscordIdentity(out var discordIdentity))
        {
            var userIdString = discordIdentity.Claims.FirstOrDefault(q => q.Type == Constants.ClaimTypes.UserId)
                ?.Value;
            if (userIdString is null)
            {
                await RejectPrincipalAndSignOutAsync(context).ConfigureAwait(false);
                return;
            }

            if (!Guid.TryParse(userIdString, out var userId))
            {
                await RejectPrincipalAndSignOutAsync(context).ConfigureAwait(false);
                return;
            }

            user = await apiDbContext.Users
                .Include(e => e.Claims)!
                .Include(e => e.Roles)!
                .ThenInclude(e=> e.Claims)
                .SingleOrDefaultAsync(u => u.PrimaryKey == userId, context.HttpContext.RequestAborted)
                .ConfigureAwait(false);
            if (user is { IsDeleted: true } or { IsBanned: true })
            {
                await RejectPrincipalAndSignOutAsync(context).ConfigureAwait(false);
                return;
            }

            if (user is not null)
            {
                var hasChanges = false;
                if (discordIdentity.Claims.FirstOrDefault(q => q.Type == ClaimTypes.NameIdentifier)
                        ?.Value is { } discordIdString
                    && ulong.TryParse(discordIdString, out var discordId)
                    && user.Discord.Id != discordId)
                {
                    user.Discord.Id = discordId;
                    hasChanges      = true;
                }

                if (discordIdentity.Claims.FirstOrDefault(q => q.Type == ClaimTypes.Name)
                        ?.Value is { } discordUsername
                    && user.Discord.Username != discordUsername)
                {
                    user.Discord.Username = discordUsername;
                    hasChanges            = true;
                }

                if (hasChanges)
                {
                    await apiDbContext.SaveChangesAsync(context.HttpContext.RequestAborted)
                        .ConfigureAwait(false);
                }
            }
        }
        else if (context.Principal.TrySteamIdentity(out var steamIdentity))
        {
            if (!steamIdentity.IsAuthenticated)
            {
                await RejectPrincipalAndSignOutAsync(context).ConfigureAwait(false);
                return;
            }

            if (!TryReadSteamId64(steamIdentity, out var steamId64))
            {
                await RejectPrincipalAndSignOutAsync(context).ConfigureAwait(false);
                return;
            }

            user = await apiDbContext.Users
                .Include(e => e.Claims)!
                .Include(e => e.Roles)!
                .ThenInclude(e=> e.Claims)
                .SingleOrDefaultAsync(
                    user => user.Steam.Id64 == steamId64 && !user.IsDeleted,
                    context.HttpContext.RequestAborted)
                .ConfigureAwait(false);
        }
        else if (context.Principal.TryApiIdentity(out _))
        {
            if (!context.Principal.TryGetUserId(out var userId))
            {
                await RejectPrincipalAndSignOutAsync(context).ConfigureAwait(false);
                return;
            }

            user = await apiDbContext.Users
                .Include(e => e.Claims)!
                .Include(e => e.Roles)!
                .ThenInclude(e=> e.Claims)
                .SingleOrDefaultAsync(u => u.PrimaryKey == userId, context.HttpContext.RequestAborted)
                .ConfigureAwait(false);
        }

        if (user is null || user.IsDeleted || user.IsBanned)
        {
            await RejectPrincipalAndSignOutAsync(context).ConfigureAwait(false);
            return;
        }

        var identity = await user.ToIdentityAsync(cancellationToken: context.HttpContext.RequestAborted)
            .ConfigureAwait(false);
        var claimsPrincipal = new ClaimsPrincipal(identity);
        context.ReplacePrincipal(claimsPrincipal);
    }

    private static bool TryReadSteamId64(ClaimsIdentity steamIdentity, out ulong steamId64)
    {
        try
        {
            return steamIdentity.TryGetSteamId64(out steamId64);
        }
        catch (FormatException)
        {
            steamId64 = default;
            return false;
        }
    }

    private static async Task RejectPrincipalAndSignOutAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(Constants.AuthorizationSchemas.Cookie)
            .ConfigureAwait(false);
    }
}

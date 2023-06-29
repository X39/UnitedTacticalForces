using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.Data.Authority.Extern;
using X39.UnitedTacticalForces.Api.ExtensionMethods;
using X39.UnitedTacticalForces.Api.Services;
using X39.Util;

namespace X39.UnitedTacticalForces.Api.Helpers;

public class ValidationHelper
{
    private const int SteamIdStartIndex = 37;

    public static async Task OnSignedIn(CookieSignedInContext? context)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));

        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var steamRepository = context.HttpContext.RequestServices.GetRequiredService<SteamRepository>();
        var httpClient = context.HttpContext.RequestServices.GetRequiredService<HttpClient>();
        var apiDbContext = context.HttpContext.RequestServices.GetRequiredService<ApiDbContext>();
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
            .Include((e) => e.Roles)
            .SingleOrDefaultAsync((user) => user.Steam.Id64 == steamId64 && !user.IsDeleted)
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
        var apiDbContext = context.HttpContext.RequestServices.GetRequiredService<ApiDbContext>();

        if (context.Principal is null)
            throw new NullReferenceException("CookieSignedInContext.Principal is null");
        User? user = null;
        if (context.Principal.TryDiscordIdentity(out var discordIdentity))
        {
            var userIdString = discordIdentity.Claims.FirstOrDefault((q) => q.Type == Constants.ClaimTypes.UserId)
                ?.Value;
            if (userIdString is null)
                throw new Exception();
            if (!Guid.TryParse(userIdString, out var userId))
                throw new Exception();
            user = await apiDbContext.Users
                .Include((e) => e.Roles)
                .SingleOrDefaultAsync((u) => u.PrimaryKey == userId && !u.IsDeleted)
                .ConfigureAwait(false);
            if (user is not null)
            {
                var hasChanges = false;
                if (discordIdentity.Claims.FirstOrDefault((q) => q.Type == ClaimTypes.NameIdentifier)
                        ?.Value is { } discordIdString
                    && ulong.TryParse(discordIdString, out var discordId)
                    && user.Discord.Id != discordId)
                {
                    user.Discord.Id = discordId;
                    hasChanges      = true;
                }

                if (discordIdentity.Claims.FirstOrDefault((q) => q.Type == ClaimTypes.Name)
                        ?.Value is { } discordUsername
                    && user.Discord.Username != discordUsername)
                {
                    user.Discord.Username = discordUsername;
                    hasChanges            = true;
                }

                if (hasChanges)
                {
                    await apiDbContext.SaveChangesAsync()
                        .ConfigureAwait(false);
                }
            }
        }
        else if (context.Principal.TrySteamIdentity(out var steamIdentity))
        {
            if (!steamIdentity.IsAuthenticated)
                throw new Exception();
            if (!steamIdentity.TryGetSteamId64(out var steamId64))
                throw new Exception();
            user = await apiDbContext.Users
                .Include((e) => e.Roles)
                .SingleOrDefaultAsync((user) => user.Steam.Id64 == steamId64 && !user.IsDeleted)
                .ConfigureAwait(false);
        }

        if (user is null)
            throw new Exception();
        var identity = await user.ToIdentityAsync().ConfigureAwait(false);
        var claimsPrincipal = new ClaimsPrincipal(identity);
        context.ReplacePrincipal(claimsPrincipal);
    }
}
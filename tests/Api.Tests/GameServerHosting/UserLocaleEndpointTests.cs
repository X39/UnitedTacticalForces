using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using X39.UnitedTacticalForces.Api.Controllers;
using X39.UnitedTacticalForces.Api.DTO.Updates;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// D-12 proof that <c>POST users/me/locale</c> records only a normalized English or German language on the caller's
/// own user and rejects everything else without storing anything. The tests run the real
/// <see cref="UsersController" /> against in-memory SQLite.
/// </summary>
public sealed class UserLocaleEndpointTests
{
    /// <summary>
    /// A supported locale is normalized to its language code, stored on the caller and answered with 204.
    /// </summary>
    /// <param name="locale">The posted locale.</param>
    /// <param name="expected">The language code expected to be stored.</param>
    /// <returns>A task completing after the stored value was verified.</returns>
    [Theory]
    [InlineData("de", "de")]
    [InlineData("en-GB", "en")]
    [InlineData(" DE_at ", "de")]
    public async Task SupportedLocale_IsStoredNormalized(string locale, string expected)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);

        var result = await CreateController(store, HostingTestData.Principal(seed.User))
            .UpdateMeLocaleAsync(new UserLocaleUpdate { Locale = locale }, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(expected, await StoredLocaleAsync(store, seed.User.PrimaryKey));
    }

    /// <summary>
    /// An unsupported, empty or oversized locale is answered with 400 and leaves the stored language untouched.
    /// </summary>
    /// <param name="locale">The rejected locale.</param>
    /// <returns>A task completing after the rejection was verified.</returns>
    [Theory]
    [InlineData("fr")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    public async Task UnsupportedLocale_ReturnsBadRequestAndStoresNothing(string locale)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.User.Locale = "de";
        await store.DbContext.SaveChangesAsync();

        var result = await CreateController(store, HostingTestData.Principal(seed.User))
            .UpdateMeLocaleAsync(new UserLocaleUpdate { Locale = locale }, CancellationToken.None);

        Assert.IsType<BadRequestResult>(result);
        Assert.Equal("de", await StoredLocaleAsync(store, seed.User.PrimaryKey));
    }

    /// <summary>
    /// A principal without a user id is answered with 401.
    /// </summary>
    /// <returns>A task completing after the denial was verified.</returns>
    [Fact]
    public async Task PrincipalWithoutUserId_ReturnsUnauthorized()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);

        var result = await CreateController(store, new ClaimsPrincipal(new ClaimsIdentity()))
            .UpdateMeLocaleAsync(new UserLocaleUpdate { Locale = "de" }, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
        Assert.Null(await StoredLocaleAsync(store, seed.User.PrimaryKey));
    }

    /// <summary>
    /// Creates the real users controller on the store for one principal.
    /// </summary>
    /// <param name="store">The test store the controller reads and writes.</param>
    /// <param name="principal">The caller of every request.</param>
    /// <returns>The controller with an HTTP context for the principal.</returns>
    private static UsersController CreateController(HostingTestStore store, ClaimsPrincipal principal)
        => new(NullLogger<UsersController>.Instance, store.DbContext)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal },
            },
        };

    /// <summary>
    /// Reads the stored locale of a user without using the change tracker.
    /// </summary>
    /// <param name="store">The store to read from.</param>
    /// <param name="userId">The user to read.</param>
    /// <returns>A task resolving to the stored locale, or <see langword="null"/> when none is stored.</returns>
    private static async Task<string?> StoredLocaleAsync(HostingTestStore store, Guid userId)
        => (await store.DbContext.Users.AsNoTracking().SingleAsync(q => q.PrimaryKey == userId)).Locale;
}

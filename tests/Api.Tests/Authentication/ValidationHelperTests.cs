using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Authority.Extern;
using X39.UnitedTacticalForces.Api.ExtensionMethods;
using X39.UnitedTacticalForces.Api.Helpers;
using Xunit;
using DataClaim = X39.UnitedTacticalForces.Database.Authority.Claim;
using SecurityClaim = System.Security.Claims.Claim;

namespace Api.Tests.Authentication;

public class ValidationHelperTests
{
    private const ulong SteamId64 = 76561198000000000;

    [Fact]
    public async Task OnValidatePrincipal_ReplacesSteamPrincipalForActiveUser()
    {
        var user = CreateUser();
        await using var context = await ValidationTestContext.CreateAsync(user);
        var principalContext = await context.ValidateAsync(CreateSteamPrincipal(SteamId64));

        Assert.NotNull(principalContext.Principal);
        Assert.Equal(0, context.SignOutCount);
        Assert.True(principalContext.Principal.TryGetUserId(out var userId));
        Assert.Equal(user.PrimaryKey, userId);
        Assert.Contains(
            principalContext.Principal.Claims,
            q => q.Type == Claims.Administrative.All && q.Value == string.Empty);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task OnValidatePrincipal_RejectsAndSignsOutUnusableSteamUser(bool isDeleted, bool isBanned)
    {
        await using var context = await ValidationTestContext.CreateAsync(
            CreateUser(isDeleted: isDeleted, isBanned: isBanned));

        var principalContext = await context.ValidateAsync(CreateSteamPrincipal(SteamId64));

        Assert.Null(principalContext.Principal);
        Assert.Equal(1, context.SignOutCount);
        Assert.Equal(Constants.AuthorizationSchemas.Cookie, context.LastSignOutScheme);
    }

    [Fact]
    public async Task OnValidatePrincipal_RejectsAndSignsOutMissingSteamUser()
    {
        await using var context = await ValidationTestContext.CreateAsync();

        var principalContext = await context.ValidateAsync(CreateSteamPrincipal(SteamId64));

        Assert.Null(principalContext.Principal);
        Assert.Equal(1, context.SignOutCount);
        Assert.Equal(Constants.AuthorizationSchemas.Cookie, context.LastSignOutScheme);
    }

    [Fact]
    public async Task OnValidatePrincipal_RejectsAndSignsOutMalformedSteamPrincipal()
    {
        await using var context = await ValidationTestContext.CreateAsync(CreateUser());

        var principalContext = await context.ValidateAsync(CreateSteamPrincipal("not-a-steam-id"));

        Assert.Null(principalContext.Principal);
        Assert.Equal(1, context.SignOutCount);
        Assert.Equal(Constants.AuthorizationSchemas.Cookie, context.LastSignOutScheme);
    }

    [Fact]
    public async Task OnValidatePrincipal_RejectsAndSignsOutDeletedApiPrincipal()
    {
        var user = CreateUser(isDeleted: true);
        await using var context = await ValidationTestContext.CreateAsync(user);

        var principalContext = await context.ValidateAsync(CreateApiPrincipal(user.PrimaryKey));

        Assert.Null(principalContext.Principal);
        Assert.Equal(1, context.SignOutCount);
        Assert.Equal(Constants.AuthorizationSchemas.Cookie, context.LastSignOutScheme);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task ToIdentityAsync_ThrowsForUnusableUsers(bool isDeleted, bool isBanned)
    {
        var user = CreateUser(isDeleted: isDeleted, isBanned: isBanned);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await user.ToIdentityAsync().ConfigureAwait(false));
    }

    private static ClaimsPrincipal CreateSteamPrincipal(ulong steamId64)
        => CreateSteamPrincipal($"https://steamcommunity.com/openid/id/{steamId64}");

    private static ClaimsPrincipal CreateSteamPrincipal(string nameIdentifier)
    {
        var identity = new ClaimsIdentity(
            [
                new SecurityClaim(ClaimTypes.NameIdentifier, nameIdentifier),
            ],
            Constants.AuthorizationSchemas.Steam);
        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal CreateApiPrincipal(Guid userId)
    {
        var identity = new ClaimsIdentity(
            [
                new SecurityClaim(ClaimTypes.Name, userId.ToString()),
            ],
            Constants.AuthorizationSchemas.Api);
        return new ClaimsPrincipal(identity);
    }

    private static User CreateUser(bool isDeleted = false, bool isBanned = false)
    {
        return new User
        {
            PrimaryKey = Guid.NewGuid(),
            Nickname   = "Authentication validation test user",
            IsVerified = true,
            IsDeleted  = isDeleted,
            IsBanned   = isBanned,
            Steam      = new SteamUser { Id64 = SteamId64 },
            Discord    = new DiscordUser { Username = "auth-test-user" },
            Roles      = [],
            Claims =
            [
                new DataClaim
                {
                    Category    = "Testing",
                    Title       = "Test administrator",
                    Identifier  = Claims.Administrative.All,
                    Description = "Administrative claim for validation helper tests.",
                    Value       = string.Empty,
                    ValueType   = ClaimValueTypes.String,
                },
            ],
        };
    }

    private sealed class ValidationTestContext : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider  _serviceProvider;
        private readonly SignOutTracker   _signOutTracker;

        private ValidationTestContext(
            SqliteConnection connection,
            ServiceProvider serviceProvider,
            SignOutTracker signOutTracker)
        {
            _connection = connection;
            _serviceProvider = serviceProvider;
            _signOutTracker = signOutTracker;
        }

        public int SignOutCount => _signOutTracker.SignOutCount;

        public string? LastSignOutScheme => _signOutTracker.LastSignOutScheme;

        public static async Task<ValidationTestContext> CreateAsync(User? user = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync().ConfigureAwait(false);

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<SignOutTracker>();
            services.AddDbContext<DatabaseContext, NodaTimeSqliteApiDbContext>(
                options => options.UseSqlite(connection));
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, SignOutTrackingAuthenticationHandler>(
                    Constants.AuthorizationSchemas.Cookie,
                    _ => { });

            var serviceProvider = services.BuildServiceProvider();
            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
                await dbContext.Database.EnsureCreatedAsync().ConfigureAwait(false);
                if (user is not null)
                {
                    dbContext.Users.Add(user);
                    await dbContext.SaveChangesAsync().ConfigureAwait(false);
                }
            }

            return new ValidationTestContext(
                connection,
                serviceProvider,
                serviceProvider.GetRequiredService<SignOutTracker>());
        }

        public async Task<CookieValidatePrincipalContext> ValidateAsync(ClaimsPrincipal principal)
        {
            using var scope = _serviceProvider.CreateScope();
            var httpContext = new DefaultHttpContext
            {
                RequestServices = scope.ServiceProvider,
            };
            var principalContext = new CookieValidatePrincipalContext(
                httpContext,
                new AuthenticationScheme(
                    Constants.AuthorizationSchemas.Cookie,
                    Constants.AuthorizationSchemas.Cookie,
                    typeof(SignOutTrackingAuthenticationHandler)),
                new CookieAuthenticationOptions(),
                new AuthenticationTicket(principal, Constants.AuthorizationSchemas.Cookie));

            await ValidationHelper.OnValidatePrincipal(principalContext).ConfigureAwait(false);
            return principalContext;
        }

        public async ValueTask DisposeAsync()
        {
            await _serviceProvider.DisposeAsync().ConfigureAwait(false);
            await _connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed class SignOutTracker
    {
        public int SignOutCount { get; set; }

        public string? LastSignOutScheme { get; set; }
    }

    private sealed class SignOutTrackingAuthenticationHandler :
        AuthenticationHandler<AuthenticationSchemeOptions>,
        IAuthenticationSignOutHandler
    {
        private readonly SignOutTracker _signOutTracker;

        public SignOutTrackingAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            SignOutTracker signOutTracker)
            : base(options, logger, encoder)
        {
            _signOutTracker = signOutTracker;
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
            => Task.FromResult(AuthenticateResult.NoResult());

        public Task SignOutAsync(AuthenticationProperties? properties)
        {
            _signOutTracker.SignOutCount++;
            _signOutTracker.LastSignOutScheme = Scheme.Name;
            return Task.CompletedTask;
        }
    }
}

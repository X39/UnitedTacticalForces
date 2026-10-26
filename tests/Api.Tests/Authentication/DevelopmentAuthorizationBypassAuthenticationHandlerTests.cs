using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api;
using X39.UnitedTacticalForces.Api.Authentication;
using X39.UnitedTacticalForces.Api.Authorization.Abstraction;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Api.ExtensionMethods;
using Xunit;
using DataClaim = X39.UnitedTacticalForces.Database.Authority.Claim;

namespace Api.Tests.Authentication;

/// <summary>Verifies development-only authentication and the Release build's unconditional bypass rejection.</summary>
public class DevelopmentAuthorizationBypassAuthenticationHandlerTests
{
    private static readonly Guid DefaultUserId = Guid.Parse(Constants.AuthorizationBypass.DefaultUserId);

    /// <summary>Checks the configured administrator in Debug and rejects the enabled bypass in Release.</summary>
    /// <returns>A task that completes after the build-specific authentication contract is verified.</returns>
    [Fact]
    public async Task AuthenticateAsync_ReturnsConfiguredApiUserWithAdminClaim()
    {
        await using var context = await AuthenticationTestContext.CreateAsync(CreateUser(DefaultUserId));

#if DEBUG
        var result = await context.AuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Principal);
        Assert.True(result.Principal.TryGetUserId(out var authenticatedUserId));
        Assert.Equal(DefaultUserId, authenticatedUserId);
        Assert.Contains(
            result.Principal.Claims,
            q => q.Type == Claims.Administrative.All && q.Value == string.Empty);
#else
        await AssertReleaseBuildRejectsBypassAsync(context);
#endif
    }

    /// <summary>Checks Debug resource authorization while preserving the Release bypass prohibition.</summary>
    /// <returns>A task that completes after the build-specific authorization behavior is verified.</returns>
    [Fact]
    public async Task AuthenticateAsync_PrincipalSatisfiesResourcePolicy()
    {
        await using var context = await AuthenticationTestContext.CreateAsync(CreateUser(DefaultUserId));
#if DEBUG
        var result = await context.AuthenticateAsync();
        Assert.True(result.Succeeded);

        var authorizationResult = await context.AuthorizeAsync(
            result.Principal!,
            Claims.ResourceBased.ModPack.Modify);

        Assert.True(authorizationResult.Succeeded);
#else
        await AssertReleaseBuildRejectsBypassAsync(context);
#endif
    }

    /// <summary>Rejects an absent Debug user and an enabled Release bypass before user lookup.</summary>
    /// <returns>A task that completes after the invalid authentication request is rejected.</returns>
    [Fact]
    public async Task AuthenticateAsync_FailsWhenConfiguredUserDoesNotExist()
    {
        await using var context = await AuthenticationTestContext.CreateAsync();

#if DEBUG
        var result = await context.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failure);
#else
        await AssertReleaseBuildRejectsBypassAsync(context);
#endif
    }

    /// <summary>Rejects unusable Debug users and the enabled bypass in every Release user state.</summary>
    /// <param name="isDeleted">Whether the configured fixture user has been deleted.</param>
    /// <param name="isBanned">Whether the configured fixture user has been banned.</param>
    /// <returns>A task that completes after authentication is rejected.</returns>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task AuthenticateAsync_FailsWhenConfiguredUserCannotBeUsed(bool isDeleted, bool isBanned)
    {
        await using var context = await AuthenticationTestContext.CreateAsync(
            CreateUser(DefaultUserId, isDeleted, isBanned));

#if DEBUG
        var result = await context.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failure);
#else
        await AssertReleaseBuildRejectsBypassAsync(context);
#endif
    }

    /// <summary>Requires the production artifact to reject the development bypass at its build guard.</summary>
    /// <param name="context">The fixture with bypass explicitly enabled and a development environment.</param>
    /// <returns>A task that completes after the Release-only rejection and its reason are verified.</returns>
    private static async Task AssertReleaseBuildRejectsBypassAsync(AuthenticationTestContext context)
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(context.AuthenticateAsync);
        Assert.Contains("only be enabled in DEBUG builds", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateAuthorizationBypassConfiguration_RejectsNonDevelopmentEnvironment()
    {
        var configuration = AuthenticationTestContext.CreateConfiguration(enabled: true);
        var environment = new TestHostEnvironment
        {
            EnvironmentName = Environments.Production,
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => Program.ValidateAuthorizationBypassConfiguration(configuration, environment));
        Assert.Contains("Development", exception.Message, StringComparison.Ordinal);
    }

    private static User CreateUser(Guid userId, bool isDeleted = false, bool isBanned = false)
    {
        return new User
        {
            PrimaryKey = userId,
            Nickname = "LLM visual test admin",
            IsVerified = true,
            IsDeleted = isDeleted,
            IsBanned = isBanned,
            Roles = [],
            Claims =
            [
                new DataClaim
                {
                    Category = "Testing",
                    Title = "Test administrator",
                    Identifier = Claims.Administrative.All,
                    Description = "Administrative claim for development authorization bypass tests.",
                    Value = string.Empty,
                    ValueType = ClaimValueTypes.String,
                },
            ],
        };
    }

    private sealed class AuthenticationTestContext : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider  _serviceProvider;

        private AuthenticationTestContext(SqliteConnection connection, ServiceProvider serviceProvider)
        {
            _connection = connection;
            _serviceProvider = serviceProvider;
        }

        public static async Task<AuthenticationTestContext> CreateAsync(
            User? user = null,
            bool enabled = true,
            string? environmentName = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync().ConfigureAwait(false);
            environmentName ??= Environments.Development;
            var configuration = CreateConfiguration(enabled);
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddSingleton<IHostEnvironment>(
                new TestHostEnvironment
                {
                    EnvironmentName = environmentName,
                });
            services.AddDbContext<DatabaseContext, NodaTimeSqliteApiDbContext>(
                options => options.UseSqlite(connection));
            services.AddAuthentication(
                    options => { options.DefaultScheme = Constants.AuthorizationSchemas.DevelopmentBypass; })
                .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthorizationBypassAuthenticationHandler>(
                    Constants.AuthorizationSchemas.DevelopmentBypass,
                    _ => { });
            services.AddAppAuthorization();

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

            return new AuthenticationTestContext(connection, serviceProvider);
        }

        public static IConfiguration CreateConfiguration(bool enabled)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        [Constants.Configuration.Testing.AuthorizationBypass.Enabled] =
                            enabled.ToString(CultureInfo.InvariantCulture),
                        [Constants.Configuration.Testing.AuthorizationBypass.UserId] =
                            Constants.AuthorizationBypass.DefaultUserId,
                    })
                .Build();
        }

        public async Task<AuthenticateResult> AuthenticateAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var httpContext = new DefaultHttpContext
            {
                RequestServices = scope.ServiceProvider,
            };
            return await httpContext.AuthenticateAsync().ConfigureAwait(false);
        }

        public async Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal principal, string policyName)
        {
            using var scope = _serviceProvider.CreateScope();
            var httpContext = new DefaultHttpContext
            {
                RequestServices = scope.ServiceProvider,
                User = principal,
            };
            var authorizationService = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
            return await authorizationService.AuthorizeAsync(principal, httpContext, policyName)
                .ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            await _serviceProvider.DisposeAsync().ConfigureAwait(false);
            await _connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Api.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

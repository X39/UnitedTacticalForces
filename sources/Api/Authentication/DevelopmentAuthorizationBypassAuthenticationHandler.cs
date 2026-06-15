using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Database;

namespace X39.UnitedTacticalForces.Api.Authentication;

internal sealed class DevelopmentAuthorizationBypassAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly DatabaseContext    _apiDbContext;
    private readonly IConfiguration  _configuration;
    private readonly IHostEnvironment _environment;

    public DevelopmentAuthorizationBypassAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        DatabaseContext apiDbContext,
        IConfiguration configuration,
        IHostEnvironment environment)
        : base(options, logger, encoder)
    {
        _apiDbContext = apiDbContext;
        _configuration = configuration;
        _environment = environment;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Program.IsAuthorizationBypassEnabled(_configuration))
            return AuthenticateResult.Fail("Development authorization bypass is disabled.");

        Program.ValidateAuthorizationBypassConfiguration(_configuration, _environment);
        var userId = Program.GetAuthorizationBypassUserId(_configuration);
        var user = await _apiDbContext.Users
            .Include(q => q.Claims)
            .Include(q => q.Roles!)
            .ThenInclude(q => q.Claims)
            .SingleOrDefaultAsync(q => q.PrimaryKey == userId && !q.IsDeleted, Context.RequestAborted)
            .ConfigureAwait(false);
        if (user is null)
            return AuthenticateResult.Fail("The configured development authorization bypass user was not found.");
        if (user.IsBanned)
            return AuthenticateResult.Fail("The configured development authorization bypass user is banned.");

        user.Claims ??= [];
        user.Roles ??= [];
        foreach (var role in user.Roles)
            role.Claims ??= [];

        var identity = await user.ToIdentityAsync(cancellationToken: Context.RequestAborted)
            .ConfigureAwait(false);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }
}

using System.Globalization;
using System.Security.Claims;
using System.Text.Json.Serialization;
using AspNet.Security.OAuth.Discord;
using AspNet.Security.OpenId.Steam;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using X39.UnitedTacticalForces.Api.Authentication;
using X39.UnitedTacticalForces.Api.Authorization.Abstraction;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Api.ExtensionMethods;
using X39.UnitedTacticalForces.Api.Helpers;
using X39.UnitedTacticalForces.Api.HostedServices;
using X39.UnitedTacticalForces.Api.ModelBinding;
using X39.UnitedTacticalForces.Api.OpenApi;
using X39.UnitedTacticalForces.Api.Services;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.Media;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Api.Services.WorkerProtocol;
using X39.Util;
using X39.Util.DependencyInjection;

namespace X39.UnitedTacticalForces.Api;

internal class Program
{
    public static async Task Main(string[] args)
    {
        // https://github.com/dotnet/aspnetcore/issues/60628
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        var builder = WebApplication.CreateBuilder(args);
        builder.Configuration.AddJsonFile("appsettings.secret.json", optional: true);
        builder.Configuration.AddJsonFile("appsettings.user.json", optional: true);
        builder.Configuration.AddEnvironmentVariables();
        builder.Configuration.AddCommandLine(args);

        // Add services to the container.

        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.AddServiceDefaults();
        AddServices(builder);
        AddForwardedHeaders(builder);
        AddAuth(builder);
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        var app = builder.Build();
        await MigrateAndSeedDataAsync(app);
        app.UseForwardedHeaders();
        app.UsePathBase(app.Configuration[Constants.Configuration.General.BasePath]);
        UseRestrictedCors(app);

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Development environment detected");
            app.UseDeveloperExceptionPage();
            app.MapOpenApi();
            app.MapScalarApiReference();
            logger.LogInformation("Scalar URL: {SwaggerUrl} (port may be different)", "http://localhost:80/scalar/v1");
        }
        

        app.UseHttpsRedirection();

        app.UseWebSockets();
        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapDefaultEndpoints();
        app.MapControllers();

        await app.RunAsync().ConfigureAwait(false);
    }

    private static async Task MigrateAndSeedDataAsync(WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await using var dbContext = scope.ServiceProvider.GetService<DatabaseContext>();
        await dbContext!.Database.MigrateAsync();
        var mediaAssetService = scope.ServiceProvider.GetRequiredService<IMediaAssetService>();
        await SiteImageSettingSeeder.SeedAsync(
                dbContext,
                mediaAssetService
            )
            .ConfigureAwait(false);
        if (app.Environment.IsDevelopment())
            await DebugDemoDataSeeder.SeedAsync(dbContext, mediaAssetService).ConfigureAwait(false);
        await HostingFinanceClaimSeeder.SeedAsync(dbContext).ConfigureAwait(false);
    }

    private static void AddServices(WebApplicationBuilder builder)
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddOpenApi(options =>
            {
                options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
                options.AddOperationTransformer<XAnonymousAnnotationOperationTransformer>();
                options.AddSchemaTransformer<FixEnumsSchemaTransformer>();
                options.AddSchemaTransformer<NodaTimeSchemaTransformer>();
                options.AddSchemaTransformer<FixBySuppressionOfNumberTypesExposingString>();
                options.AddOperationTransformer<FixFileResultsOperationTransformer>();
                options.AddSchemaTransformer<FixFileResultsSchemaTransformer>();
                options.AddSchemaTransformer<KiotaFixNullableNumberTypeAnnotation>();
            }
        );
        builder.Services.AddAttributedServicesFromAssemblyOf<Program>(builder.Configuration);
        builder.Services.Configure<LifecycleWorkerOptions>(
            builder.Configuration.GetSection(LifecycleWorkerOptions.SectionName));
        builder.Services.Configure<WorkerProvisioningOptions>(
            builder.Configuration.GetSection(WorkerProvisioningOptions.SectionName));
        builder.Services.Configure<PersistentServerStorageOptions>(
            builder.Configuration.GetSection(PersistentServerStorageOptions.SectionName));
        builder.Services.Configure<ProviderReconciliationOptions>(
            builder.Configuration.GetSection(ProviderReconciliationOptions.SectionName));
        builder.Services.Configure<HostingFinanceOptions>(
            builder.Configuration.GetSection(HostingFinanceOptions.SectionName));
        builder.Services.AddDbContextFactory<DatabaseContext>(
            options =>
            {
                options.UseNpgsql(
                    builder.Configuration.GetConnectionString("database"),
                    npgsqlOptions => npgsqlOptions.UseNodaTime());
                if (builder.Environment.IsDevelopment())
                {
                    options.EnableDetailedErrors();
                    options.EnableSensitiveDataLogging();
                }
            });
        builder.Services.AddSingleton<DiscordBot>();
        builder.Services.AddHostedService<DiscordBot>(serviceProvider => serviceProvider.GetRequiredService<DiscordBot>());
        builder.Services.AddHostedService<GameServerControlPlaneWorker>();
        builder.Services.AddHostedService<DeploymentHealthReconciliationWorker>();
        builder.Services.AddHostedService<HostingCostReconciliationWorker>();
        builder.Services.AddHostedService<ProviderResourceReconciliationWorker>();
        builder.Services.AddSingleton(new BaseUrl(
            builder.Configuration[Constants.Configuration.General.ApiBaseUrl] ?? string.Empty,
            builder.Configuration[Constants.Configuration.General.ClientBaseUrl] ?? string.Empty));

        builder.Services.AddCors();
        builder.Services.AddRateLimiter(WorkerProtocolRateLimitPolicy.Configure);
        builder.Services.AddControllers(options => options.ModelBinderProviders.Insert(0, new NodaTimeModelBinderProvider()))
            .AddJsonOptions(
                jsonOptions =>
                {
                    // jsonOptions.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.Preserve;
                    jsonOptions.JsonSerializerOptions.MaxDepth         = 64;
                    jsonOptions.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                    jsonOptions.JsonSerializerOptions.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
                });
        builder.Services.AddHttpClient();
    }

    private static void AddForwardedHeaders(WebApplicationBuilder builder)
    {
        builder.Services.Configure<ForwardedHeadersOptions>(
            options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.ForwardLimit     = 1;
            });
    }

    private static void UseRestrictedCors(WebApplication app)
    {
        var allowedOrigins = GetAllowedCorsOrigins(app.Configuration, app.Environment).ToArray();
        app.UseCors(
            cors =>
            {
                if (allowedOrigins.Length is 0)
                    return;
                cors.WithOrigins(allowedOrigins)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
            });
    }

    private static IEnumerable<string> GetAllowedCorsOrigins(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        var candidates = origins.Append(configuration[Constants.Configuration.General.ClientBaseUrl]);
        if (environment.IsDevelopment())
        {
            candidates = candidates.Concat(["https://localhost:7086", "http://localhost:5022"]);
        }

        return candidates
            .Select(NormalizeOrigin)
            .Where(q => q is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)!;
    }

    private static string? NormalizeOrigin(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            ? uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped)
            : null;
    }

    private static void AddAuth(WebApplicationBuilder builder)
    {
        var authorizationBypassEnabled = IsAuthorizationBypassEnabled(builder.Configuration);
        ValidateAuthorizationBypassConfiguration(builder.Configuration, builder.Environment);

        builder.Services.AddAuthorization();
        builder.Services.AddAppAuthorization();
        var authenticationBuilder = builder.Services
            .AddAuthentication(
                options =>
                {
                    options.DefaultScheme = authorizationBypassEnabled
                        ? Constants.AuthorizationSchemas.DevelopmentBypass
                        : Constants.AuthorizationSchemas.Cookie;
                })
            .AddCookie(
                options =>
                {
                    #if !DEBUG
                    options.AccessDeniedPath = "/";
                    #endif
                    options.Cookie.SecurePolicy        = CookieSecurePolicy.Always;
                    options.Cookie.HttpOnly            = true;
                    options.Cookie.SameSite            = SameSiteMode.Lax;
                    options.ExpireTimeSpan             = TimeSpan.FromDays(7);
                    options.Events.OnSignedIn          = ValidationHelper.OnSignedIn;
                    options.Events.OnValidatePrincipal = ValidationHelper.OnValidatePrincipal;
                    options.LoginPath                  = "/users/login/steam";
                    options.LogoutPath                 = "/users/logout";
                    options.SlidingExpiration          = true;
                });
        if (authorizationBypassEnabled)
            authenticationBuilder.AddScheme<AuthenticationSchemeOptions, DevelopmentAuthorizationBypassAuthenticationHandler>(
                Constants.AuthorizationSchemas.DevelopmentBypass,
                _ => { });
        var discordAuthenticationEnabled = IsDiscordAuthenticationEnabled(builder.Configuration);
        if (discordAuthenticationEnabled)
            authenticationBuilder.AddDiscord(
                DiscordAuthenticationDefaults.AuthenticationScheme,
                options =>
                {
                    builder.Configuration.Bind("Discord:CorrelationCookie", options.CorrelationCookie);
                    options.ClientId = builder.Configuration[Constants.Configuration.Discord.OAuth.ClientId] ?? string.Empty;
                    options.ClientSecret = builder.Configuration[Constants.Configuration.Discord.OAuth.ClientSecret] ??
                                           string.Empty;
                    options.Events.OnRedirectToAuthorizationEndpoint = context =>
                    {
                        if (context.HttpContext.User.TryGetUserId(out var userId))
                        {
                            var tokens = context.Properties.GetTokens().ToList();
                            tokens.Add(
                                new AuthenticationToken
                                {
                                    Name  = "UTF-UserId",
                                    Value = userId.ToString(),
                                });
                            context.Properties.StoreTokens(tokens);
                            context.UpdateRedirectUrlStateQueryParameter();
                        }

                        context.Response.Redirect(context.RedirectUri);
                        return Task.CompletedTask;
                    };
                    options.Events.OnCreatingTicket = context =>
                    {
                        var userId = context.Properties.GetTokens().FirstOrDefault(q => q.Name == "UTF-UserId")?.Value;
                        if (userId is not null && context.Identity is not null)
                        {
                            context.Identity.AddClaim(new Claim(Constants.ClaimTypes.UserId, userId));
                        }

                        return Task.CompletedTask;
                    };
                });
        if (builder.Configuration[Constants.Configuration.Steam.Enabled]?.ToBoolean() ?? false)
            authenticationBuilder.AddSteam(
                SteamAuthenticationDefaults.AuthenticationScheme,
                options => { builder.Configuration.Bind("Steam:CorrelationCookie", options.CorrelationCookie); });
    }

    internal static bool IsDiscordAuthenticationEnabled(IConfiguration configuration)
        => (configuration[Constants.Configuration.Discord.Enabled]?.ToBoolean() ?? false)
           && !string.IsNullOrWhiteSpace(configuration[Constants.Configuration.Discord.OAuth.ClientId])
           && !string.IsNullOrWhiteSpace(configuration[Constants.Configuration.Discord.OAuth.ClientSecret]);

    internal static bool IsAuthorizationBypassEnabled(IConfiguration configuration)
    {
        return bool.TryParse(
                   configuration[Constants.Configuration.Testing.AuthorizationBypass.Enabled],
                   out var enabled)
               && enabled;
    }

    internal static Guid GetAuthorizationBypassUserId(IConfiguration configuration)
    {
        var configuredUserId = configuration[Constants.Configuration.Testing.AuthorizationBypass.UserId];
        if (string.IsNullOrWhiteSpace(configuredUserId))
            configuredUserId = Constants.AuthorizationBypass.DefaultUserId;
        if (!Guid.TryParse(configuredUserId, out var userId))
        {
            throw new InvalidOperationException(
                $"{Constants.Configuration.Testing.AuthorizationBypass.UserId} must be a valid GUID.");
        }

        return userId;
    }

    internal static void ValidateAuthorizationBypassConfiguration(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        if (!IsAuthorizationBypassEnabled(configuration))
            return;
#if DEBUG
        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Development authorization bypass can only be enabled in the Development environment.");
        }

        _ = GetAuthorizationBypassUserId(configuration);
#else
        throw new InvalidOperationException("Development authorization bypass can only be enabled in DEBUG builds.");
#endif
    }
}

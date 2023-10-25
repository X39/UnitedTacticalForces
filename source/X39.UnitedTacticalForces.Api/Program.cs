using System.Globalization;
using System.Security.Claims;
using System.Text.Json.Serialization;
using AspNet.Security.OAuth.Discord;
using AspNet.Security.OpenId.Steam;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Unchase.Swashbuckle.AspNetCore.Extensions.Extensions;
using Unchase.Swashbuckle.AspNetCore.Extensions.Options;
using X39.UnitedTacticalForces.Api;
using X39.UnitedTacticalForces.Api.Authorization.Abstraction;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Data.Hosting;
using X39.UnitedTacticalForces.Api.ExtensionMethods;
using X39.UnitedTacticalForces.Api.Helpers;
using X39.UnitedTacticalForces.Api.HostedServices;
using X39.UnitedTacticalForces.Api.Services;
using X39.UnitedTacticalForces.Contract.GameServer;
using X39.Util;
using X39.Util.DependencyInjection;

// See https://www.npgsql.org/efcore/release-notes/6.0.html#breaking-changes
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.secret.json", optional: true);
builder.Configuration.AddJsonFile("appsettings.user.json", optional: true);

// Add services to the container.

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(
    (options) =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo {Title = "UTF-API", Version = "v1"});
        options.UseAllOfToExtendReferenceSchemas();
        var xmlFilePath = Path.Combine(AppContext.BaseDirectory, "X39.UnitedTacticalForces.Api.xml");
        options.IncludeXmlComments(xmlFilePath);
        options.AddEnumsWithValuesFixFilters(
            (o) =>
            {
                o.ApplySchemaFilter    = true;
                o.ApplyParameterFilter = true;
                o.ApplyDocumentFilter  = true;
                o.IncludeDescriptions  = true;
                o.IncludeXEnumRemarks  = true;
                o.DescriptionSource    = DescriptionSources.DescriptionAttributesThenXmlComments;
                o.NewLine              = "\n";
                o.IncludeXmlCommentsFrom(xmlFilePath);
            });
    });
builder.Services.AddDbContextFactory<ApiDbContext>(
    options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("ApiDbContext")));
builder.Services.AddAttributedServicesFromAssemblyOf<Program>(builder.Configuration);
builder.Services.AddSingleton<DiscordBot>();
builder.Services.AddHostedService<DiscordBot>((serviceProvider) => serviceProvider.GetRequiredService<DiscordBot>());
builder.Services.AddSingleton(new BaseUrl(
    builder.Configuration[Constants.Configuration.General.ApiBaseUrl] ?? string.Empty,
    builder.Configuration[Constants.Configuration.General.ClientBaseUrl] ?? string.Empty));

builder.Services.AddAuthorization();
builder.Services.AddAppAuthorization();
var authenticationBuilder = builder.Services
    .AddAuthentication(options => { options.DefaultScheme = Constants.AuthorizationSchemas.Cookie; })
    .AddCookie(
        options =>
        {
#if !DEBUG
        options.AccessDeniedPath = "/";
#endif
            options.ExpireTimeSpan             = TimeSpan.FromDays(7);
            options.Events.OnSignedIn          = ValidationHelper.OnSignedIn;
            options.Events.OnValidatePrincipal = ValidationHelper.OnValidatePrincipal;
            options.LoginPath                  = "/users/login/steam";
            options.LogoutPath                 = "/users/logout";
            options.SlidingExpiration          = true;
            options.ExpireTimeSpan             = TimeSpan.FromDays(7);
        });
if (builder.Configuration[Constants.Configuration.Discord.Enabled]?.ToBoolean() ?? false)
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
                var userId = context.Properties.GetTokens().FirstOrDefault((q) => q.Name == "UTF-UserId")?.Value;
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
builder.Services.AddControllers()
    .AddJsonOptions(
        (jsonOptions) =>
        {
            jsonOptions.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.Preserve;
            jsonOptions.JsonSerializerOptions.MaxDepth         = 64;
        });
builder.Services.AddHttpClient();
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
var app = builder.Build();
{
    await using var scope = app.Services.CreateAsyncScope();
    await using var dbContext = scope.ServiceProvider.GetService<ApiDbContext>();
    await dbContext!.Database.MigrateAsync();
    await foreach (var gameServer in dbContext.GameServers)
    {
        gameServer.Status = ELifetimeStatus.Stopped;
    }

    await dbContext.SaveChangesAsync();
}
app.UsePathBase(app.Configuration[Constants.Configuration.General.BasePath]);
app.UseCors(
    cors => cors
        .SetIsOriginAllowed(_ => true)
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials());

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await app.RunAsync().ConfigureAwait(false);
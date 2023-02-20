using System.Reflection.Metadata;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using X39.UnitedTacticalForces.Api;
using X39.UnitedTacticalForces.Api.Data;
using X39.Util.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.secret.json", optional: true);
builder.Configuration.AddJsonFile("appsettings.user.json", optional: true);

// Add services to the container.

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<ApiDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("ApiDbContext")));
builder.Services.AddAttributedServicesFromAssemblyOf<Program>(builder.Configuration);
builder.Services.AddAuthorization();
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer((options) =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration[Constants.Configuration.Jwt.Issuer],
            ValidAudience = builder.Configuration[Constants.Configuration.Jwt.Audience],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(Constants.Configuration.Jwt.SecretKey)),
        };
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
    })
    .AddSteam(Constants.AuthorizationSchemas.Steam, (options) =>
    {
        // https://steamcommunity.com/dev/apikey
        options.ApplicationKey = builder.Configuration[Constants.Configuration.Steam.ApiKey];
    })
    .AddScheme<ApiScheme, ApiSchemeAuthenticationHandler>(Constants.AuthorizationSchemas.Api, "Api", (options) => { });
builder.Services.AddControllers();
builder.Services.AddHttpClient();
// ...
var app = builder.Build();
{
    await using var scope = app.Services.CreateAsyncScope();
    await using var dbContext = scope.ServiceProvider.GetService<ApiDbContext>();
    await dbContext!.Database.MigrateAsync();
}
app.UseCors(builder => builder
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

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

app.Run();

public class ApiSchemeAuthenticationHandler : AuthenticationHandler<ApiScheme>
{
    public ApiSchemeAuthenticationHandler(IOptionsMonitor<ApiScheme> options, ILoggerFactory logger, UrlEncoder encoder,
        ISystemClock clock) : base(options, logger, encoder, clock)
    {
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Context.User.Claims.FirstOrDefault((q) => q.Type == Constants.ClaimTypes.CookieId);
        return new HandleRequestResult()
        {
        };
    }
}
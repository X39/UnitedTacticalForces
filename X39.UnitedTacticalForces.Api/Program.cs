using System.Diagnostics;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using X39.UnitedTacticalForces.Api;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Helpers;
using X39.Util.DependencyInjection;

// See https://www.npgsql.org/efcore/release-notes/6.0.html#breaking-changes
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

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
builder.Services.AddAuthentication(options => { options.DefaultScheme = Constants.AuthorizationSchemas.Cookie; })
    .AddCookie(options =>
    {
#if !DEBUG
        options.AccessDeniedPath = "/";
#endif
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.Events.OnSignedIn = ValidationHelper.OnSignedIn;
        options.Events.OnValidatePrincipal = ValidationHelper.OnValidatePrincipal;
        options.LoginPath = "/Users/login/steam";
        options.LogoutPath = "/Users/logout";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
    })
    .AddSteam();
builder.Services.AddControllers()
    .AddJsonOptions((jsonOptions) => jsonOptions.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);
builder.Services.AddHttpClient();
// ...
var app = builder.Build();
{
    await using var scope = app.Services.CreateAsyncScope();
    await using var dbContext = scope.ServiceProvider.GetService<ApiDbContext>();
    await dbContext!.Database.MigrateAsync();
}
app.UseCors(cors => cors
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

app.Run();

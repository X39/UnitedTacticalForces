using System.Reflection.Metadata;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api;
using X39.UnitedTacticalForces.Api.Data;
using X39.Util.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.secret.json");
builder.Configuration.AddJsonFile("appsettings.user.json");

// Add services to the container.

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<ApiDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("ApiDbContext")));
builder.Services.AddAttributedServicesFromAssemblyOf<Program>(builder.Configuration);
builder.Services.AddAuthorization();
builder.Services.AddAuthentication()
    .AddSteam(Constants.AuthorizationSchemas.Steam, (options) =>
    {
        // https://steamcommunity.com/dev/apikey
        options.ApplicationKey = builder.Configuration["Steam:ApiKey"];
    });
builder.Services.AddControllers();
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

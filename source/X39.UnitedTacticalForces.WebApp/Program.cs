using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using X39.UnitedTacticalForces.WebApp;
using MudBlazor.Services;
using X39.UnitedTacticalForces.WebApp.Services;
using X39.Util.DependencyInjection;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddScoped(_ => new HttpClient {BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)});
builder.Services.AddMudServices();
builder.Services.AddLocalization();
builder.Services.AddAuthorizationCore();
builder.Services.AddSingleton(new BaseUrl(
    builder.Configuration[Constants.Configuration.ApiBaseUrl]
    ?? string.Empty,
    builder.HostEnvironment.BaseAddress));

builder.Services.AddAttributedServicesFromAssemblyOf<Program>(builder.Configuration);
// https://learn.microsoft.com/de-de/aspnet/core/blazor/security/webassembly/standalone-with-authentication-library?view=aspnetcore-7.0&tabs=visual-studio
var host = builder.Build();
// ToDo: Build into X39.Util.DependencyInjection
await host.Services.GetRequiredService<MeService>().InitializeAsync();
await host.RunAsync();
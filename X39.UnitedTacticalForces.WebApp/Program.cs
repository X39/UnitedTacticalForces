using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using X39.UnitedTacticalForces.WebApp;
using MudBlazor.Services;
using X39.Util.DependencyInjection;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddScoped(sp => new HttpClient {BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)});
builder.Services.AddMudServices();
builder.Services.AddLocalization();
builder.Services.AddOidcAuthentication((options) =>
{
    options.ProviderOptions.MetadataUrl = "https://steamcommunity.com/openid/";
    options.ProviderOptions.RedirectUri = "/authentication";
});
builder.Services.AddSingleton(new ApiBaseUrl(
    builder.Configuration[Constants.Configuration.ApiBaseUrl]
    ?? string.Empty));

builder.Services.AddAttributedServicesFromAssemblyOf<Program>(builder.Configuration);
// https://learn.microsoft.com/de-de/aspnet/core/blazor/security/webassembly/standalone-with-authentication-library?view=aspnetcore-7.0&tabs=visual-studio
await builder.Build().RunAsync();
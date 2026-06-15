using System.Globalization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;
using X39.UnitedTacticalForces.WebApp;
using X39.UnitedTacticalForces.WebApp.Services;
using X39.Util.DependencyInjection;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddTransient<UserAutoCompleteService>();
builder.Services.AddTransient<IncludeCookiesHttpMessageHandler>();
builder.Services.AddHttpClient("API", conf => conf.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
    .AddHttpMessageHandler<IncludeCookiesHttpMessageHandler>();
builder.Services.AddFluentUIComponents(options =>
{
    options.ValidateClassNames = false;
});

builder.Services.AddLocalization();
builder.Services.AddAuthorizationCore();
builder.Services.AddSingleton(new BaseUrl(
    builder.Configuration[Constants.Configuration.ApiBaseUrl]
    ?? string.Empty,
    builder.HostEnvironment.BaseAddress));

builder.Services.AddAttributedServicesFromAssemblyOf<Program>(builder.Configuration);
builder.Services.AddAttributedServicesFromAssemblyOf<X39.Util.Blazor.WebAssembly.Assembly>(builder.Configuration);
// https://learn.microsoft.com/de-de/aspnet/core/blazor/security/webassembly/standalone-with-authentication-library?view=aspnetcore-7.0&tabs=visual-studio
var host = builder.Build();
var culture = ResolveSupportedCulture(await host.Services.GetRequiredService<IJSRuntime>()
    .InvokeAsync<string?>("utfCulture.get"));
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;
// ToDo: Build into X39.Util.DependencyInjection
await host.Services.GetRequiredService<AuthenticationProviderAvailability>().InitializeAsync();
await host.Services.GetRequiredService<MeService>().InitializeAsync();
await host.Services.GetRequiredService<UiConfiguration>().InitializeAsync();
await host.RunAsync();

static CultureInfo ResolveSupportedCulture(string? cultureName)
{
    if (cultureName is null)
        return CultureInfo.GetCultureInfo("en");

    try
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        return culture.TwoLetterISOLanguageName.Equals("de", StringComparison.OrdinalIgnoreCase)
            ? CultureInfo.GetCultureInfo("de")
            : CultureInfo.GetCultureInfo("en");
    }
    catch (CultureNotFoundException)
    {
        return CultureInfo.GetCultureInfo("en");
    }
}

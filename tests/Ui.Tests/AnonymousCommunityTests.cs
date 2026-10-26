using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.Playwright.Assertions;

namespace Ui.Tests;

/// <summary>
/// Exercises the rendered Blazor community shell with isolated anonymous API responses.
/// </summary>
public sealed class AnonymousCommunityTests : IClassFixture<WebAppHostFixture>
{
    /// <summary>Provides the running Blazor host and real Chromium browser for each scenario.</summary>
    private readonly WebAppHostFixture _host;

    /// <summary>Records bounded browser diagnostics from the isolated fixture session.</summary>
    private readonly ITestOutputHelper _output;

    /// <summary>Connects the scenarios to the shared host without sharing browser session state.</summary>
    /// <param name="host">The fixture that starts Chromium and supplies the Blazor origin.</param>
    /// <param name="output">The test output receiving startup errors from mocked browser sessions.</param>
    public AnonymousCommunityTests(WebAppHostFixture host, ITestOutputHelper output)
    {
        _host = host;
        _output = output;
    }

    /// <summary>Verifies startup data binding and anonymous navigation through the rendered application.</summary>
    /// <returns>A task that completes after the home and operations pages have rendered.</returns>
    [Fact]
    public async Task Home_RendersUpcomingOperationAndNavigatesWithoutManagementAccess()
    {
        await using var context = await CreateContextAsync();
        var unexpectedRequests = new ConcurrentQueue<string>();
        await ConfigureNetworkAsync(context, unexpectedRequests);
        var page = await CreatePageAsync(context);
        await page.GotoAsync(_host.BaseUrl);

        await Expect(page.Locator("h1")).ToHaveTextAsync("CI community operation");
        await Expect(page.Locator(".utf-landing-hero__summary")).ToContainTextAsync("Deterministic briefing");
        await Expect(page.Locator(".utf-sign-in-button")).ToHaveTextAsync("Login");
        await Expect(page.Locator(".utf-command-entry")).ToHaveCountAsync(0);

        await page.Locator(".utf-community-links").GetByRole(AriaRole.Link,
            new() { Name = "Operations", Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(new Uri(new Uri(_host.BaseUrl), "events/upcoming").ToString());
        await Expect(page.Locator("h1")).ToHaveTextAsync("Upcoming Operations");
        await Expect(page.Locator(".utf-community-links a.active")).ToHaveTextAsync("Operations");
        Assert.Empty(unexpectedRequests);
    }

    /// <summary>Verifies that anonymous login opens the provider chooser without leaving the application.</summary>
    /// <returns>A task that completes after the chooser is opened, checked and dismissed.</returns>
    [Fact]
    public async Task AnonymousLogin_OffersOnlyConfiguredProviderAndCanBeDismissed()
    {
        await using var context = await CreateContextAsync();
        var unexpectedRequests = new ConcurrentQueue<string>();
        await ConfigureNetworkAsync(context, unexpectedRequests);
        var page = await CreatePageAsync(context);
        await page.GotoAsync(_host.BaseUrl);

        await page.Locator(".utf-sign-in-button").ClickAsync();
        var dialog = page.GetByRole(AriaRole.Dialog);
        await Expect(dialog).ToBeVisibleAsync();
        await Expect(dialog.GetByRole(AriaRole.Heading)).ToHaveTextAsync("Sign in");
        await Expect(dialog.Locator("a.utf-sign-in-option")).ToHaveCountAsync(1);
        await Expect(dialog.Locator("a.utf-sign-in-option")).ToHaveAttributeAsync(
            "href", new System.Text.RegularExpressions.Regex("/api/users/login/steam\\?returnUrl="));
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();
        await Expect(dialog).ToHaveCountAsync(0);
        await Expect(page).ToHaveURLAsync(_host.BaseUrl);
        Assert.Empty(unexpectedRequests);
    }

    /// <summary>Creates a page that reports startup failures without extending assertion timeouts.</summary>
    /// <param name="context">The isolated context whose API responses are supplied by the fixture.</param>
    /// <returns>A page with bounded console and uncaught-error diagnostics attached before navigation.</returns>
    private async Task<IPage> CreatePageAsync(IBrowserContext context)
    {
        var page = await context.NewPageAsync();
        var diagnosticCount = 0;
        page.PageError += (_, error) => Report("Browser page error: " + error);
        page.Console += (_, message) =>
        {
            if (message.Type == "error")
                Report("Browser console error: " + message.Text);
        };
        return page;

        void Report(string message)
        {
            if (Interlocked.Increment(ref diagnosticCount) <= 20)
                _output.WriteLine(message[..Math.Min(message.Length, 4000)]);
        }
    }

    /// <summary>Creates an English desktop session with service workers disabled so every API request is intercepted.</summary>
    /// <returns>An isolated browser context whose resources are disposed by its scenario.</returns>
    private async Task<IBrowserContext> CreateContextAsync()
    {
        var context = await _host.Browser.NewContextAsync(new()
        {
            Locale = "en-US",
            ViewportSize = new() { Width = 1440, Height = 1000 },
            ServiceWorkers = ServiceWorkerPolicy.Block,
        });
        context.SetDefaultTimeout(60000);
        await context.AddInitScriptAsync("window.localStorage.setItem('UtfCulture', 'en');");
        return context;
    }

    /// <summary>Serves deterministic application configuration and API data while blocking external network access.</summary>
    /// <param name="context">The browser session whose requests are intercepted before its first navigation.</param>
    /// <param name="unexpectedRequests">Collects unrecognized API paths so fallback error handling cannot hide gaps.</param>
    /// <returns>A task that completes when interception is installed.</returns>
    private async Task ConfigureNetworkAsync(IBrowserContext context, ConcurrentQueue<string> unexpectedRequests)
    {
        var origin = new Uri(_host.BaseUrl).GetLeftPart(UriPartial.Authority);
        await context.RouteAsync("**/*", async route =>
        {
            var uri = new Uri(route.Request.Url);
            if (uri.GetLeftPart(UriPartial.Authority) != origin)
            {
                await route.AbortAsync();
                return;
            }

            var path = uri.AbsolutePath;
            if (path.StartsWith("/appsettings", StringComparison.Ordinal) && path.EndsWith(".json", StringComparison.Ordinal))
            {
                await route.FulfillAsync(new()
                {
                    ContentType = "application/json",
                    Body = JsonSerializer.Serialize(new
                    {
                        Api = new { BaseUrl = origin + "/api/" },
                        Urls = new { TeamSpeak3 = "", Discord = "" },
                        Ui = new { ReactVariantEnabled = false },
                    }),
                });
                return;
            }

            if (!path.StartsWith("/api/", StringComparison.Ordinal))
            {
                await route.ContinueAsync();
                return;
            }

            if (path == "/api/users/me")
            {
                await route.FulfillAsync(new() { Status = 204 });
                return;
            }

            var body = path switch
            {
                "/api/users/authentication-providers" => "{\"steam\":true,\"discord\":false}",
                "/api/events/upcoming" => """
                    [{"primaryKey":"148d5202-95a7-47bc-b448-eb23ae947d81",
                      "title":"CI community operation","description":"Deterministic briefing",
                      "scheduledFor":"2099-06-01T18:00:00Z","acceptedCount":2,"minimumAccepted":4,
                      "terrainTitle":"CI terrain","modPackDefinitionTitle":"CI mod pack",
                      "hostedBy":{"nickname":"CI host"}}]
                    """,
                "/api/field-reports" or "/api/site-image-settings" => "[]",
                _ => null,
            };
            if (body is null)
                unexpectedRequests.Enqueue(route.Request.Url);
            await route.FulfillAsync(new()
            {
                Status = body is null ? 500 : 200,
                ContentType = "application/json",
                Body = body ?? "{}",
            });
        });
    }
}

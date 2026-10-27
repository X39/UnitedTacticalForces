using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using X39.UnitedTacticalForces.Api.Middleware;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>Verifies that the paused HTTP boundary blocks mutations and worker command delivery.</summary>
public sealed class HostingControlPlanePauseMiddlewareTests
{
    /// <summary>Protected requests receive a stable, noncacheable response without entering controllers.</summary>
    /// <param name="method">The incoming HTTP method.</param>
    /// <param name="path">The protected request path.</param>
    /// <returns>A task completing after the response body has been verified.</returns>
    [Theory]
    [InlineData("POST", "/game-servers")]
    [InlineData("PUT", "/game-servers/server/configuration")]
    [InlineData("DELETE", "/game-servers/server")]
    [InlineData("PATCH", "/jobs/job")]
    [InlineData("POST", "/jobs/job/retry")]
    [InlineData("GET", "/lifecycle-workers/commands")]
    [InlineData("HEAD", "/lifecycle-workers")]
    [InlineData("OPTIONS", "/lifecycle-workers/registration")]
    [InlineData("POST", "/lifecycle-workers/register")]
    public async Task Paused_ProtectedRequestNeverReachesController(string method, string path)
    {
        using var services = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
        var context = CreateContext(method, path);
        context.RequestServices = services;
        var invoked = false;
        var middleware = new HostingControlPlanePauseMiddleware(_ =>
        {
            invoked = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, CreateState(paused: true));

        Assert.False(invoked);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
        context.Response.Body.Position = 0;
        using var response = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("hosting_control_plane_paused", response.RootElement.GetProperty("code").GetString());
    }

    /// <summary>Read-only management requests and unrelated routes remain available while paused.</summary>
    /// <param name="method">The incoming HTTP method.</param>
    /// <param name="path">The read-only or unrelated request path.</param>
    /// <returns>A task completing after downstream invocation is verified.</returns>
    [Theory]
    [InlineData("GET", "/game-servers")]
    [InlineData("HEAD", "/game-servers/server")]
    [InlineData("OPTIONS", "/game-servers")]
    [InlineData("GET", "/jobs/job")]
    [InlineData("POST", "/events")]
    [InlineData("POST", "/game-servers-other")]
    [InlineData("GET", "/health")]
    public Task Paused_ManagementReadsAndUnrelatedRoutesRemainAvailable(string method, string path)
        => AssertDownstreamAsync(method, path, paused: true);

    /// <summary>An explicit enable setting preserves controller access for operational requests.</summary>
    /// <param name="method">The incoming HTTP method.</param>
    /// <param name="path">The operational request path.</param>
    /// <returns>A task completing after downstream invocation is verified.</returns>
    [Theory]
    [InlineData("POST", "/game-servers")]
    [InlineData("POST", "/jobs/job/retry")]
    [InlineData("GET", "/lifecycle-workers/commands")]
    public Task Enabled_OperationalRequestsReachController(string method, string path)
        => AssertDownstreamAsync(method, path, paused: false);

    /// <summary>Checks that the middleware preserves the downstream response.</summary>
    /// <param name="method">The incoming HTTP method.</param>
    /// <param name="path">The request path.</param>
    /// <param name="paused">Whether the hosting control plane is paused.</param>
    /// <returns>A task completing after the delegate has executed.</returns>
    private static async Task AssertDownstreamAsync(string method, string path, bool paused)
    {
        var context = CreateContext(method, path);
        var middleware = new HostingControlPlanePauseMiddleware(downstream =>
        {
            downstream.Response.StatusCode = StatusCodes.Status202Accepted;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, CreateState(paused));

        Assert.Equal(StatusCodes.Status202Accepted, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
    }

    /// <summary>Creates a request with an inspectable in-memory response stream.</summary>
    /// <param name="method">The incoming HTTP method.</param>
    /// <param name="path">The request path.</param>
    /// <returns>The isolated HTTP context.</returns>
    private static DefaultHttpContext CreateContext(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    /// <summary>Creates an explicit state without consulting environment configuration.</summary>
    /// <param name="paused">Whether hosting operations must be paused.</param>
    /// <returns>The state supplied to the middleware.</returns>
    private static HostingControlPlaneState CreateState(bool paused)
        => new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [HostingControlPlaneState.ConfigurationKey] = paused.ToString(),
        }).Build());
}

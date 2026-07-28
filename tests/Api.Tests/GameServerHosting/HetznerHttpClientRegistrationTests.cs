using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using Xunit;

namespace Api.Tests.GameServerHosting;

public sealed class HetznerHttpClientRegistrationTests
{
    public static TheoryData<HttpMethod> OtherUnsafeMethods => new()
    {
        HttpMethod.Patch,
        HttpMethod.Put,
        HttpMethod.Delete,
        new HttpMethod("CONNECT"),
    };

    [Fact]
    public async Task ProductionDefaults_DoNotRetryTransientPost()
    {
        var handler = new SequenceHandler(HttpStatusCode.ServiceUnavailable);
        using var services = CreateProductionServices(handler);
        var client = services.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(HetznerCloudClient));

        using var response = await client.PostAsync("https://api.hetzner.cloud/v1/servers", content: null);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(1, handler.InvocationCount);
    }

    [Theory]
    [MemberData(nameof(OtherUnsafeMethods))]
    public async Task ProductionDefaults_DoNotRetryOtherUnsafeMethods(HttpMethod method)
    {
        var handler = new SequenceHandler(HttpStatusCode.ServiceUnavailable);
        using var services = CreateProductionServices(handler);
        var client = services.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(HetznerCloudClient));
        using var request = new HttpRequestMessage(method, "https://api.hetzner.cloud/v1/test");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(1, handler.InvocationCount);
    }

    [Fact]
    public async Task ProductionDefaults_RetryTransientGet()
    {
        var handler = new SequenceHandler(HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK);
        using var services = CreateProductionServices(handler);
        var client = services.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(HetznerCloudClient));

        using var response = await client.GetAsync("https://api.hetzner.cloud/v1/servers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, handler.InvocationCount);
    }

    private static ServiceProvider CreateProductionServices(HttpMessageHandler handler)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddServiceDefaults();
        builder.Services
            .AddHttpClient(nameof(HetznerCloudClient))
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        return builder.Services.BuildServiceProvider();
    }

    private sealed class SequenceHandler(params HttpStatusCode[] statusCodes) : HttpMessageHandler
    {
        private int _invocationCount;

        public int InvocationCount => _invocationCount;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var invocation = Interlocked.Increment(ref _invocationCount);
            var statusCode = statusCodes[Math.Min(invocation - 1, statusCodes.Length - 1)];
            var response = new HttpResponseMessage(statusCode);
            if (statusCode is HttpStatusCode.ServiceUnavailable)
                response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
            return Task.FromResult(response);
        }
    }
}

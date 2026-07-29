using System.Net;
using System.Text;
using System.Text.Json;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using Xunit;

namespace Api.Tests.GameServerHosting;

public sealed class HetznerCloudClientContractTests
{
    [Theory]
    [InlineData("server_types")]
    [InlineData("servers")]
    [InlineData("primary_ips")]
    [InlineData("firewalls")]
    public async Task PaginatedDiscovery_RejectsSuccessfulEnvelopeWithoutRequiredListRoot(string resource)
    {
        var handler = new ContractHttpMessageHandler().Respond(
            HttpStatusCode.OK,
            """{"meta":{"pagination":{"next_page":null}}}""");

        var result = await DiscoverAsync(CreateClient(handler), resource);

        Assert.Equal(ERuntimeProviderOutcome.Unknown, result.Outcome);
        Assert.Contains("unreadable", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("""{"servers":null,"meta":{"pagination":{"next_page":null}}}""")]
    [InlineData("""{"servers":[],"meta":null}""")]
    [InlineData("""{"servers":[],"meta":{"pagination":null}}""")]
    [InlineData("""{"servers":[],"meta":{"pagination":{}}}""")]
    public async Task PaginatedDiscovery_RejectsNullOrIncompletePaginationShape(string responseBody)
    {
        var handler = new ContractHttpMessageHandler().Respond(HttpStatusCode.OK, responseBody);

        var result = await CreateClient(handler).GetServersAsync(EmptyLabels(), CancellationToken.None);

        Assert.Equal(ERuntimeProviderOutcome.Unknown, result.Outcome);
        Assert.Contains("unreadable", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PaginatedDiscovery_AcceptsInt64NextPage()
    {
        const long nextPage = (long)int.MaxValue + 1;
        var handler = new ContractHttpMessageHandler()
            .Respond(HttpStatusCode.OK, ServerListResponse(nextPage))
            .Respond(HttpStatusCode.OK, ServerListResponse(null));

        var result = await CreateClient(handler).GetServersAsync(EmptyLabels(), CancellationToken.None);

        Assert.Equal(ERuntimeProviderOutcome.Succeeded, result.Outcome);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains($"page={nextPage}", handler.Requests[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Firewalls_MapNestedLabelSelectorResourcesAndKeepUnresolvedSelectorsAttached()
    {
        var responseBody = JsonSerializer.Serialize(new
        {
            firewalls = new object[]
            {
                new
                {
                    id = 19,
                    name = "resolved-selector",
                    labels = new Dictionary<string, string>(),
                    applied_to = new object[]
                    {
                        new
                        {
                            type = "label_selector",
                            label_selector = new { selector = "role=game" },
                            applied_to_resources = new object[]
                            {
                                new { type = "server", server = new { id = 42L } },
                                new
                                {
                                    type = "label_selector",
                                    label_selector = new { selector = "pool=secondary" },
                                    applied_to_resources = new object[]
                                    {
                                        new { type = "server", server = new { id = 43L } },
                                    },
                                },
                                new { type = "server", server = new { id = 42L } },
                            },
                        },
                    },
                    rules = Array.Empty<object>(),
                },
                new
                {
                    id = 20,
                    name = "unresolved-selector",
                    labels = new Dictionary<string, string>(),
                    applied_to = new object[]
                    {
                        new
                        {
                            type = "label_selector",
                            label_selector = new { selector = "role=missing" },
                            applied_to_resources = Array.Empty<object>(),
                        },
                    },
                    rules = Array.Empty<object>(),
                },
                new
                {
                    id = 21,
                    name = "unreadable-attachments",
                    labels = new Dictionary<string, string>(),
                    applied_to = (object?)null,
                    rules = Array.Empty<object>(),
                },
            },
            meta = Pagination(null),
        });
        var handler = new ContractHttpMessageHandler().Respond(HttpStatusCode.OK, responseBody);

        var result = await CreateClient(handler).GetFirewallsAsync(EmptyLabels(), CancellationToken.None);

        Assert.Equal(ERuntimeProviderOutcome.Succeeded, result.Outcome);
        var resolved = Assert.Single(result.Value!, q => q.Id == 19);
        Assert.Equal(5, resolved.AppliedToCount);
        Assert.Equal([42L, 43L], resolved.AppliedServerIds);
        var unresolved = Assert.Single(result.Value!, q => q.Id == 20);
        Assert.Equal(1, unresolved.AppliedToCount);
        Assert.Empty(unresolved.AppliedServerIds);
        var unreadable = Assert.Single(result.Value!, q => q.Id == 21);
        Assert.Equal(1, unreadable.AppliedToCount);
        Assert.Empty(unreadable.AppliedServerIds);
    }

    [Theory]
    [InlineData(412, "resource_unavailable", ERuntimeProviderOutcome.TransientFailure)]
    [InlineData(423, "locked", ERuntimeProviderOutcome.TransientFailure)]
    [InlineData(423, "protected", ERuntimeProviderOutcome.PermanentFailure)]
    [InlineData(409, "uniqueness_error", ERuntimeProviderOutcome.PermanentFailure)]
    [InlineData(409, "conflict", ERuntimeProviderOutcome.TransientFailure)]
    public async Task Create_ClassifiesProviderErrorsByStatusAndCode(
        int statusCode,
        string providerCode,
        ERuntimeProviderOutcome expectedOutcome)
    {
        var responseBody = JsonSerializer.Serialize(new
        {
            error = new { code = providerCode, message = "Provider failure" },
        });
        var handler = new ContractHttpMessageHandler().Respond((HttpStatusCode)statusCode, responseBody);

        var result = await CreateClient(handler).CreateFirewallAsync(
            new HetznerFirewallCreateRequest("test-firewall", [], new Dictionary<string, string>()),
            CancellationToken.None);

        Assert.Equal(expectedOutcome, result.Outcome);
        Assert.Contains(providerCode, result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Create_ClassifiesLockedStatusAsTransientWhenErrorBodyIsUnreadable()
    {
        var handler = new ContractHttpMessageHandler().Respond((HttpStatusCode)423, "not-json");

        var result = await CreateClient(handler).CreateFirewallAsync(
            new HetznerFirewallCreateRequest("test-firewall", [], new Dictionary<string, string>()),
            CancellationToken.None);

        Assert.Equal(ERuntimeProviderOutcome.TransientFailure, result.Outcome);
    }

    [Fact]
    public async Task GetPricingCurrency_NormalizesAuthoritativeCurrency()
    {
        var handler = new ContractHttpMessageHandler().Respond(
            HttpStatusCode.OK,
            """{"pricing":{"currency":" eur "}}""");

        var result = await CreateClient(handler).GetPricingCurrencyAsync(CancellationToken.None);

        Assert.Equal(ERuntimeProviderOutcome.Succeeded, result.Outcome);
        Assert.Equal("EUR", result.Value);
        Assert.Equal("/v1/pricing", Assert.Single(handler.Requests));
    }

    [Fact]
    public async Task ServerTypes_RejectLocationWithoutRequiredDeprecationState()
    {
        var responseBody = JsonSerializer.Serialize(new
        {
            server_types = new[]
            {
                new
                {
                    name = "ccx13",
                    locations = new[]
                    {
                        new { id = 1L, name = "fsn1", available = true, recommended = true },
                    },
                },
            },
            meta = Pagination(null),
        });
        var handler = new ContractHttpMessageHandler().Respond(HttpStatusCode.OK, responseBody);

        var result = await CreateClient(handler).GetServerTypesAsync(CancellationToken.None);

        Assert.Equal(ERuntimeProviderOutcome.Unknown, result.Outcome);
        Assert.Contains("unreadable", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"pricing":null}""")]
    [InlineData("""{"pricing":{}}""")]
    [InlineData("""{"pricing":{"currency":null}}""")]
    [InlineData("""{"pricing":{"currency":"EU"}}""")]
    [InlineData("""{"pricing":{"currency":"EU1"}}""")]
    [InlineData("""{"pricing":{"currency":"€UR"}}""")]
    public async Task GetPricingCurrency_RejectsMissingOrInvalidShape(string responseBody)
    {
        var handler = new ContractHttpMessageHandler().Respond(HttpStatusCode.OK, responseBody);

        var result = await CreateClient(handler).GetPricingCurrencyAsync(CancellationToken.None);

        Assert.Equal(ERuntimeProviderOutcome.Unknown, result.Outcome);
        Assert.Contains("unreadable", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<(ERuntimeProviderOutcome Outcome, string? Error)> DiscoverAsync(
        HetznerCloudClient client,
        string resource)
    {
        return resource switch
        {
            "server_types" => Result(await client.GetServerTypesAsync(CancellationToken.None)),
            "servers" => Result(await client.GetServersAsync(EmptyLabels(), CancellationToken.None)),
            "primary_ips" => Result(await client.GetPrimaryIpsAsync(EmptyLabels(), CancellationToken.None)),
            "firewalls" => Result(await client.GetFirewallsAsync(EmptyLabels(), CancellationToken.None)),
            _ => throw new ArgumentOutOfRangeException(nameof(resource), resource, null),
        };
    }

    private static (ERuntimeProviderOutcome Outcome, string? Error) Result<T>(HetznerClientResult<T> result)
        => (result.Outcome, result.Error);

    private static IReadOnlyDictionary<string, string> EmptyLabels()
        => new Dictionary<string, string>();

    private static HetznerCloudClient CreateClient(ContractHttpMessageHandler handler)
    {
        var options = new HetznerRuntimeOptions
        {
            Token = "test-token",
            BaseUrl = "https://unit.test/v1/",
            AllowCustomEndpointForTesting = true,
        };
        return new HetznerCloudClient(
            new ContractHttpClientFactory(handler),
            new ContractOptionsProvider(options));
    }

    private static string ServerListResponse(long? nextPage)
        => JsonSerializer.Serialize(new
        {
            servers = Array.Empty<object>(),
            meta = Pagination(nextPage),
        });

    private static object Pagination(long? nextPage)
        => new { pagination = new { next_page = nextPage } };

    private sealed class ContractOptionsProvider(HetznerRuntimeOptions value) : IHetznerRuntimeOptionsProvider
    {
        public HetznerRuntimeOptions Value { get; } = value;
    }

    private sealed class ContractHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class ContractHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();

        public List<string> Requests { get; } = [];

        public ContractHttpMessageHandler Respond(HttpStatusCode statusCode, string json)
        {
            _responses.Enqueue(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
            return this;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri?.PathAndQuery ?? string.Empty);
            return Task.FromResult(_responses.Dequeue());
        }
    }
}

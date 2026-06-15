using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using Xunit;

namespace Api.Tests.GameServerHosting;

public sealed class HetznerRuntimeProviderTests
{
    [Fact]
    public async Task Provision_CreatesAndReturnsCompleteAttributedResourceGraph()
    {
        var request = CreateProvisionRequest();
        var handler = new SequencedHttpMessageHandler()
            .Respond(HttpStatusCode.OK, ServerListJson())
            .Respond(HttpStatusCode.OK, PrimaryIpListJson())
            .Respond(HttpStatusCode.OK, FirewallListJson())
            .Respond(HttpStatusCode.Created, FirewallResponseJson(19, request))
            .Respond(HttpStatusCode.Created, PrimaryIpResponseJson(91, request))
            .Respond(HttpStatusCode.Created, ServerResponseJson(42, request))
            .Respond(HttpStatusCode.OK, PrimaryIpResponseJson(91, request, assigneeId: 42))
            .Respond(HttpStatusCode.OK, FirewallResponseJson(19, request, 42));
        var provider = CreateProvider(handler);

        var result = await provider.ProvisionAsync(request);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("provisioned", result.Stage);
        Assert.Equal("server:42", result.Resource?.ResourceIdentifier);
        Assert.Equal("203.0.113.91", result.Resource?.PublicAddress);
        Assert.Equal(
            ["server:42", "primary-ip:91", "firewall:19"],
            result.ProvisionedResources.Select(q => q.ResourceIdentifier).ToArray());
        Assert.True(result.ProvisionedResources.Single(q => q.ResourceType == "primary-ip")
            .IsPotentiallyCostGenerating);
        Assert.False(result.ProvisionedResources.Single(q => q.ResourceType == "firewall")
            .IsPotentiallyCostGenerating);

        Assert.Equal(
            [
                "/v1/servers", "/v1/primary_ips", "/v1/firewalls", "/v1/firewalls", "/v1/primary_ips",
                "/v1/servers", "/v1/primary_ips/91", "/v1/firewalls/19",
            ],
            handler.Requests.Select(q => q.Path).ToArray());
        Assert.All(handler.Requests, q =>
        {
            Assert.Equal("Bearer", q.AuthorizationScheme);
            Assert.Equal(TestToken, q.AuthorizationParameter);
        });

        using var firewallBody = JsonDocument.Parse(handler.Requests[3].Body);
        var firewallRoot = firewallBody.RootElement;
        var rules = firewallRoot.GetProperty("rules").EnumerateArray().ToArray();
        Assert.Equal(2, rules.Length);
        Assert.Equal("in", rules[0].GetProperty("direction").GetString());
        Assert.Equal("tcp", rules[0].GetProperty("protocol").GetString());
        Assert.Equal("30100", rules[0].GetProperty("port").GetString());
        Assert.Equal("Primary test traffic", rules[0].GetProperty("description").GetString());
        Assert.Equal("udp", rules[1].GetProperty("protocol").GetString());
        Assert.Equal("30110-30112", rules[1].GetProperty("port").GetString());
        Assert.Equal("Secondary test traffic", rules[1].GetProperty("description").GetString());
        Assert.All(rules, rule =>
        {
            Assert.Equal("0.0.0.0/0", Assert.Single(rule.GetProperty("source_ips").EnumerateArray()).GetString());
            Assert.False(rule.TryGetProperty("destination_ips", out _));
        });

        using var ipBody = JsonDocument.Parse(handler.Requests[4].Body);
        var ipRoot = ipBody.RootElement;
        Assert.Equal("ipv4", ipRoot.GetProperty("type").GetString());
        Assert.Equal("fsn1", ipRoot.GetProperty("location").GetString());
        Assert.True(ipRoot.GetProperty("auto_delete").GetBoolean());
        Assert.False(ipRoot.TryGetProperty("datacenter", out _));
        Assert.False(ipRoot.TryGetProperty("assignee_type", out _));

        using var serverBody = JsonDocument.Parse(handler.Requests[5].Body);
        var serverRoot = serverBody.RootElement;
        Assert.Equal("ccx13", serverRoot.GetProperty("server_type").GetString());
        Assert.Equal(request.CloudInit, serverRoot.GetProperty("user_data").GetString());
        Assert.Equal(91, serverRoot.GetProperty("public_net").GetProperty("ipv4").GetInt64());
        Assert.True(serverRoot.GetProperty("public_net").GetProperty("enable_ipv4").GetBoolean());
        Assert.False(serverRoot.GetProperty("public_net").GetProperty("enable_ipv6").GetBoolean());
        Assert.Equal(
            19,
            Assert.Single(serverRoot.GetProperty("firewalls").EnumerateArray()).GetProperty("firewall").GetInt64());
        Assert.False(serverRoot.TryGetProperty("datacenter", out _));
        Assert.Equal(
            HetznerRuntimeProvider.ServerResourceType,
            serverRoot.GetProperty("labels").GetProperty(HetznerRuntimeProvider.ResourceKindLabel).GetString());
    }

    [Fact]
    public async Task Provision_AdoptsCompleteExistingGraphWithoutPosting()
    {
        var request = CreateProvisionRequest();
        var handler = new SequencedHttpMessageHandler()
            .Respond(HttpStatusCode.OK, ServerListJson((42, request)))
            .Respond(HttpStatusCode.OK, PrimaryIpListJson((91, request, 42L)))
            .Respond(HttpStatusCode.OK, FirewallListJson((19, request)))
            .Respond(HttpStatusCode.OK, PrimaryIpResponseJson(91, request, assigneeId: 42))
            .Respond(HttpStatusCode.OK, FirewallResponseJson(19, request, 42));

        var result = await CreateProvider(handler).ProvisionAsync(request);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("recovered-existing", result.Stage);
        Assert.Equal(3, result.ProvisionedResources.Count);
        Assert.DoesNotContain(handler.Requests, q => q.Method == HttpMethod.Post);
    }

    [Theory]
    [InlineData()]
    [InlineData(43L)]
    [InlineData(42L, 43L)]
    public async Task Provision_RefusesRecoveredGraphUnlessFirewallIsAttachedOnlyToAttributedServer(
        params long[] appliedServerIds)
    {
        var request = CreateProvisionRequest();
        var handler = new SequencedHttpMessageHandler()
            .Respond(HttpStatusCode.OK, ServerListJson((42, request)))
            .Respond(HttpStatusCode.OK, PrimaryIpListJson((91, request, 42L)))
            .Respond(HttpStatusCode.OK, FirewallListJson((19, request)))
            .Respond(HttpStatusCode.OK, PrimaryIpResponseJson(91, request, assigneeId: 42))
            .Respond(HttpStatusCode.OK, FirewallResponseJson(19, request, appliedServerIds));

        var result = await CreateProvider(handler).ProvisionAsync(request);

        Assert.Equal(ERuntimeProviderOutcome.Unknown, result.Outcome);
        Assert.Equal("firewall-attachment-unverified", result.Stage);
        Assert.Equal(3, result.ProvisionedResources.Count);
        Assert.DoesNotContain(handler.Requests, q => q.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task Provision_RecoversAmbiguousFirewallCreateByStableIntentBeforeContinuing()
    {
        var request = CreateProvisionRequest();
        var handler = new SequencedHttpMessageHandler()
            .Respond(HttpStatusCode.OK, ServerListJson())
            .Respond(HttpStatusCode.OK, PrimaryIpListJson())
            .Respond(HttpStatusCode.OK, FirewallListJson())
            .Throw(new HttpRequestException("simulated lost create response"))
            .Respond(HttpStatusCode.OK, FirewallListJson((19, request)))
            .Respond(HttpStatusCode.Created, PrimaryIpResponseJson(91, request))
            .Respond(HttpStatusCode.Created, ServerResponseJson(42, request))
            .Respond(HttpStatusCode.OK, PrimaryIpResponseJson(91, request, assigneeId: 42))
            .Respond(HttpStatusCode.OK, FirewallResponseJson(19, request, 42));

        var result = await CreateProvider(handler).ProvisionAsync(request);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("recovered-after-ambiguous-create", result.Stage);
        Assert.Equal(3, handler.Requests.Count(q => q.Method == HttpMethod.Post));
        Assert.DoesNotContain(TestToken, result.Error ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(request.CloudInit!, result.Error ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Provision_RetryWithOnlyFirewallVisibleNeverPostsSecondPrimaryIp()
    {
        var request = CreateProvisionRequest();
        var handler = new SequencedHttpMessageHandler()
            .Respond(HttpStatusCode.OK, ServerListJson())
            .Respond(HttpStatusCode.OK, PrimaryIpListJson())
            .Respond(HttpStatusCode.OK, FirewallListJson())
            .Respond(HttpStatusCode.Created, FirewallResponseJson(19, request))
            .Throw(new HttpRequestException("simulated lost Primary IP create response"))
            .Respond(HttpStatusCode.OK, PrimaryIpListJson())
            .Respond(HttpStatusCode.OK, ServerListJson())
            .Respond(HttpStatusCode.OK, PrimaryIpListJson())
            .Respond(HttpStatusCode.OK, FirewallListJson((19, request)));
        var provider = CreateProvider(handler);

        var first = await provider.ProvisionAsync(request);
        var second = await provider.ProvisionAsync(request);

        Assert.Equal(ERuntimeProviderOutcome.Unknown, first.Outcome);
        Assert.Equal("primary-ip-create-outcome-unknown", first.Stage);
        Assert.Equal(ERuntimeProviderOutcome.Unknown, second.Outcome);
        Assert.Equal("provision-incomplete-resource-graph", second.Stage);
        Assert.Equal(2, handler.Requests.Count(q => q.Method == HttpMethod.Post));
        Assert.Equal(1, handler.Requests.Count(q =>
            q.Method == HttpMethod.Post && q.Path == "/v1/primary_ips"));
        Assert.Equal(
            [
                HetznerRuntimeProvider.ServerResourceType,
                HetznerRuntimeProvider.PrimaryIpResourceType,
                HetznerRuntimeProvider.FirewallResourceType,
            ],
            provider.Capabilities.RequiredProvisionedResourceTypes);
    }

    [Fact]
    public async Task Discover_InventoriesServerPrimaryIpAndFirewallWithTrustClassification()
    {
        var request = CreateProvisionRequest();
        var incompleteManaged = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [HetznerRuntimeProvider.ManagedLabel] = "true",
            [HetznerRuntimeProvider.ResourceKindLabel] = HetznerRuntimeProvider.PrimaryIpResourceType,
        };
        var handler = new SequencedHttpMessageHandler()
            .Respond(HttpStatusCode.OK, ServerListJson((42, request)))
            .Respond(
                HttpStatusCode.OK,
                PrimaryIpListJsonWithLabels(
                    (91, Labels(request, HetznerRuntimeProvider.PrimaryIpResourceType), 42L),
                    (92, incompleteManaged, null)))
            .Respond(HttpStatusCode.OK, FirewallListJson((19, request)));

        var result = await CreateProvider(handler).DiscoverAsync();

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(4, result.Resources.Count);
        Assert.All(result.Resources, q => Assert.True(q.IsPlatformManaged));
        Assert.True(result.Resources.Single(q => q.ResourceIdentifier == "primary-ip:91").HasTrustworthyAttribution);
        Assert.False(result.Resources.Single(q => q.ResourceIdentifier == "primary-ip:92").HasTrustworthyAttribution);
        Assert.Equal(
            ["/v1/servers", "/v1/primary_ips", "/v1/firewalls"],
            handler.Requests.Select(q => q.Path).ToArray());
    }

    [Fact]
    public async Task Discover_ClassifiesRecoveredGraphRelationshipsAsReadyPendingOrConflicting()
    {
        var request = CreateProvisionRequest();
        var ready = await CreateProvider(new SequencedHttpMessageHandler()
                .Respond(HttpStatusCode.OK, ServerListJson((42, request)))
                .Respond(HttpStatusCode.OK, PrimaryIpListJson((91, request, 42L)))
                .Respond(HttpStatusCode.OK, FirewallListJsonWithAttachments((19, request, [42L]))))
            .DiscoverAsync();
        var pending = await CreateProvider(new SequencedHttpMessageHandler()
                .Respond(HttpStatusCode.OK, ServerListJson((42, request)))
                .Respond(HttpStatusCode.OK, PrimaryIpListJson((91, request, null)))
                .Respond(HttpStatusCode.OK, FirewallListJson((19, request))))
            .DiscoverAsync();
        var conflicting = await CreateProvider(new SequencedHttpMessageHandler()
                .Respond(HttpStatusCode.OK, ServerListJson((42, request)))
                .Respond(HttpStatusCode.OK, PrimaryIpListJson((91, request, 43L)))
                .Respond(HttpStatusCode.OK, FirewallListJsonWithAttachments((19, request, [43L]))))
            .DiscoverAsync();

        Assert.All(ready.Resources, q => Assert.True(q.IsProvisioningReady));
        Assert.All(
            pending.Resources.Where(q => q.ResourceType is "primary-ip" or "firewall"),
            q =>
            {
                Assert.Equal(ERuntimeResourceProvisioningReadiness.Pending, q.ProvisioningReadiness);
                Assert.False(q.HasProvisioningConflict);
            });
        Assert.All(
            conflicting.Resources.Where(q => q.ResourceType is "primary-ip" or "firewall"),
            q => Assert.True(q.HasProvisioningConflict));
    }

    [Fact]
    public async Task Discover_ClassifiesUnsafePrimaryIpTypeOrAutoDeleteAsConflicting()
    {
        var request = CreateProvisionRequest();
        var handler = new SequencedHttpMessageHandler()
            .Respond(HttpStatusCode.OK, ServerListJson((42, request)))
            .Respond(
                HttpStatusCode.OK,
                PrimaryIpListJsonWithSafety(
                    (91, request, 42L, "ipv4", false),
                    (92, request, 42L, "ipv6", true)))
            .Respond(HttpStatusCode.OK, FirewallListJsonWithAttachments((19, request, [42L])));

        var result = await CreateProvider(handler).DiscoverAsync();

        Assert.All(
            result.Resources.Where(q => q.ResourceType == "primary-ip"),
            q => Assert.True(q.HasProvisioningConflict));
    }

    [Fact]
    public async Task ReleasePrimaryIp_PowersOffUnassignsDeletesAndRequiresConfirmedAbsence()
    {
        var request = CreateProvisionRequest();
        var firstPass = new SequencedHttpMessageHandler()
            .Respond(HttpStatusCode.OK, PrimaryIpResponseJson(91, request, assigneeId: 42))
            .Respond(HttpStatusCode.OK, ServerResponseJson(42, request, status: "running"))
            .Respond(HttpStatusCode.Created, "{}");
        var provider = CreateProvider(firstPass);
        var releaseRequest = new RuntimeReleaseRequest(
            request.ServerId,
            request.DeploymentId,
            "primary-ip:91",
            request.ProvisioningIntentId,
            request.WorkerId);

        var pending = await provider.ReleaseAsync(releaseRequest);

        Assert.False(pending.IsVerifiedNonBillable);
        Assert.Equal("release-poweroff-pending", pending.Stage);
        Assert.Equal("/v1/servers/42/actions/poweroff", firstPass.Requests[2].Path);

        var secondPass = new SequencedHttpMessageHandler()
            .Respond(HttpStatusCode.OK, PrimaryIpResponseJson(91, request, assigneeId: 42))
            .Respond(HttpStatusCode.OK, ServerResponseJson(42, request, status: "off"))
            .Respond(HttpStatusCode.Created, "{}")
            .Respond(HttpStatusCode.OK, PrimaryIpResponseJson(91, request))
            .Respond(HttpStatusCode.NoContent, string.Empty)
            .Respond(HttpStatusCode.NotFound, "{}");

        var released = await CreateProvider(secondPass).ReleaseAsync(releaseRequest);

        Assert.True(released.Succeeded, released.Error);
        Assert.True(released.IsVerifiedNonBillable);
        Assert.Equal(
            [HttpMethod.Get, HttpMethod.Get, HttpMethod.Post, HttpMethod.Get, HttpMethod.Delete, HttpMethod.Get],
            secondPass.Requests.Select(q => q.Method).ToArray());
        Assert.Equal("/v1/primary_ips/91/actions/unassign", secondPass.Requests[2].Path);
    }

    [Fact]
    public async Task ReleasePrimaryIp_RefusesToPowerOffServerWithDifferentAttribution()
    {
        var request = CreateProvisionRequest();
        var unrelated = CreateProvisionRequest();
        var handler = new SequencedHttpMessageHandler()
            .Respond(HttpStatusCode.OK, PrimaryIpResponseJson(91, request, assigneeId: 42))
            .Respond(HttpStatusCode.OK, ServerResponseJson(42, unrelated, status: "running"));
        var provider = CreateProvider(handler);

        var result = await provider.ReleaseAsync(new RuntimeReleaseRequest(
            request.ServerId,
            request.DeploymentId,
            "primary-ip:91",
            request.ProvisioningIntentId,
            request.WorkerId));

        Assert.False(result.Succeeded);
        Assert.False(result.IsVerifiedNonBillable);
        Assert.Equal("release-assignee-attribution-conflict", result.Stage);
        Assert.Equal([HttpMethod.Get, HttpMethod.Get], handler.Requests.Select(q => q.Method).ToArray());
        Assert.DoesNotContain(handler.Requests, q => q.Method is not null && q.Method != HttpMethod.Get);
    }

    [Fact]
    public async Task ReleaseDangling_RequiresExactManagementAndResourceKindMarkers()
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [HetznerRuntimeProvider.ManagedLabel] = "true",
        };
        var handler = new SequencedHttpMessageHandler()
            .Respond(HttpStatusCode.OK, FirewallResponseJson(19, labels));

        var result = await CreateProvider(handler).ReleaseDanglingAsync(
            new RuntimeDanglingReleaseRequest("firewall:19", "firewall"));

        Assert.False(result.Succeeded);
        Assert.False(result.IsVerifiedNonBillable);
        Assert.Equal("release-unattributed", result.Stage);
        Assert.Single(handler.Requests);
        Assert.DoesNotContain(handler.Requests, q => q.Method == HttpMethod.Delete);
    }

    [Fact]
    public async Task ReleaseDangling_AllowsIncompleteDbAttributionWithExactPlatformMarkers()
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [HetznerRuntimeProvider.ManagedLabel] = "true",
            [HetznerRuntimeProvider.ResourceKindLabel] = HetznerRuntimeProvider.FirewallResourceType,
        };
        var handler = new SequencedHttpMessageHandler()
            .Respond(HttpStatusCode.OK, FirewallResponseJson(19, labels))
            .Respond(HttpStatusCode.NoContent, string.Empty)
            .Respond(HttpStatusCode.NotFound, "{}");

        var result = await CreateProvider(handler).ReleaseDanglingAsync(
            new RuntimeDanglingReleaseRequest("firewall:19", "firewall"));

        Assert.True(result.Succeeded, result.Error);
        Assert.True(result.IsVerifiedNonBillable);
        Assert.Equal(
            [HttpMethod.Get, HttpMethod.Delete, HttpMethod.Get],
            handler.Requests.Select(q => q.Method).ToArray());
    }

    [Fact]
    public async Task ReleaseDangling_RefusesToDeleteFirewallWhileItIsStillApplied()
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [HetznerRuntimeProvider.ManagedLabel] = "true",
            [HetznerRuntimeProvider.ResourceKindLabel] = HetznerRuntimeProvider.FirewallResourceType,
        };
        var handler = new SequencedHttpMessageHandler()
            .Respond(HttpStatusCode.OK, FirewallResponseJson(19, labels, 42));

        var result = await CreateProvider(handler).ReleaseDanglingAsync(
            new RuntimeDanglingReleaseRequest("firewall:19", "firewall"));

        Assert.False(result.Succeeded);
        Assert.False(result.IsVerifiedNonBillable);
        Assert.Equal("release-firewall-still-applied", result.Stage);
        Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
    }

    [Fact]
    public void Capabilities_AreNotReadyWithoutConservativeCostEstimate()
    {
        var provider = CreateProvider(new SequencedHttpMessageHandler(), estimatedHourlyCostEur: 0m);

        Assert.False(provider.Capabilities.IsReady);
        Assert.Null(provider.Capabilities.EstimatedHourlyCostEur);
        Assert.Contains("EstimatedHourlyCostEur", provider.Capabilities.ReadinessFailure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Provision_RejectsEmptyInboundNetworkPlanBeforeAnyProviderRequest()
    {
        var handler = new SequencedHttpMessageHandler();
        var request = CreateProvisionRequest() with
        {
            InboundNetworkRequirements = [],
        };

        var result = await CreateProvider(handler).ProvisionAsync(request);

        Assert.Equal(ERuntimeProviderOutcome.PermanentFailure, result.Outcome);
        Assert.Equal("provision-validation", result.Stage);
        Assert.Contains("At least one", result.Error, StringComparison.Ordinal);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData((int)ERuntimeNetworkProtocol.Udp, 0, 100)]
    [InlineData((int)ERuntimeNetworkProtocol.Tcp, 100, 65536)]
    [InlineData((int)ERuntimeNetworkProtocol.Udp, 200, 199)]
    [InlineData(99, 200, 200)]
    public async Task Provision_RejectsInvalidInboundNetworkRuleBeforeAnyProviderRequest(
        int protocol,
        int startPort,
        int endPort)
    {
        var handler = new SequencedHttpMessageHandler();
        var request = CreateProvisionRequest() with
        {
            InboundNetworkRequirements =
            [
                new((ERuntimeNetworkProtocol)protocol, startPort, endPort, "Invalid test traffic"),
            ],
        };

        var result = await CreateProvider(handler).ProvisionAsync(request);

        Assert.Equal(ERuntimeProviderOutcome.PermanentFailure, result.Outcome);
        Assert.Equal("provision-validation", result.Stage);
        Assert.NotNull(result.Error);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Provision_RejectsExcessiveInboundNetworkRuleCountBeforeAnyProviderRequest()
    {
        var handler = new SequencedHttpMessageHandler();
        var request = CreateProvisionRequest() with
        {
            InboundNetworkRequirements = Enumerable.Range(0, 33)
                .Select(q => new RuntimeInboundNetworkRequirement(
                    ERuntimeNetworkProtocol.Udp,
                    40000 + q,
                    40000 + q,
                    $"Test traffic {q}"))
                .ToArray(),
        };

        var result = await CreateProvider(handler).ProvisionAsync(request);

        Assert.Equal(ERuntimeProviderOutcome.PermanentFailure, result.Outcome);
        Assert.Equal("provision-validation", result.Stage);
        Assert.Contains("No more than 32", result.Error, StringComparison.Ordinal);
        Assert.Empty(handler.Requests);
    }

    private const string TestToken = "test-hetzner-token-do-not-log";

    private static RuntimeProvisionRequest CreateProvisionRequest()
        => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "test-game",
            Guid.NewGuid(),
            "#cloud-config\nwrite_files: []",
            [
                new(ERuntimeNetworkProtocol.Tcp, 30100, 30100, "Primary test traffic"),
                new(ERuntimeNetworkProtocol.Udp, 30110, 30112, "Secondary test traffic"),
            ]);

    private static HetznerRuntimeProvider CreateProvider(
        SequencedHttpMessageHandler handler,
        decimal estimatedHourlyCostEur = 0.10m)
    {
        var optionsProvider = new TestOptionsProvider(new HetznerRuntimeOptions
        {
            Token = TestToken,
            BaseUrl = "https://unit.test/v1/",
            AllowCustomEndpointForTesting = true,
            ServerType = "ccx13",
            Image = "ubuntu-24.04",
            Location = "fsn1",
            EstimatedHourlyCostEur = estimatedHourlyCostEur,
        });
        var client = new HetznerCloudClient(new TestHttpClientFactory(handler), optionsProvider);
        return new HetznerRuntimeProvider(client, optionsProvider, NullLogger<HetznerRuntimeProvider>.Instance);
    }

    private static string ServerResponseJson(
        long id,
        RuntimeProvisionRequest request,
        string status = "running")
        => ServerResponseJson(id, Labels(request, HetznerRuntimeProvider.ServerResourceType), status);

    private static string ServerResponseJson(
        long id,
        IReadOnlyDictionary<string, string> labels,
        string status = "running")
        => JsonSerializer.Serialize(new
        {
            server = new
            {
                id,
                name = $"utf-{id}",
                status,
                labels,
                public_net = new { ipv4 = new { ip = $"203.0.113.{id}" } },
            },
        });

    private static string PrimaryIpResponseJson(
        long id,
        RuntimeProvisionRequest request,
        long? assigneeId = null)
        => PrimaryIpResponseJson(
            id,
            Labels(request, HetznerRuntimeProvider.PrimaryIpResourceType),
            assigneeId);

    private static string PrimaryIpResponseJson(
        long id,
        IReadOnlyDictionary<string, string> labels,
        long? assigneeId = null)
        => JsonSerializer.Serialize(new
        {
            primary_ip = new
            {
                id,
                name = $"utf-{id}",
                ip = $"203.0.113.{id}",
                type = "ipv4",
                assignee_id = assigneeId,
                assignee_type = assigneeId.HasValue ? "server" : "unassigned",
                auto_delete = true,
                labels,
            },
        });

    private static string FirewallResponseJson(
        long id,
        RuntimeProvisionRequest request,
        params long[] appliedServerIds)
        => FirewallResponseJson(
            id,
            Labels(request, HetznerRuntimeProvider.FirewallResourceType),
            appliedServerIds);

    private static string FirewallResponseJson(
        long id,
        IReadOnlyDictionary<string, string> labels,
        params long[] appliedServerIds)
        => JsonSerializer.Serialize(new
        {
            firewall = new
            {
                id,
                name = $"utf-{id}",
                labels,
                applied_to = appliedServerIds.Select(q => new { type = "server", server = new { id = q } }),
            },
        });

    private static string ServerListJson(params (long Id, RuntimeProvisionRequest Request)[] values)
        => JsonSerializer.Serialize(new
        {
            servers = values.Select(q => new
            {
                id = q.Id,
                name = $"utf-{q.Id}",
                status = "running",
                labels = Labels(q.Request, HetznerRuntimeProvider.ServerResourceType),
                public_net = new { ipv4 = new { ip = $"203.0.113.{q.Id}" } },
            }),
            meta = Pagination(),
        });

    private static string PrimaryIpListJson(
        params (long Id, RuntimeProvisionRequest Request, long? AssigneeId)[] values)
        => PrimaryIpListJsonWithLabels(values.Select(q => (
            q.Id,
            (IReadOnlyDictionary<string, string>)Labels(q.Request, HetznerRuntimeProvider.PrimaryIpResourceType),
            q.AssigneeId)).ToArray());

    private static string PrimaryIpListJsonWithLabels(
        params (long Id, IReadOnlyDictionary<string, string> Labels, long? AssigneeId)[] values)
        => JsonSerializer.Serialize(new
        {
            primary_ips = values.Select(q => new
            {
                id = q.Id,
                name = $"utf-{q.Id}",
                ip = $"203.0.113.{q.Id}",
                type = "ipv4",
                assignee_id = q.AssigneeId,
                assignee_type = q.AssigneeId.HasValue ? "server" : "unassigned",
                auto_delete = true,
                labels = q.Labels,
            }),
            meta = Pagination(),
        });

    private static string PrimaryIpListJsonWithSafety(
        params (long Id, RuntimeProvisionRequest Request, long? AssigneeId, string Type, bool AutoDelete)[] values)
        => JsonSerializer.Serialize(new
        {
            primary_ips = values.Select(q => new
            {
                id = q.Id,
                name = $"utf-{q.Id}",
                ip = $"203.0.113.{q.Id}",
                type = q.Type,
                assignee_id = q.AssigneeId,
                assignee_type = q.AssigneeId.HasValue ? "server" : "unassigned",
                auto_delete = q.AutoDelete,
                labels = Labels(q.Request, HetznerRuntimeProvider.PrimaryIpResourceType),
            }),
            meta = Pagination(),
        });

    private static string FirewallListJson(params (long Id, RuntimeProvisionRequest Request)[] values)
        => JsonSerializer.Serialize(new
        {
            firewalls = values.Select(q => new
            {
                id = q.Id,
                name = $"utf-{q.Id}",
                labels = Labels(q.Request, HetznerRuntimeProvider.FirewallResourceType),
                applied_to = Array.Empty<object>(),
            }),
            meta = Pagination(),
        });

    private static string FirewallListJsonWithAttachments(
        params (long Id, RuntimeProvisionRequest Request, long[] AppliedServerIds)[] values)
        => JsonSerializer.Serialize(new
        {
            firewalls = values.Select(q => new
            {
                id = q.Id,
                name = $"utf-{q.Id}",
                labels = Labels(q.Request, HetznerRuntimeProvider.FirewallResourceType),
                applied_to = q.AppliedServerIds.Select(id => new { type = "server", server = new { id } }),
            }),
            meta = Pagination(),
        });

    private static object Pagination() => new { pagination = new { next_page = (int?)null } };

    private static Dictionary<string, string> Labels(RuntimeProvisionRequest request, string resourceType)
        => new(StringComparer.Ordinal)
        {
            [HetznerRuntimeProvider.ManagedLabel] = "true",
            [HetznerRuntimeProvider.ServerLabel] = request.ServerId.ToString("N"),
            [HetznerRuntimeProvider.DeploymentLabel] = request.DeploymentId.ToString("N"),
            [HetznerRuntimeProvider.IntentLabel] = request.ProvisioningIntentId.ToString("N"),
            [HetznerRuntimeProvider.WorkerLabel] = request.WorkerId.ToString("N"),
            [HetznerRuntimeProvider.ResourceKindLabel] = resourceType,
        };

    private sealed class TestOptionsProvider(HetznerRuntimeOptions value) : IHetznerRuntimeOptionsProvider
    {
        public HetznerRuntimeOptions Value { get; } = value;
    }

    private sealed class TestHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class SequencedHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> _responses = new();

        public List<ObservedRequest> Requests { get; } = [];

        public SequencedHttpMessageHandler Respond(HttpStatusCode statusCode, string json)
        {
            _responses.Enqueue(() => new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
            return this;
        }

        public SequencedHttpMessageHandler Throw(HttpRequestException exception)
        {
            _responses.Enqueue(() => throw exception);
            return this;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new ObservedRequest(
                request.Method,
                request.RequestUri?.AbsolutePath ?? string.Empty,
                request.RequestUri?.PathAndQuery ?? string.Empty,
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                body));
            return _responses.Dequeue().Invoke();
        }
    }

    private sealed record ObservedRequest(
        HttpMethod Method,
        string Path,
        string PathAndQuery,
        string? AuthorizationScheme,
        string? AuthorizationParameter,
        string Body);
}

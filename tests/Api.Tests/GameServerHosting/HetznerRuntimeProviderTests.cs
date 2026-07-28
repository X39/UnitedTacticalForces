using System.Net;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging.Abstractions;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using Xunit;

namespace Api.Tests.GameServerHosting;

public sealed class HetznerRuntimeProviderTests
{
    [Fact]
    public async Task Profiles_ReturnAvailableX86SizesAndProviderPricesForConfiguredLocation()
    {
        var handler = new SequencedHttpMessageHandler().Respond(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(new
            {
                server_types = new object[]
                {
                    new
                    {
                        name = "ccx13",
                        description = "Dedicated AMD",
                        cores = 2,
                        memory = 8,
                        disk = 80,
                        cpu_type = "dedicated",
                        architecture = "x86",
                        deprecation = (object?)null,
                        locations = new[] { new { location = new { name = "fsn1" }, available = true } },
                        prices = new[]
                        {
                            new
                            {
                                location = "fsn1",
                                price_hourly = new { gross = "0.0800" },
                                price_monthly = new { gross = "50.00" },
                            },
                        },
                    },
                    new
                    {
                        name = "cax11",
                        description = "Arm",
                        cores = 2,
                        memory = 4,
                        disk = 40,
                        cpu_type = "shared",
                        architecture = "arm",
                        deprecation = (object?)null,
                        locations = new[] { new { location = new { name = "fsn1" }, available = true } },
                        prices = Array.Empty<object>(),
                    },
                },
                meta = new { pagination = new { next_page = (int?)null } },
            }));

        var profiles = await CreateProvider(handler, estimatedHourlyCostEur: 0.10m).GetProfilesAsync();

        var profile = Assert.Single(profiles);
        Assert.Equal("ccx13", profile.Identifier);
        Assert.Equal(2, profile.VirtualCpuCount);
        Assert.Equal(8m, profile.MemoryGiB);
        Assert.Equal(0.10m, profile.EstimatedHourlyCostEur);
        Assert.Equal(64.60m, profile.EstimatedMonthlyCostEur);
        Assert.Equal("/v1/server_types", Assert.Single(handler.Requests).Path);
    }

    [Fact]
    public async Task Provision_EmitsOneProviderSpanPerHttpCallUnderRuntimeOperation()
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
        var stopped = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == HostingTelemetry.SourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = stopped.Add,
        };
        ActivitySource.AddActivityListener(listener);

        var result = await CreateProvider(handler).ProvisionAsync(request);

        Assert.True(result.Succeeded, result.Error);
        var requests = stopped.Where(q => q.GetTagItem("http.request.method") is not null).ToArray();
        Assert.Equal(handler.Requests.Count, requests.Length);
        Assert.All(requests, activity =>
        {
            Assert.Equal("hetzner", activity.GetTagItem("utf.hosting.provider"));
            Assert.Equal("Succeeded", activity.GetTagItem("utf.hosting.outcome"));
            Assert.NotNull(activity.ParentId);
            Assert.DoesNotContain(TestToken, activity.TagObjects.Select(q => q.Value?.ToString()));
            Assert.DoesNotContain(request.CloudInit, activity.TagObjects.Select(q => q.Value?.ToString()));
        });
        var provision = Assert.Single(stopped, q => q.OperationName == "runtime.provision");
        Assert.Equal(ActivityStatusCode.Unset, provision.Status);
        Assert.Equal("Succeeded", provision.GetTagItem("utf.hosting.outcome"));
        Assert.All(requests, activity => Assert.Equal(provision.TraceId, activity.TraceId));
    }

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
        var firewallFingerprint = firewallRoot.GetProperty("labels")
            .GetProperty(HetznerRuntimeProvider.FirewallRulesFingerprintLabel)
            .GetString();
        Assert.Equal(
            HetznerRuntimeProvider.FirewallRulesFingerprint(request.InboundNetworkRequirements!),
            firewallFingerprint);
        Assert.InRange(firewallFingerprint!.Length, 1, 63);

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
        var serverFingerprint = serverRoot.GetProperty("labels")
            .GetProperty(HetznerRuntimeProvider.ServerConfigurationFingerprintLabel)
            .GetString();
        Assert.Equal(
            HetznerRuntimeProvider.ServerConfigurationFingerprint("ccx13", "ubuntu-24.04", "fsn1"),
            serverFingerprint);
        Assert.InRange(serverFingerprint!.Length, 1, 63);
    }

    [Fact]
    public async Task Provision_WhenBarrierClosesBetweenCreates_DoesNotIssueAnotherProviderPost()
    {
        var request = CreateProvisionRequest();
        var handler = new SequencedHttpMessageHandler()
            .Respond(HttpStatusCode.OK, ServerListJson())
            .Respond(HttpStatusCode.OK, PrimaryIpListJson())
            .Respond(HttpStatusCode.OK, FirewallListJson())
            .Respond(HttpStatusCode.Created, FirewallResponseJson(19, request));
        var guard = new BoundedProvisioningMutationGuard(authorizedMutationCount: 1);
        var provider = CreateProvider(handler, provisioningMutationGuard: guard);

        var result = await provider.ProvisionAsync(request);

        Assert.False(result.Succeeded);
        Assert.Equal(ERuntimeProviderOutcome.TransientFailure, result.Outcome);
        Assert.Equal("provisioning-blocked", result.Stage);
        Assert.Equal(2, guard.Attempts);
        Assert.Equal(
            ["/v1/servers", "/v1/primary_ips", "/v1/firewalls", "/v1/firewalls"],
            handler.Requests.Select(q => q.Path).ToArray());
        Assert.Single(handler.Requests, q => q.Method == HttpMethod.Post);
        Assert.Equal("firewall:19", Assert.Single(result.ProvisionedResources).ResourceIdentifier);
        Assert.True(result.IsProvisioningAttemptDefinitivelyClosed);
    }

    [Fact]
    public async Task Provision_WhenBarrierClosesAfterRecoveredUnknownCreate_ClaimsDefinitiveClosure()
    {
        var request = CreateProvisionRequest();
        var handler = new SequencedHttpMessageHandler()
            .Respond(HttpStatusCode.OK, ServerListJson())
            .Respond(HttpStatusCode.OK, PrimaryIpListJson())
            .Respond(HttpStatusCode.OK, FirewallListJson())
            .Throw(new HttpRequestException("simulated lost firewall create response"))
            .Respond(HttpStatusCode.OK, FirewallListJson((19, request)));
        var guard = new BoundedProvisioningMutationGuard(authorizedMutationCount: 1);

        var result = await CreateProvider(handler, provisioningMutationGuard: guard).ProvisionAsync(request);

        Assert.Equal(ERuntimeProviderOutcome.TransientFailure, result.Outcome);
        Assert.Equal("provisioning-blocked", result.Stage);
        Assert.True(result.IsProvisioningAttemptDefinitivelyClosed);
        Assert.Equal("firewall:19", Assert.Single(result.ProvisionedResources).ResourceIdentifier);
        Assert.Single(handler.Requests, q => q.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task Provision_WhenPreflightDiscoveryFailsBeforeMutation_ClaimsDefinitiveClosure()
    {
        var request = CreateProvisionRequest();
        var handler = new SequencedHttpMessageHandler()
            .Throw(new HttpRequestException("simulated preflight transport failure"))
            .Respond(HttpStatusCode.OK, PrimaryIpListJson())
            .Respond(HttpStatusCode.OK, FirewallListJson());

        var result = await CreateProvider(handler).ProvisionAsync(request);

        Assert.Equal(ERuntimeProviderOutcome.TransientFailure, result.Outcome);
        Assert.Equal("provision-discovery-failed", result.Stage);
        Assert.True(result.IsProvisioningAttemptDefinitivelyClosed);
        Assert.Empty(result.ProvisionedResources);
        Assert.DoesNotContain(handler.Requests, q => q.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task Provision_WhenFirstCreateIsRateLimited_ClosesAttemptWithoutResources()
    {
        var request = CreateProvisionRequest();
        var handler = new SequencedHttpMessageHandler()
            .Respond(HttpStatusCode.OK, ServerListJson())
            .Respond(HttpStatusCode.OK, PrimaryIpListJson())
            .Respond(HttpStatusCode.OK, FirewallListJson())
            .Respond(HttpStatusCode.TooManyRequests, "{}");

        var result = await CreateProvider(handler).ProvisionAsync(request);

        Assert.Equal(ERuntimeProviderOutcome.TransientFailure, result.Outcome);
        Assert.Equal("firewall-create-failed", result.Stage);
        Assert.True(result.IsProvisioningAttemptDefinitivelyClosed);
        Assert.Empty(result.ProvisionedResources);
        Assert.Single(handler.Requests, q => q.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task Provision_WhenSecondCreateIsRateLimited_ClosesKnownPartialGraph()
    {
        var request = CreateProvisionRequest();
        var handler = new SequencedHttpMessageHandler()
            .Respond(HttpStatusCode.OK, ServerListJson())
            .Respond(HttpStatusCode.OK, PrimaryIpListJson())
            .Respond(HttpStatusCode.OK, FirewallListJson())
            .Respond(HttpStatusCode.Created, FirewallResponseJson(19, request))
            .Respond(HttpStatusCode.TooManyRequests, "{}");

        var result = await CreateProvider(handler).ProvisionAsync(request);

        Assert.Equal(ERuntimeProviderOutcome.TransientFailure, result.Outcome);
        Assert.Equal("primary-ip-create-failed", result.Stage);
        Assert.True(result.IsProvisioningAttemptDefinitivelyClosed);
        Assert.Equal("firewall:19", Assert.Single(result.ProvisionedResources).ResourceIdentifier);
        Assert.Equal(2, handler.Requests.Count(q => q.Method == HttpMethod.Post));
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
    [InlineData("cpx31", "ubuntu-24.04", "fsn1")]
    [InlineData("ccx13", "ubuntu-22.04", "fsn1")]
    [InlineData("ccx13", "ubuntu-24.04", "nbg1")]
    public async Task Provision_RejectsRecoveredGraphWithMismatchedAuthoritativeServerConfiguration(
        string serverType,
        string image,
        string location)
    {
        var request = CreateProvisionRequest();
        var handler = new SequencedHttpMessageHandler()
            .Respond(
                HttpStatusCode.OK,
                ServerListJsonWithConfiguration((42, request, "running", serverType, image, location)))
            .Respond(HttpStatusCode.OK, PrimaryIpListJson((91, request, 42L)))
            .Respond(HttpStatusCode.OK, FirewallListJsonWithAttachments((19, request, [42L])));

        var result = await CreateProvider(handler).ProvisionAsync(request);

        Assert.Equal(ERuntimeProviderOutcome.PermanentFailure, result.Outcome);
        Assert.Equal("server-configuration-mismatch", result.Stage);
        Assert.Null(result.Resource);
        Assert.Equal(3, result.ProvisionedResources.Count);
        Assert.DoesNotContain(handler.Requests, q => q.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task Provision_AcceptsExactFirewallSemanticsRegardlessOfRuleOrderOrSinglePortNotation()
    {
        var request = CreateProvisionRequest();
        var rules = ExpectedFirewallRules(request)
            .Reverse()
            .Select(q => q.Port == "30100"
                ? q with { Port = "30100-30100", Description = "Non-semantic provider description" }
                : q)
            .ToArray();
        var labels = Labels(request, HetznerRuntimeProvider.FirewallResourceType);
        var handler = new SequencedHttpMessageHandler()
            .Respond(HttpStatusCode.OK, ServerListJson((42, request)))
            .Respond(HttpStatusCode.OK, PrimaryIpListJson((91, request, 42L)))
            .Respond(HttpStatusCode.OK, FirewallListJsonWithRules((19, request, [42L], rules)))
            .Respond(HttpStatusCode.OK, PrimaryIpResponseJson(91, request, assigneeId: 42))
            .Respond(HttpStatusCode.OK, FirewallResponseJsonWithRules(19, labels, rules, 42));

        var result = await CreateProvider(handler).ProvisionAsync(request);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("recovered-existing", result.Stage);
        Assert.Equal(3, result.ProvisionedResources.Count);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("direction")]
    [InlineData("protocol")]
    [InlineData("port")]
    [InlineData("source")]
    [InlineData("destination")]
    public async Task Provision_RejectsRecoveredGraphWithFirewallSemanticMismatch(string mutation)
    {
        var request = CreateProvisionRequest();
        var rules = ExpectedFirewallRules(request).ToList();
        switch (mutation)
        {
            case "missing":
                rules.RemoveAt(1);
                break;
            case "extra":
                rules.Add(rules[0] with { Port = "30200" });
                break;
            case "direction":
                rules[0] = rules[0] with { Direction = "out" };
                break;
            case "protocol":
                rules[0] = rules[0] with { Protocol = "udp" };
                break;
            case "port":
                rules[0] = rules[0] with { Port = "30101" };
                break;
            case "source":
                rules[0] = rules[0] with { SourceIps = ["10.0.0.0/8"] };
                break;
            case "destination":
                rules[0] = rules[0] with { DestinationIps = ["192.0.2.1/32"] };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }
        var handler = new SequencedHttpMessageHandler()
            .Respond(HttpStatusCode.OK, ServerListJson((42, request)))
            .Respond(HttpStatusCode.OK, PrimaryIpListJson((91, request, 42L)))
            .Respond(HttpStatusCode.OK, FirewallListJsonWithRules((19, request, [42L], rules.ToArray())));

        var result = await CreateProvider(handler).ProvisionAsync(request);

        Assert.Equal(ERuntimeProviderOutcome.PermanentFailure, result.Outcome);
        Assert.Equal("firewall-rules-mismatch", result.Stage);
        Assert.Null(result.Resource);
        Assert.Equal(3, result.ProvisionedResources.Count);
        Assert.DoesNotContain(handler.Requests, q => q.Method == HttpMethod.Post);
    }

    [Theory]
    [InlineData("off")]
    [InlineData("stopping")]
    [InlineData("deleting")]
    [InlineData("failed")]
    public async Task Provision_ReturnsCompleteAllocatedGraphForConfirmedServerOperationalFault(string status)
    {
        var request = CreateProvisionRequest();
        var handler = new SequencedHttpMessageHandler()
            .Respond(
                HttpStatusCode.OK,
                ServerListJsonWithConfiguration((42, request, status, "ccx13", "ubuntu-24.04", "fsn1")))
            .Respond(HttpStatusCode.OK, PrimaryIpListJson((91, request, 42L)))
            .Respond(HttpStatusCode.OK, FirewallListJsonWithAttachments((19, request, [42L])));

        var result = await CreateProvider(handler).ProvisionAsync(request);

        Assert.Equal(ERuntimeProviderOutcome.PermanentFailure, result.Outcome);
        Assert.Equal("server-operational-fault", result.Stage);
        Assert.Null(result.Resource);
        Assert.Equal(3, result.ProvisionedResources.Count);
        Assert.True(result.ProvisionedResources.Single(q => q.ResourceType == "server").HasConfirmedOperationalFault);
    }

    [Theory]
    [InlineData("failed", "ccx13", "server-operational-fault")]
    [InlineData("running", "cpx31", "server-configuration-mismatch")]
    public async Task Provision_NewlyCreatedFaultedOrMismatchedServerReturnsEveryResourceForTeardown(
        string status,
        string serverType,
        string expectedStage)
    {
        var request = CreateProvisionRequest();
        var handler = new SequencedHttpMessageHandler()
            .Respond(HttpStatusCode.OK, ServerListJson())
            .Respond(HttpStatusCode.OK, PrimaryIpListJson())
            .Respond(HttpStatusCode.OK, FirewallListJson())
            .Respond(HttpStatusCode.Created, FirewallResponseJson(19, request))
            .Respond(HttpStatusCode.Created, PrimaryIpResponseJson(91, request))
            .Respond(
                HttpStatusCode.Created,
                ServerResponseJson(42, request, status: status, serverType: serverType));

        var result = await CreateProvider(handler).ProvisionAsync(request);

        Assert.Equal(ERuntimeProviderOutcome.PermanentFailure, result.Outcome);
        Assert.Equal(expectedStage, result.Stage);
        Assert.Null(result.Resource);
        Assert.Equal(3, result.ProvisionedResources.Count);
        Assert.Equal(3, handler.Requests.Count(q => q.Method == HttpMethod.Post));
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("provider-added-state")]
    [InlineData("initializing")]
    public async Task Provision_LeavesUnrecognizedOrConvergingServerStatusUncertain(string status)
    {
        var request = CreateProvisionRequest();
        var handler = new SequencedHttpMessageHandler()
            .Respond(
                HttpStatusCode.OK,
                ServerListJsonWithConfiguration((42, request, status, "ccx13", "ubuntu-24.04", "fsn1")))
            .Respond(HttpStatusCode.OK, PrimaryIpListJson((91, request, 42L)))
            .Respond(HttpStatusCode.OK, FirewallListJsonWithAttachments((19, request, [42L])));

        var result = await CreateProvider(handler).ProvisionAsync(request);

        Assert.Equal(ERuntimeProviderOutcome.Unknown, result.Outcome);
        Assert.Equal("server-operational-state-unverified", result.Stage);
        Assert.Equal(3, result.ProvisionedResources.Count);
        var server = result.ProvisionedResources.Single(q => q.ResourceType == "server");
        Assert.Equal(ERuntimeResourceOperationalState.Uncertain, server.OperationalState);
        Assert.False(server.HasConfirmedOperationalFault);
    }

    [Fact]
    public async Task Provision_DuplicateIntentGraphReturnsEveryDiscoveredResourceForTeardown()
    {
        var request = CreateProvisionRequest();
        var handler = new SequencedHttpMessageHandler()
            .Respond(HttpStatusCode.OK, ServerListJson((42, request), (43, request)))
            .Respond(HttpStatusCode.OK, PrimaryIpListJson((91, request, 42L)))
            .Respond(HttpStatusCode.OK, FirewallListJsonWithAttachments((19, request, [42L])));

        var result = await CreateProvider(handler).ProvisionAsync(request);

        Assert.Equal(ERuntimeProviderOutcome.PermanentFailure, result.Outcome);
        Assert.Equal("provision-discovery-failed", result.Stage);
        Assert.Equal(
            ["server:42", "server:43", "primary-ip:91", "firewall:19"],
            result.ProvisionedResources.Select(q => q.ResourceIdentifier).ToArray());
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

        Assert.Equal(
            appliedServerIds.Length == 0
                ? ERuntimeProviderOutcome.Unknown
                : ERuntimeProviderOutcome.PermanentFailure,
            result.Outcome);
        Assert.Equal(
            appliedServerIds.Length == 0 ? "firewall-attachment-pending" : "firewall-attachment-unverified",
            result.Stage);
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
    public async Task Provision_PersistsSanitizedProviderErrorAndRequestReference()
    {
        var request = CreateProvisionRequest();
        var handler = new SequencedHttpMessageHandler()
            .Respond(HttpStatusCode.OK, ServerListJson())
            .Respond(HttpStatusCode.OK, PrimaryIpListJson())
            .Respond(HttpStatusCode.OK, FirewallListJson())
            .Respond(
                HttpStatusCode.UnprocessableEntity,
                JsonSerializer.Serialize(new
                {
                    error = new
                    {
                        code = "invalid_input",
                        message = $"Unsupported location; token={TestToken}",
                        details = new
                        {
                            user_data = request.CloudInit,
                        },
                    },
                }),
                requestId: "provider-request-123");

        var result = await CreateProvider(handler).ProvisionAsync(request);

        Assert.Equal(ERuntimeProviderOutcome.PermanentFailure, result.Outcome);
        Assert.Equal("firewall-create-failed", result.Stage);
        Assert.Contains("invalid_input", result.Error, StringComparison.Ordinal);
        Assert.Contains("Unsupported location", result.Error, StringComparison.Ordinal);
        Assert.Contains("provider-request-123", result.Error, StringComparison.Ordinal);
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
    public async Task Discover_WhenProviderRepeatsPaginationCursor_FailsAfterOnePage()
    {
        var handler = new SequencedHttpMessageHandler().Respond(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(new
            {
                servers = Array.Empty<object>(),
                meta = new { pagination = new { next_page = (int?)1 } },
            }));

        var result = await CreateProvider(handler).DiscoverAsync();

        Assert.False(result.Succeeded);
        Assert.Equal(ERuntimeProviderOutcome.Unknown, result.Outcome);
        Assert.Equal("discovery-failed", result.Stage);
        Assert.Contains("pagination", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Single(handler.Requests);
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
    public async Task Discover_ClassifiesAuthoritativeServerAndFirewallConfigurationDriftAsConflicting()
    {
        var request = CreateProvisionRequest();
        var rules = ExpectedFirewallRules(request);
        rules[0] = rules[0] with { Port = "30101" };
        var handler = new SequencedHttpMessageHandler()
            .Respond(
                HttpStatusCode.OK,
                ServerListJsonWithConfiguration((42, request, "running", "cpx31", "ubuntu-24.04", "fsn1")))
            .Respond(HttpStatusCode.OK, PrimaryIpListJson((91, request, 42L)))
            .Respond(HttpStatusCode.OK, FirewallListJsonWithRules((19, request, [42L], rules)));

        var result = await CreateProvider(handler).DiscoverAsync();

        Assert.True(result.Resources.Single(q => q.ResourceType == "server").HasProvisioningConflict);
        Assert.True(result.Resources.Single(q => q.ResourceType == "firewall").HasProvisioningConflict);
        Assert.True(result.Resources.Single(q => q.ResourceType == "primary-ip").IsProvisioningReady);
    }

    [Fact]
    public async Task Discover_MissingExpectedConfigurationFingerprintsRemainsUncertainWithoutClaimingConflict()
    {
        var request = CreateProvisionRequest();
        var serverLabels = Labels(request, HetznerRuntimeProvider.ServerResourceType);
        serverLabels.Remove(HetznerRuntimeProvider.ServerConfigurationFingerprintLabel);
        var firewallLabels = Labels(request, HetznerRuntimeProvider.FirewallResourceType);
        firewallLabels.Remove(HetznerRuntimeProvider.FirewallRulesFingerprintLabel);
        var handler = new SequencedHttpMessageHandler()
            .Respond(
                HttpStatusCode.OK,
                ServerListJsonWithLabelsAndConfiguration(
                    (42, serverLabels, "running", "ccx13", "ubuntu-24.04", "fsn1")))
            .Respond(HttpStatusCode.OK, PrimaryIpListJson((91, request, 42L)))
            .Respond(
                HttpStatusCode.OK,
                FirewallListJsonWithLabelsAndRules(
                    (19, firewallLabels, [42L], ExpectedFirewallRules(request))));

        var result = await CreateProvider(handler).DiscoverAsync();

        Assert.All(
            result.Resources.Where(q => q.ResourceType is "server" or "firewall"),
            resource =>
            {
                Assert.Equal(ERuntimeResourceProvisioningReadiness.Pending, resource.ProvisioningReadiness);
                Assert.False(resource.HasProvisioningConflict);
                Assert.False(resource.IsProvisioningReady);
            });
    }

    [Fact]
    public async Task Discover_MissingAuthoritativeConfigurationFieldsRemainsUncertainWithoutClaimingConflict()
    {
        var request = CreateProvisionRequest();
        var handler = new SequencedHttpMessageHandler()
            .Respond(HttpStatusCode.OK, ServerListJsonWithoutConfiguration(42, request))
            .Respond(HttpStatusCode.OK, PrimaryIpListJson((91, request, 42L)))
            .Respond(HttpStatusCode.OK, FirewallListJsonWithoutRules(19, request, 42));

        var result = await CreateProvider(handler).DiscoverAsync();

        Assert.All(
            result.Resources.Where(q => q.ResourceType is "server" or "firewall"),
            resource =>
            {
                Assert.Equal(ERuntimeResourceProvisioningReadiness.Pending, resource.ProvisioningReadiness);
                Assert.False(resource.HasProvisioningConflict);
            });
    }

    [Theory]
    [InlineData("running", (int)ERuntimeResourceOperationalState.Operational, false)]
    [InlineData("off", (int)ERuntimeResourceOperationalState.Faulted, true)]
    [InlineData("stopping", (int)ERuntimeResourceOperationalState.Faulted, true)]
    [InlineData("deleting", (int)ERuntimeResourceOperationalState.Faulted, true)]
    [InlineData("failed", (int)ERuntimeResourceOperationalState.Faulted, true)]
    [InlineData("unknown", (int)ERuntimeResourceOperationalState.Uncertain, false)]
    [InlineData("provider-added-state", (int)ERuntimeResourceOperationalState.Uncertain, false)]
    public async Task Discover_MapsServerStatusToProviderNeutralOperationalEvidence(
        string status,
        int expectedState,
        bool expectedFault)
    {
        var request = CreateProvisionRequest();
        var handler = new SequencedHttpMessageHandler()
            .Respond(
                HttpStatusCode.OK,
                ServerListJsonWithConfiguration((42, request, status, "ccx13", "ubuntu-24.04", "fsn1")))
            .Respond(HttpStatusCode.OK, PrimaryIpListJson())
            .Respond(HttpStatusCode.OK, FirewallListJson());

        var result = await CreateProvider(handler).DiscoverAsync();

        var server = Assert.Single(result.Resources);
        Assert.Equal((ERuntimeResourceOperationalState)expectedState, server.OperationalState);
        Assert.Equal(expectedFault, server.HasConfirmedOperationalFault);
        Assert.True(server.IsProvisioningReady);
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
        decimal estimatedHourlyCostEur = 0.10m,
        IProviderProvisioningMutationGuard? provisioningMutationGuard = null)
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
        return new HetznerRuntimeProvider(
            client,
            optionsProvider,
            provisioningMutationGuard ?? new AlwaysAuthorizedProvisioningMutationGuard(),
            NullLogger<HetznerRuntimeProvider>.Instance);
    }

    private static string ServerResponseJson(
        long id,
        RuntimeProvisionRequest request,
        string status = "running",
        string serverType = "ccx13",
        string image = "ubuntu-24.04",
        string location = "fsn1")
        => ServerResponseJson(
            id,
            Labels(request, HetznerRuntimeProvider.ServerResourceType),
            status,
            serverType,
            image,
            location);

    private static string ServerResponseJson(
        long id,
        IReadOnlyDictionary<string, string> labels,
        string status = "running",
        string serverType = "ccx13",
        string image = "ubuntu-24.04",
        string location = "fsn1")
        => JsonSerializer.Serialize(new
        {
            server = new
            {
                id,
                name = $"utf-{id}",
                status,
                labels,
                public_net = new { ipv4 = new { ip = $"203.0.113.{id}" } },
                server_type = new { id = 1, name = serverType },
                image = new { id = 2, name = image },
                location = new { id = 3, name = location },
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
        => FirewallResponseJsonWithRules(
            id,
            Labels(request, HetznerRuntimeProvider.FirewallResourceType),
            ExpectedFirewallRules(request),
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
                rules = Array.Empty<object>(),
            },
        });

    private static string FirewallResponseJsonWithRules(
        long id,
        IReadOnlyDictionary<string, string> labels,
        IReadOnlyList<FirewallRuleFixture> rules,
        params long[] appliedServerIds)
        => JsonSerializer.Serialize(new
        {
            firewall = new
            {
                id,
                name = $"utf-{id}",
                labels,
                applied_to = appliedServerIds.Select(q => new { type = "server", server = new { id = q } }),
                rules,
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
                server_type = new { id = 1, name = RequestedServerType(q.Request) },
                image = new { id = 2, name = "ubuntu-24.04" },
                location = new { id = 3, name = "fsn1" },
            }),
            meta = Pagination(),
        });

    private static string ServerListJsonWithConfiguration(
        params (long Id, RuntimeProvisionRequest Request, string Status, string ServerType, string Image, string Location)[]
            values)
        => JsonSerializer.Serialize(new
        {
            servers = values.Select(q => new
            {
                id = q.Id,
                name = $"utf-{q.Id}",
                status = q.Status,
                labels = Labels(q.Request, HetznerRuntimeProvider.ServerResourceType),
                public_net = new { ipv4 = new { ip = $"203.0.113.{q.Id}" } },
                server_type = new { id = 1, name = q.ServerType },
                image = new { id = 2, name = q.Image },
                location = new { id = 3, name = q.Location },
            }),
            meta = Pagination(),
        });

    private static string ServerListJsonWithLabelsAndConfiguration(
        params (long Id, IReadOnlyDictionary<string, string> Labels, string Status, string ServerType, string Image,
            string Location)[] values)
        => JsonSerializer.Serialize(new
        {
            servers = values.Select(q => new
            {
                id = q.Id,
                name = $"utf-{q.Id}",
                status = q.Status,
                labels = q.Labels,
                public_net = new { ipv4 = new { ip = $"203.0.113.{q.Id}" } },
                server_type = new { id = 1, name = q.ServerType },
                image = new { id = 2, name = q.Image },
                location = new { id = 3, name = q.Location },
            }),
            meta = Pagination(),
        });

    private static string ServerListJsonWithoutConfiguration(long id, RuntimeProvisionRequest request)
        => JsonSerializer.Serialize(new
        {
            servers = new[]
            {
                new
                {
                    id,
                    name = $"utf-{id}",
                    status = "running",
                    labels = Labels(request, HetznerRuntimeProvider.ServerResourceType),
                    public_net = new { ipv4 = new { ip = $"203.0.113.{id}" } },
                },
            },
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
                rules = ExpectedFirewallRules(q.Request),
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
                rules = ExpectedFirewallRules(q.Request),
            }),
            meta = Pagination(),
        });

    private static string FirewallListJsonWithRules(
        params (long Id, RuntimeProvisionRequest Request, long[] AppliedServerIds, FirewallRuleFixture[] Rules)[] values)
        => JsonSerializer.Serialize(new
        {
            firewalls = values.Select(q => new
            {
                id = q.Id,
                name = $"utf-{q.Id}",
                labels = Labels(q.Request, HetznerRuntimeProvider.FirewallResourceType),
                applied_to = q.AppliedServerIds.Select(id => new { type = "server", server = new { id } }),
                rules = q.Rules,
            }),
            meta = Pagination(),
        });

    private static string FirewallListJsonWithLabelsAndRules(
        params (long Id, IReadOnlyDictionary<string, string> Labels, long[] AppliedServerIds,
            FirewallRuleFixture[] Rules)[] values)
        => JsonSerializer.Serialize(new
        {
            firewalls = values.Select(q => new
            {
                id = q.Id,
                name = $"utf-{q.Id}",
                labels = q.Labels,
                applied_to = q.AppliedServerIds.Select(id => new { type = "server", server = new { id } }),
                rules = q.Rules,
            }),
            meta = Pagination(),
        });

    private static string FirewallListJsonWithoutRules(
        long id,
        RuntimeProvisionRequest request,
        params long[] appliedServerIds)
        => JsonSerializer.Serialize(new
        {
            firewalls = new[]
            {
                new
                {
                    id,
                    name = $"utf-{id}",
                    labels = Labels(request, HetznerRuntimeProvider.FirewallResourceType),
                    applied_to = appliedServerIds.Select(q => new { type = "server", server = new { id = q } }),
                },
            },
            meta = Pagination(),
        });

    private static object Pagination() => new { pagination = new { next_page = (int?)null } };

    private static Dictionary<string, string> Labels(RuntimeProvisionRequest request, string resourceType)
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [HetznerRuntimeProvider.ManagedLabel] = "true",
            [HetznerRuntimeProvider.ServerLabel] = request.ServerId.ToString("N"),
            [HetznerRuntimeProvider.DeploymentLabel] = request.DeploymentId.ToString("N"),
            [HetznerRuntimeProvider.IntentLabel] = request.ProvisioningIntentId.ToString("N"),
            [HetznerRuntimeProvider.WorkerLabel] = request.WorkerId.ToString("N"),
            [HetznerRuntimeProvider.ResourceKindLabel] = resourceType,
        };
        if (resourceType == HetznerRuntimeProvider.ServerResourceType)
        {
            labels[HetznerRuntimeProvider.ServerConfigurationFingerprintLabel] =
                HetznerRuntimeProvider.ServerConfigurationFingerprint(
                    RequestedServerType(request),
                    "ubuntu-24.04",
                    "fsn1");
        }
        else if (resourceType == HetznerRuntimeProvider.FirewallResourceType)
        {
            labels[HetznerRuntimeProvider.FirewallRulesFingerprintLabel] =
                HetznerRuntimeProvider.FirewallRulesFingerprint(request.InboundNetworkRequirements!);
        }
        return labels;
    }

    private static string RequestedServerType(RuntimeProvisionRequest request)
        => string.IsNullOrWhiteSpace(request.RuntimeProfileIdentifier) ? "ccx13" : request.RuntimeProfileIdentifier;

    private static FirewallRuleFixture[] ExpectedFirewallRules(RuntimeProvisionRequest request)
        => request.InboundNetworkRequirements!
            .Select(q => new FirewallRuleFixture(
                "in",
                q.Protocol is ERuntimeNetworkProtocol.Tcp ? "tcp" : "udp",
                q.StartPort == q.EndPort ? $"{q.StartPort}" : $"{q.StartPort}-{q.EndPort}",
                ["0.0.0.0/0"],
                [],
                q.Description))
            .ToArray();

    private sealed record FirewallRuleFixture(
        [property: JsonPropertyName("direction")] string Direction,
        [property: JsonPropertyName("protocol")] string Protocol,
        [property: JsonPropertyName("port")] string Port,
        [property: JsonPropertyName("source_ips")] IReadOnlyList<string> SourceIps,
        [property: JsonPropertyName("destination_ips")] IReadOnlyList<string> DestinationIps,
        [property: JsonPropertyName("description")] string Description);

    private sealed class TestOptionsProvider(HetznerRuntimeOptions value) : IHetznerRuntimeOptionsProvider
    {
        public HetznerRuntimeOptions Value { get; } = value;
    }

    private sealed class TestHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class AlwaysAuthorizedProvisioningMutationGuard : IProviderProvisioningMutationGuard
    {
        public async Task<ProviderProvisioningMutationGuardResult<T>> ExecuteCreateAsync<T>(
            Guid serverId,
            Guid deploymentId,
            Func<CancellationToken, Task<T>> mutation,
            CancellationToken cancellationToken = default)
            => new(true, await mutation(cancellationToken), null);
    }

    private sealed class BoundedProvisioningMutationGuard(int authorizedMutationCount)
        : IProviderProvisioningMutationGuard
    {
        public int Attempts { get; private set; }

        public async Task<ProviderProvisioningMutationGuardResult<T>> ExecuteCreateAsync<T>(
            Guid serverId,
            Guid deploymentId,
            Func<CancellationToken, Task<T>> mutation,
            CancellationToken cancellationToken = default)
        {
            Attempts++;
            if (Attempts > authorizedMutationCount)
            {
                return new ProviderProvisioningMutationGuardResult<T>(
                    false,
                    default,
                    "Provisioning is blocked by durable stop or termination state.");
            }

            return new ProviderProvisioningMutationGuardResult<T>(
                true,
                await mutation(cancellationToken),
                null);
        }
    }

    private sealed class SequencedHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> _responses = new();

        public List<ObservedRequest> Requests { get; } = [];

        public SequencedHttpMessageHandler Respond(
            HttpStatusCode statusCode,
            string json,
            string? requestId = null)
        {
            _responses.Enqueue(() =>
            {
                var response = new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                };
                if (requestId is not null)
                    response.Headers.Add("x-request-id", requestId);
                return response;
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

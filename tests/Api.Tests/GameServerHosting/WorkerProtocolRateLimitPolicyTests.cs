using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using X39.UnitedTacticalForces.Api.Controllers;
using X39.UnitedTacticalForces.Api.Services.WorkerProtocol;
using Xunit;

namespace Api.Tests.GameServerHosting;

public sealed class WorkerProtocolRateLimitPolicyTests
{
    [Fact]
    public void Configure_Uses429AndBothAnonymousWorkerControllersOptIntoPolicy()
    {
        var options = new RateLimiterOptions();

        WorkerProtocolRateLimitPolicy.Configure(options);

        Assert.Equal(StatusCodes.Status429TooManyRequests, options.RejectionStatusCode);
        AssertControllerUsesPolicy<LifecycleWorkersController>();
        AssertControllerUsesPolicy<LifecycleWorkerStorageController>();
    }

    [Fact]
    public void FixedWindow_IsPartitionedByNormalizedRemoteIpAndRejectsExcessTrafficWithoutQueueing()
    {
        var policy = new WorkerProtocolRateLimitPolicy();
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("::ffff:203.0.113.10");
        var partition = policy.GetPartition(context);
        var otherContext = new DefaultHttpContext();
        otherContext.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.11");
        var otherPartition = policy.GetPartition(otherContext);

        Assert.Equal("203.0.113.10", partition.PartitionKey);
        Assert.NotEqual(partition.PartitionKey, otherPartition.PartitionKey);
        using var limiter = partition.Factory(partition.PartitionKey);
        for (var request = 0; request < WorkerProtocolRateLimitPolicy.PermitLimit; request++)
        {
            using var lease = limiter.AttemptAcquire();
            Assert.True(lease.IsAcquired);
        }

        using var rejected = limiter.AttemptAcquire();
        Assert.False(rejected.IsAcquired);
    }

    [Fact]
    public void ApiPipeline_RegistersAndRunsRateLimitingBeforeControllers()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot, "sources/Api/Program.cs"));
        var forwardedHeadersIndex = source.IndexOf("app.UseForwardedHeaders();", StringComparison.Ordinal);
        var rateLimiterIndex = source.IndexOf("app.UseRateLimiter();", StringComparison.Ordinal);
        var controllersIndex = source.IndexOf("app.MapControllers();", StringComparison.Ordinal);

        Assert.Contains(
            "builder.Services.AddRateLimiter(WorkerProtocolRateLimitPolicy.Configure);",
            source,
            StringComparison.Ordinal);
        Assert.True(forwardedHeadersIndex >= 0 && forwardedHeadersIndex < rateLimiterIndex);
        Assert.True(rateLimiterIndex >= 0 && rateLimiterIndex < controllersIndex);
    }

    private static void AssertControllerUsesPolicy<TController>()
    {
        var attribute = Assert.Single(
            typeof(TController).GetCustomAttributes(typeof(EnableRateLimitingAttribute), inherit: true)
                .Cast<EnableRateLimitingAttribute>());
        Assert.Equal(WorkerProtocolRateLimitPolicy.Name, attribute.PolicyName);
    }

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null
                   && !File.Exists(Path.Combine(directory.FullName, "X39.UnitedTacticalForces.slnx")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName
                   ?? throw new InvalidOperationException("Could not find repository root.");
        }
    }
}

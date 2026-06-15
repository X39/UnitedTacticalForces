using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

/// <summary>
/// Bounds anonymous worker-protocol traffic by its forwarded remote IP before credential validation reaches the
/// database. The allowance covers normal heartbeat and command polling traffic with room for command reports.
/// </summary>
internal sealed class WorkerProtocolRateLimitPolicy : IRateLimiterPolicy<string>
{
    public const string Name = "worker-protocol-per-ip";
    internal const int PermitLimit = 240;
    internal static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => null;

    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        var remoteAddress = httpContext.Connection.RemoteIpAddress;
        if (remoteAddress?.IsIPv4MappedToIPv6 is true)
            remoteAddress = remoteAddress.MapToIPv4();
        var partitionKey = remoteAddress?.ToString() ?? "unknown-remote-ip";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            static _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = PermitLimit,
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                Window = Window,
            });
    }

    public static void Configure(RateLimiterOptions options)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy(Name, new WorkerProtocolRateLimitPolicy());
    }
}

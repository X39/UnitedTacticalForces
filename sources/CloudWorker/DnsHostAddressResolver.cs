using System.Net;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>Uses the operating-system DNS resolver for public repository validation.</summary>
internal sealed class DnsHostAddressResolver : IHostAddressResolver
{
    /// <inheritdoc />
    public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken)
        => Dns.GetHostAddressesAsync(host, cancellationToken);
}

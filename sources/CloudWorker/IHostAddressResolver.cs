using System.Net;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>Resolves repository hosts before an external Git process is started.</summary>
internal interface IHostAddressResolver
{
    /// <summary>Resolves all addresses currently advertised for a host.</summary>
    /// <param name="host">Validated DNS host name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Resolved addresses.</returns>
    Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken);
}

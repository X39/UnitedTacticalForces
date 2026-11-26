namespace X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

/// <summary>
/// Constants of the persistent worker channel that the control plane and the external worker must agree on.
/// </summary>
public static class WorkerChannelProtocol
{
    /// <summary>
    /// The worker protocol version that speaks the persistent channel.
    /// </summary>
    /// <remarks>
    /// The HTTP heartbeat, claim and report endpoints keep serving protocol versions 1 and 2 so worker images that
    /// are already running on provisioned machines keep working. Registration accepts versions 1 to 3.
    /// </remarks>
    public const int ProtocolVersion = 3;

    /// <summary>
    /// The last route segment of the channel endpoint, <c>lifecycle-workers/{workerId}/channel</c>.
    /// </summary>
    public const string ChannelPathSegment = "channel";

    /// <summary>
    /// The query parameter carrying the non-secret deployment id on the channel upgrade request. The credential
    /// travels only in the bearer header, never in the query string.
    /// </summary>
    public const string DeploymentIdQueryParameter = "deploymentId";

    /// <summary>
    /// The largest message, in bytes, a worker may send to the control plane. A larger message closes the channel
    /// with status 1009.
    /// </summary>
    public const int MaxWorkerMessageBytes = 262144;

    /// <summary>
    /// The largest message, in bytes, the control plane sends to a worker. It is larger than the worker limit because
    /// command payloads can carry sizeable configuration.
    /// </summary>
    public const int MaxControlPlaneMessageBytes = 4194304;
}

namespace X39.UnitedTacticalForces.Api.Services.ServerMetrics;

/// <summary>
/// Pushes the current readings of a server to the users who may view it.
/// </summary>
internal interface IServerMetricsPublisher
{
    /// <summary>
    /// Decides the current readings of a server and sends them to every user who may view it.
    /// </summary>
    /// <param name="gameServerId">The server whose readings are pushed.</param>
    /// <param name="cancellationToken">Cancels the publication.</param>
    /// <returns>A task completing once the frames were sent or their failure was logged.</returns>
    /// <remarks>
    /// The publisher never throws for an audience or send failure; a failed audience read sends nothing so no frame
    /// can reach a user who may not view the server.
    /// </remarks>
    Task PublishAsync(Guid gameServerId, CancellationToken cancellationToken);
}

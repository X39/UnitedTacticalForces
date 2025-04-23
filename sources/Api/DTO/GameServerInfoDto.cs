namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Class containing a virtual <see cref="GameServer"/> and corresponding information about
/// the physical instance of it.
/// </summary>
public record GameServerInfoDto
{
    /// <summary>The virtual server.</summary>
    public PlainGameServerDto GameServer { get; init; } = new();

    /// <summary>Whether the server is running or not.</summary>
    public bool IsRunning { get; init; }

    /// <summary>Whether the server can be started or not.</summary>
    public bool CanStart { get; init; }

    /// <summary>Whether the server can be stopped or not.</summary>
    public bool CanStop { get; init; }

    /// <summary>Whether the server can change the configuration or not.</summary>
    public bool CanUpdateConfiguration { get; init; }

    /// <summary>Whether the server can be upgraded or not.</summary>
    public bool CanUpgrade { get; init; }
}

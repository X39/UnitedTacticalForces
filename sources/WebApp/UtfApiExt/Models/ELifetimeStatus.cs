// ReSharper disable once CheckNamespace
namespace X39.UnitedTacticalForces.WebApp.Api.Models;

public partial class ELifetimeStatus
{
    /// <summary>
    /// Status indicating that something is currently not running in any way.
    /// </summary>
    public static ELifetimeStatus Stopped => new(){Integer = 0};

    /// <summary>
    /// Status indicating a transition from <see cref="Stopped"/> to <see cref="Running"/>.
    /// </summary>
    /// <remarks>
    /// Implies that a lifetime change was requested.
    /// </remarks>
    public static ELifetimeStatus Starting => new(){Integer = 1};

    /// <summary>
    /// Status indicating a transition from <see cref="Running"/> to <see cref="Stopped"/>.
    /// </summary>
    /// <remarks>
    /// Implies that a lifetime change was requested.
    /// </remarks>
    public static ELifetimeStatus Stopping => new(){Integer = 2};

    /// <summary>
    /// Status indicating that something is currently running.
    /// </summary>
    public static ELifetimeStatus Running => new(){Integer = 3};
        
    /// <summary>
    /// Status that indicates a transient, update state where the game server is generally unavailable.
    /// </summary>
    public static ELifetimeStatus Updating => new(){Integer = 4};
}

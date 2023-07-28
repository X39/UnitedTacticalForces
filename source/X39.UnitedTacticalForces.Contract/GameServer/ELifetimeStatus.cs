namespace X39.UnitedTacticalForces.Contract.GameServer
{
    public enum ELifetimeStatus
    {
        /// <summary>
        /// Status indicating that something is currently not running in any way.
        /// </summary>
        Stopped,

        /// <summary>
        /// Status indicating a transition from <see cref="Stopped"/> to <see cref="Running"/>.
        /// </summary>
        /// <remarks>
        /// Implies that a lifetime change was requested.
        /// </remarks>
        Starting,

        /// <summary>
        /// Status indicating a transition from <see cref="Running"/> to <see cref="Stopped"/>.
        /// </summary>
        /// <remarks>
        /// Implies that a lifetime change was requested.
        /// </remarks>
        Stopping,

        /// <summary>
        /// Status indicating that something is currently running.
        /// </summary>
        Running,
        
        /// <summary>
        /// Status that indicates a transient, update state where the game server is generally unavailable.
        /// </summary>
        Updating,
    }
}
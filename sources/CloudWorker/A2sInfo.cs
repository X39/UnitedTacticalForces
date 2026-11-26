namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>The player figures of a Source-style A2S_INFO reply.</summary>
/// <param name="Players">The total player slots in use, which Source servers count including bots.</param>
/// <param name="MaxPlayers">The maximum player capacity.</param>
/// <param name="Bots">The number of bots among <paramref name="Players" />.</param>
public sealed record A2sInfo(int Players, int MaxPlayers, int Bots)
{
    /// <summary>Gets the number of human players, which is the players minus the bots and never negative.</summary>
    /// <value>The non-negative human player count.</value>
    public int Humans => Math.Max(0, Players - Bots);
}

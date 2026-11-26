namespace X39.UnitedTacticalForces.Api.Data;

/// <summary>
/// The player feed an emulated UAT worker reports in its metrics frames, so a UAT scenario can choose whether its
/// server looks empty, lightly used or like the metrics showcase.
/// </summary>
internal enum EUatPlayerProfile
{
    /// <summary>
    /// The smooth synthetic series of <see cref="UatMetricHistorySeeder"/>, which continues the seeded history.
    /// </summary>
    Synthetic = 0,

    /// <summary>The server always reports zero connected players.</summary>
    NoPlayers = 1,

    /// <summary>The server always reports a fixed small number of connected players.</summary>
    FewPlayers = 2,
}

namespace X39.UnitedTacticalForces.GameServerImplementations;

/// <summary>
/// Classifies how prominently a game configuration option is presented. Each game definition decides its own set
/// of <see cref="Common" /> options; an option without a declaration is <see cref="Advanced" />.
/// </summary>
public enum EGameConfigurationTier
{
    /// <summary>
    /// The default for options without a declaration. The UI shows these options behind an Advanced disclosure.
    /// </summary>
    Advanced = 0,

    /// <summary>
    /// A small, everyday option that the UI shows before the Advanced options.
    /// </summary>
    Common = 1,
}

namespace X39.UnitedTacticalForces.Common.Event;

/// <summary>
/// Identifies the semantic symbol used to present an event slot role.
/// </summary>
/// <remarks>
/// Values are persisted as integers. Existing numeric assignments must remain stable and new values must be appended.
/// </remarks>
public enum EEventSlotRoleSymbol
{
    /// <summary>
    /// Derives the symbol from the role title for compatibility with existing slots.
    /// </summary>
    Automatic = 0,

    /// <summary>
    /// Uses a generic personnel-slot symbol.
    /// </summary>
    Generic = 1,

    /// <summary>
    /// Uses the rifleman symbol.
    /// </summary>
    Rifleman = 2,

    /// <summary>
    /// Uses the automatic-rifleman symbol.
    /// </summary>
    AutomaticRifleman = 3,

    /// <summary>
    /// Uses the grenadier symbol.
    /// </summary>
    Grenadier = 4,

    /// <summary>
    /// Uses the anti-armor symbol.
    /// </summary>
    AntiArmor = 5,

    /// <summary>
    /// Uses the medic symbol.
    /// </summary>
    Medic = 6,

    /// <summary>
    /// Uses the radio-operator symbol.
    /// </summary>
    RadioOperator = 7,

    /// <summary>
    /// Uses the leadership symbol.
    /// </summary>
    Leader = 8,

    /// <summary>
    /// Uses the marksman or reconnaissance symbol.
    /// </summary>
    Marksman = 9,

    /// <summary>
    /// Uses the engineer symbol.
    /// </summary>
    Engineer = 10,

    /// <summary>
    /// Uses the vehicle-crew symbol.
    /// </summary>
    Crew = 11,

    /// <summary>
    /// Uses the pilot symbol.
    /// </summary>
    Pilot = 12,

    /// <summary>
    /// Uses the support-role symbol.
    /// </summary>
    Support = 13,
}

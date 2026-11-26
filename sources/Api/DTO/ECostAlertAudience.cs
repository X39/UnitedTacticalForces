namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Tells who the alerts of a cost owner are addressed to.
/// </summary>
public enum ECostAlertAudience
{
    /// <summary>
    /// The alerts go to the single user behind a personal cost owner.
    /// </summary>
    Self,

    /// <summary>
    /// The alerts go to the people who may use a Verein or external organization cost owner.
    /// </summary>
    Group,
}

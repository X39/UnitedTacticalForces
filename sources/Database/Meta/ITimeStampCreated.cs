namespace X39.UnitedTacticalForces.Database.Meta;

/// <summary>
/// Meta interface to express that a class has a timestamp when it was created.
/// </summary>
public interface ITimeStampCreated
{
    /// <summary>
    /// The timestamp when this entity was created.
    /// </summary>
    public Instant TimeStampCreated { get; set; }
}

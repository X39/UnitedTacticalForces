namespace X39.UnitedTacticalForces.Api.Data.Meta;

/// <summary>
/// Meta interface to express that a class has a primary key.
/// </summary>
public interface IPrimaryKey<T>
{
    /// <summary>
    /// The primary key of the entity.
    /// </summary>
    public T PrimaryKey { get; set; }
}
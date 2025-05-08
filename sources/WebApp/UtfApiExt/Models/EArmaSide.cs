// ReSharper disable once CheckNamespace
namespace X39.UnitedTacticalForces.WebApp.Api.Models;

public partial class EArmaSide
{
    /// <summary>
    /// Enum value for <see href="https://community.bistudio.com/wiki/sideEmpty"/>
    /// </summary>
    public static EArmaSide Empty => new(){Integer = 0};
    /// <summary>
    /// Enum value for <see href="https://community.bistudio.com/wiki/east"/>
    /// </summary>
    public static EArmaSide East => new(){Integer = 1};
    /// <summary>
    /// Enum value for <see href="https://community.bistudio.com/wiki/west"/>
    /// </summary>
    public static EArmaSide West => new(){Integer = 2};
    /// <summary>
    /// Enum value for <see href="https://community.bistudio.com/wiki/resistance"/>
    /// </summary>
    public static EArmaSide Resistance => new(){Integer = 3};
    /// <summary>
    /// Enum value for <see href="https://community.bistudio.com/wiki/civilian"/>
    /// </summary>
    public static EArmaSide Civilian => new(){Integer = 4};
    /// <summary>
    /// Enum value for <see href="https://community.bistudio.com/wiki/sideAmbientLife"/>
    /// </summary>
    public static EArmaSide AmbientLife => new(){Integer = 5};
    /// <summary>
    /// Enum value for <see href="https://community.bistudio.com/wiki/sideLogic"/>
    /// </summary>
    public static EArmaSide Logic => new(){Integer = 6};
}

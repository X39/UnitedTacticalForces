namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

/// <summary>
/// Provider-neutral classification of whether an attributed resource can safely participate in a recovered
/// runtime graph.
/// </summary>
public enum ERuntimeResourceProvisioningReadiness
{
    /// <summary>The resource and its required provider relationships are verified ready.</summary>
    Ready,

    /// <summary>
    /// The relationship is valid but still converging and must be observed again without reposting create.
    /// </summary>
    Pending,

    /// <summary>The relationship is conclusively unsafe or points at a conflicting provider resource.</summary>
    Conflict,
}
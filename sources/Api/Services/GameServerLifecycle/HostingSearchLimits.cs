namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// The shared bounds of the bounded searches of the hosting API, so the controller and the services that back it can
/// never disagree about how many results one request may ask for.
/// </summary>
internal static class HostingSearchLimits
{
    /// <summary>
    /// The largest <c>take</c> a single candidate or cost owner option search may request. The smallest is 1.
    /// </summary>
    /// <remarks>
    /// The literal value is repeated in the <c>take</c> documentation of the public contracts and the controller,
    /// because a reference to this internal member does not resolve there and breaks the OpenAPI snapshot. A test
    /// pins the value, so changing it fails loudly until those descriptions are updated.
    /// </remarks>
    public const int MaximumTake = 50;
}

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

/// <summary>Provider-neutral classification of an allocated runtime's observed operational state.</summary>
public enum ERuntimeResourceOperationalState
{
    /// <summary>The provider state does not conclusively prove either normal operation or a fault.</summary>
    Uncertain,

    /// <summary>The provider conclusively reports normal runtime operation.</summary>
    Operational,

    /// <summary>The provider conclusively reports an operational fault that requires safe teardown.</summary>
    Faulted,
}

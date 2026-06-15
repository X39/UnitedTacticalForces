namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

public interface IPreservationSelectionResolver
{
    PreservationSelectionMatch? Resolve(
        string relativePath,
        bool isDirectory,
        IReadOnlyCollection<PreservationPathSelection> selections);
}
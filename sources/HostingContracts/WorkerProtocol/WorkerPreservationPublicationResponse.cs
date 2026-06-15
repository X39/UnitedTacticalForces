namespace X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

public sealed record WorkerPreservationPublicationResponse
{
    public int CopiedFileCount { get; init; }

    public int DeletedFileCount { get; init; }
}
namespace X39.UnitedTacticalForces.Api.DTO.Payloads;

public sealed record GameServerContentSelectionPayload(
    long? TerrainId,
    Guid? TerrainVersionId,
    Guid? ScenarioId,
    Guid? ScenarioVersionId);

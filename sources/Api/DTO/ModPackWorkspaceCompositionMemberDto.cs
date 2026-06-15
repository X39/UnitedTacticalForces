namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents one linked revision inside a composition mod pack workspace.
/// </summary>
public record ModPackWorkspaceCompositionMemberDto
{
    /// <summary>
    /// The standalone mod pack definition that owns the linked revision.
    /// </summary>
    public PlainModPackDefinitionDto Definition { get; init; } = new();

    /// <summary>
    /// The owner of the standalone mod pack definition.
    /// </summary>
    public PlainUserDto? Owner { get; init; }

    /// <summary>
    /// The linked revision.
    /// </summary>
    public PlainModPackRevisionDto Revision { get; init; } = new();
}

namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Contains a filtered page of hosting-finance protocol entries.
/// </summary>
public sealed record HostingFinanceProtocolPageDto
{
    /// <summary>Gets the matching protocol entries.</summary>
    public HostingFinanceProtocolEntryDto[] Entries { get; init; } = [];
    /// <summary>Gets the total matching entry count.</summary>
    public long TotalCount { get; init; }
}

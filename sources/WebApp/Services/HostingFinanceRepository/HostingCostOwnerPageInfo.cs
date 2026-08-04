namespace X39.UnitedTacticalForces.WebApp.Services.HostingFinanceRepository;

public sealed record HostingCostOwnerPageInfo
{
    public HostingCostOwnerInfo[] Owners { get; init; } = [];
    public long TotalCount { get; init; }
}

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal sealed record HetznerFirewall(
    long Id,
    string Name,
    int AppliedToCount,
    IReadOnlyList<long> AppliedServerIds,
    IReadOnlyList<HetznerFirewallRule> Rules,
    bool HasRuleConfiguration,
    IReadOnlyDictionary<string, string> Labels);

internal sealed record HetznerFirewallRule(
    string Direction,
    string Protocol,
    string Port,
    IReadOnlyList<string> SourceIps,
    IReadOnlyList<string> DestinationIps,
    string Description);

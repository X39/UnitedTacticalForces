namespace X39.UnitedTacticalForces.Api.Services.Media;

/// <summary>
/// Query filter for reusable media assets.
/// </summary>
/// <param name="Search">Optional search text for filename, content type, and tags.</param>
/// <param name="IncludeTags">Tags that must be present.</param>
/// <param name="ExcludeTags">Tags that must not be present.</param>
/// <param name="IncludeUsageStatus">Optional usage status that must match.</param>
/// <param name="ExcludeUsageStatus">Optional usage status that must not match.</param>
/// <param name="IncludeUsageDomains">Usage domains that must be present.</param>
/// <param name="ExcludeUsageDomains">Usage domains that must not be present.</param>
/// <param name="Skip">Number of rows to skip after filtering.</param>
/// <param name="Take">Maximum number of rows to return.</param>
public sealed record MediaAssetFilter(
    string? Search,
    string[] IncludeTags,
    string[] ExcludeTags,
    string? IncludeUsageStatus,
    string? ExcludeUsageStatus,
    string[] IncludeUsageDomains,
    string[] ExcludeUsageDomains,
    int Skip,
    int Take);

using System.Net;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services.HostingFinanceRepository;

[Scoped<HostingFinanceRepositoryImpl, IHostingFinanceRepository>]
internal sealed class HostingFinanceRepositoryImpl(IHttpClientFactory httpClientFactory, BaseUrl baseUrl)
    : RepositoryBase(httpClientFactory, baseUrl), IHostingFinanceRepository
{
    public async Task<HostingCostOwnerPageInfo> GetCostOwnersAsync(
        string? search,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = $"cost-owners?skip={Math.Max(0, skip)}&take={Math.Clamp(take, 1, 50)}";
        if (!string.IsNullOrWhiteSpace(search))
            query += $"&search={Uri.EscapeDataString(search.Trim())}";
        return await GetFromJsonAsync<HostingCostOwnerPageInfo>(query, cancellationToken).ConfigureAwait(false)
               ?? new HostingCostOwnerPageInfo();
    }

    public async Task<HostingCostOwnerInfo?> GetUserCostOwnerAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.GetAsync(
                BaseUrl.ResolveApiUrl($"cost-owners/users/{userId}"), cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await ReadFromJsonAsync<HostingCostOwnerInfo>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<HostingCostOwnerInfo> EnsureUserCostOwnerAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsync(
                BaseUrl.ResolveApiUrl($"cost-owners/users/{userId}"), null, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<HostingCostOwnerInfo>(response, "Cost-owner creation", cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<HostingCostOwnerInfo> RecordPaymentAsync(
        Guid costOwnerId,
        decimal amount,
        DateTimeOffset effectiveAt,
        string reference,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var response = await PostAsJsonAsync(
                $"cost-owners/{costOwnerId}/payments",
                new
                {
                    RecordIdentifier = Guid.NewGuid(),
                    Amount = amount,
                    Currency = "EUR",
                    TimeStampEffective = effectiveAt,
                    ExternalReference = reference,
                    Reason = reason,
                },
                cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await GetCostOwnerAsync(costOwnerId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<HostingCostOwnerInfo> ChangeAllowanceAsync(
        Guid costOwnerId,
        decimal? allowance,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var response = await PutAsJsonAsync(
                $"cost-owners/{costOwnerId}/allowance",
                new { RecordIdentifier = Guid.NewGuid(), Allowance = allowance, Reason = reason },
                cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await GetCostOwnerAsync(costOwnerId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<HostingFinancePolicyInfo> GetPolicyAsync(CancellationToken cancellationToken = default)
        => await GetFromJsonAsync<HostingFinancePolicyInfo>("cost-owners/policy", cancellationToken)
            .ConfigureAwait(false)
           ?? throw new InvalidOperationException("Hosting finance policy returned no response body.");

    public async Task<HostingFinancePolicyInfo> UpdatePolicyAsync(
        decimal minimumSafetyRangeEur,
        int reservationSliceMinutes,
        CancellationToken cancellationToken = default)
    {
        var response = await PutAsJsonAsync(
                "cost-owners/policy",
                new { MinimumSafetyRangeEur = minimumSafetyRangeEur, ReservationSliceMinutes = reservationSliceMinutes },
                cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<HostingFinancePolicyInfo>(response, "Finance policy update", cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<HostingCostOwnerInfo> GetCostOwnerAsync(
        Guid costOwnerId,
        CancellationToken cancellationToken)
    {
        var response = await HttpClient.GetAsync(
                BaseUrl.ResolveApiUrl($"cost-owners/{costOwnerId}"), cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<HostingCostOwnerInfo>(response, "Cost-owner read", cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<T> ReadRequiredAsync<T>(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
        => await ReadFromJsonAsync<T>(response, cancellationToken).ConfigureAwait(false)
           ?? throw new InvalidOperationException($"{operation} returned no response body.");
}

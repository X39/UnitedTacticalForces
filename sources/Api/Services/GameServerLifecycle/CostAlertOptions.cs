namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Settings of cost alert evaluation and delivery, bound from the <c>Hosting:CostAlerts</c> section.
/// </summary>
public sealed class CostAlertOptions
{
    /// <summary>
    /// The configuration section these options are bound from.
    /// </summary>
    public const string SectionName = "Hosting:CostAlerts";

    /// <summary>
    /// Gets or sets how often the control plane evaluates thresholds against accrued cost.
    /// </summary>
    public TimeSpan EvaluationInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets how long after resolution a deployment is still evaluated, so a final amount that reaches a
    /// threshold is alerted even when the last evaluation pass ran before resolution.
    /// </summary>
    public TimeSpan ResolvedLookback { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Gets or sets the maximum number of thresholds one cost owner may hold.
    /// </summary>
    public int MaxThresholds { get; set; } = 10;

    /// <summary>
    /// Gets or sets the largest threshold amount that may be configured.
    /// </summary>
    public decimal MaxThresholdAmount { get; set; } = 10000m;

    /// <summary>
    /// Gets or sets how often the dispatcher polls for deliveries that are due.
    /// </summary>
    public TimeSpan DispatchInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the maximum number of deliveries the dispatcher claims per pass.
    /// </summary>
    public int DeliveryBatchSize { get; set; } = 20;

    /// <summary>
    /// Gets or sets the maximum number of attempts per delivery before it is marked failed.
    /// </summary>
    public int MaxDeliveryAttempts { get; set; } = 5;

    /// <summary>
    /// Gets or sets the delays before the second and each later attempt. The last entry applies to every attempt
    /// beyond the list.
    /// </summary>
    public TimeSpan[] DeliveryRetryDelays { get; set; } =
    [
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromHours(1),
    ];

    /// <summary>
    /// Gets or sets how long a dispatcher holds the lease on a delivery before another instance may take it over.
    /// </summary>
    public TimeSpan DeliveryLease { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Gets or sets the composite format string of the in-app path linked from alert messages. The placeholder
    /// <c>{0}</c> receives the server id.
    /// </summary>
    public string ServerLinkPathTemplate { get; set; } = "next/command/servers/{0}";

    /// <summary>
    /// Checks the options for unusable values.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// An interval or the lease is not positive, the lookback is shorter than the evaluation interval, a count or
    /// amount limit is out of range, the retry delay list is empty or contains a non-positive entry, or the link
    /// template has no <c>{0}</c> placeholder.
    /// </exception>
    public void Validate()
    {
        if (EvaluationInterval <= TimeSpan.Zero
            || DispatchInterval <= TimeSpan.Zero
            || DeliveryLease <= TimeSpan.Zero
            || ResolvedLookback < EvaluationInterval
            || MaxThresholds is < 1 or > 50
            || MaxThresholdAmount <= 0m
            || DeliveryBatchSize is < 1 or > 100
            || MaxDeliveryAttempts is < 1 or > 20
            || DeliveryRetryDelays is null
            || DeliveryRetryDelays.Length == 0
            || DeliveryRetryDelays.Any(q => q <= TimeSpan.Zero)
            || string.IsNullOrEmpty(ServerLinkPathTemplate)
            || !ServerLinkPathTemplate.Contains("{0}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Hosting:CostAlerts contains invalid settings.");
        }
    }
}

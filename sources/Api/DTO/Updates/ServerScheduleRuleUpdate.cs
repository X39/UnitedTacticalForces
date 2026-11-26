using System.ComponentModel.DataAnnotations;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.DTO.Updates;

/// <summary>
/// One weekly schedule rule inside a <see cref="ServerAutomationUpdate" />.
/// </summary>
public sealed record ServerScheduleRuleUpdate
{
    /// <summary>
    /// Gets the identifier of an existing rule.
    /// </summary>
    /// <value>
    /// The identifier the API assigned to the rule, or <see langword="null" /> for a new rule. An identifier that does
    /// not belong to the server is rejected.
    /// </value>
    public Guid? Id { get; init; }

    /// <summary>
    /// Gets the action the rule performs.
    /// </summary>
    /// <value>Start or stop.</value>
    public EServerScheduleAction Action { get; init; }

    /// <summary>
    /// Gets the weekdays the rule applies to.
    /// </summary>
    /// <value>
    /// A non-empty set of distinct ISO weekdays, Monday is 1 and Sunday is 7. The API returns them in ascending order.
    /// </value>
    public IReadOnlyList<int> Days { get; init; } = [];

    /// <summary>
    /// Gets the local time of day in the schedule time zone.
    /// </summary>
    /// <value>The time formatted as <c>HH:mm</c>, from <c>00:00</c> to <c>23:59</c>.</value>
    [MaxLength(5)]
    public string Time { get; init; } = string.Empty;
}

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Reports that a payload was rejected and names every field that failed validation, so a client can attach each
/// error to its input.
/// </summary>
/// <param name="message">The reason the payload was rejected.</param>
/// <param name="fields">The paths of the failing fields, such as <c>schedules[1].time</c>.</param>
public sealed class HostingFieldValidationException(string message, IReadOnlyList<string> fields)
    : ArgumentException(message)
{
    /// <summary>
    /// Gets the paths of the fields that failed validation.
    /// </summary>
    /// <value>The failing field paths in the order they were detected, without duplicates.</value>
    public IReadOnlyList<string> Fields { get; } = fields ?? throw new ArgumentNullException(nameof(fields));
}

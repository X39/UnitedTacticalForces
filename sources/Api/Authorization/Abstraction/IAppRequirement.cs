using Microsoft.AspNetCore.Authorization;

namespace X39.UnitedTacticalForces.Api.Authorization.Abstraction;

/// <summary>
/// Denotes a requirement for the app authorization system.
/// </summary>
public interface IAppRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// The name of the policy to register this requirement to.
    /// </summary>
    public string PolicyName { get; }

    /// <summary>
    /// Method to check if the requirement is satisfied.
    /// </summary>
    /// <param name="httpContext">The http context to check.</param>
    /// <param name="serviceProvider"></param>
    /// <returns><see langword="true"/> if the requirement is satisfied, otherwise <see langword="false"/>.</returns>
    public ValueTask<bool> IsSatisfiedAsync(
        HttpContext httpContext,
        IServiceProvider serviceProvider);
}
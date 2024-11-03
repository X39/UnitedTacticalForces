using System.Reflection;
using Discord.Commands;
using Microsoft.AspNetCore.Authorization;
using X39.Util;

namespace X39.UnitedTacticalForces.Api.Authorization.Abstraction;

/// <summary>
/// Contains service collection extensions for the authorization system.
/// </summary>
public static class ServiceCollectionExtension
{
    public static IServiceCollection AddAppAuthorization(this IServiceCollection self)
    {
        self.AddAuthorization(
            (options) =>
            {
                var assembly = typeof(Program).Assembly;
                var appRequirementTypes = assembly.GetTypes()
                    .Where((q) => !q.IsAbstract)
                    .Where((q) => q.IsAssignableTo(typeof(IAppRequirement)));
                foreach (var appRequirementType in appRequirementTypes)
                {
                    var instance = appRequirementType.CreateInstance<IAppRequirement>();
                    options.AddPolicy(
                        instance.PolicyName,
                        (policy) => policy
                            .RequireAuthenticatedUser()
                            .AddRequirements(instance));
                }
            });
        self.AddTransient<IAuthorizationHandler, AppAuthorizationHandler>();
        return self;
    }
}
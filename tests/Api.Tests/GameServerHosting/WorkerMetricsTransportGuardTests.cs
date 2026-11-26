using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using X39.UnitedTacticalForces.Api.Controllers;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// MET-05 and D-03 proof that metrics reach the control plane only as <c>Metrics</c> frames on the authenticated
/// worker channel: no HTTP controller action accepts a metrics sample, and the worker HTTP controller has no metrics
/// route.
/// </summary>
public sealed class WorkerMetricsTransportGuardTests
{
    /// <summary>
    /// Finds the public actions declared by the controllers of the API assembly.
    /// </summary>
    /// <returns>Every public instance method declared by a concrete controller type.</returns>
    private static IReadOnlyList<MethodInfo> ControllerActions()
        => typeof(GameServersController).Assembly
            .GetTypes()
            .Where(q => typeof(ControllerBase).IsAssignableFrom(q) && !q.IsAbstract)
            .SelectMany(q => q.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(q => !q.IsSpecialName)
            .ToList();

    /// <summary>
    /// No public controller action takes a <see cref="WorkerMetricsSample"/> as a parameter, directly or wrapped.
    /// </summary>
    [Fact]
    public void NoControllerActionAcceptsAMetricsSample()
    {
        var actions = ControllerActions();

        Assert.NotEmpty(actions);
        Assert.DoesNotContain(
            actions,
            action => action.GetParameters().Any(q => IsMetricsSampleType(q.ParameterType)));
    }

    /// <summary>
    /// No route of the worker HTTP controller contains a metrics segment.
    /// </summary>
    [Fact]
    public void WorkerHttpControllerHasNoMetricsRoute()
    {
        var templates = typeof(LifecycleWorkersController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(q => q.GetCustomAttributes<HttpMethodAttribute>())
            .Select(q => q.Template ?? string.Empty)
            .ToList();
        var routePrefix = typeof(LifecycleWorkersController).GetCustomAttribute<RouteAttribute>()?.Template
                          ?? string.Empty;

        Assert.NotEmpty(templates);
        Assert.DoesNotContain(templates, q => q.Contains("metrics", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("metrics", routePrefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The only HTTP routes that mention metrics are the two read routes of the game server controller, and both are
    /// GET routes.
    /// </summary>
    [Fact]
    public void OnlyTheTwoReadRoutesMentionMetrics()
    {
        var metricsRoutes = ControllerActions()
            .SelectMany(q => q.GetCustomAttributes<HttpMethodAttribute>().Select(a => (Action: q, Attribute: a)))
            .Where(q => (q.Attribute.Template ?? string.Empty).Contains("metrics", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Equal(2, metricsRoutes.Count);
        Assert.All(metricsRoutes, q => Assert.Equal(typeof(GameServersController), q.Action.DeclaringType));
        Assert.All(metricsRoutes, q => Assert.Contains("GET", q.Attribute.HttpMethods));
    }

    /// <summary>
    /// Tells whether a parameter type is, or wraps, the worker metrics sample.
    /// </summary>
    /// <param name="type">The parameter type to inspect.</param>
    /// <returns><see langword="true"/> when the type is the sample or a generic or array wrapper around it.</returns>
    private static bool IsMetricsSampleType(Type type)
        => type == typeof(WorkerMetricsSample)
           || (type.IsGenericType && type.GetGenericArguments().Any(IsMetricsSampleType))
           || (type.IsArray && type.GetElementType() is { } element && IsMetricsSampleType(element));
}

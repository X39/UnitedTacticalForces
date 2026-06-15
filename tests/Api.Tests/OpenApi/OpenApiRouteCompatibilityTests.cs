using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using X39.UnitedTacticalForces.Api.Controllers;
using X39.UnitedTacticalForces.Api.DTO;
using Xunit;

namespace Api.Tests.OpenApi;

public class OpenApiRouteCompatibilityTests
{
    private static readonly Regex RouteParameterRegex = new(@"\{([^{}]+)\}", RegexOptions.Compiled);

    [Fact]
    public void ControllerRoutes_DoNotExposeAmbiguousTemplatedPathSignatures()
    {
        var routes = typeof(FieldReportsController).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false } && typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(GetOpenApiRoutes)
            .ToArray();

        var collisions = routes
            .GroupBy(route => route.Signature, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                Signature = group.Key,
                Paths     = group
                    .Select(route => route.OpenApiPath)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                Routes    = group.ToArray(),
            })
            .Where(group => group.Paths.Length > 1)
            .Select(group =>
            {
                var routesText = string.Join(
                    ", ",
                    group.Routes.Select(route => $"{route.HttpMethod} {route.OpenApiPath} ({route.Action})")
                );
                return $"{group.Signature}: {routesText}";
            })
            .ToArray();

        Assert.True(
            collisions.Length is 0,
            "OpenAPI path templates must remain unique after parameter names are removed. Collisions: "
            + string.Join("; ", collisions)
        );
    }

    [Fact]
    public void PlainEventSlotDto_ExposesKiotaSupportedSlotNumberType()
    {
        var slotNumberProperty = typeof(PlainEventSlotDto).GetProperty(nameof(PlainEventSlotDto.SlotNumber));

        Assert.Equal(typeof(int), slotNumberProperty?.PropertyType);
    }

    private static IEnumerable<RouteInfo> GetOpenApiRoutes(Type controllerType)
    {
        var controllerRouteTemplates = controllerType
            .GetCustomAttributes<RouteAttribute>(inherit: true)
            .Select(attribute => attribute.Template)
            .DefaultIfEmpty(string.Empty);

        var actionRoutes = controllerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .SelectMany(method => method
                .GetCustomAttributes<HttpMethodAttribute>(inherit: true)
                .Select(attribute => (method, attribute))
            );

        foreach (var controllerRouteTemplate in controllerRouteTemplates)
        {
            foreach (var (method, attribute) in actionRoutes)
            {
                var combinedRoute = CombineRoutes(controllerRouteTemplate, attribute.Template);
                var openApiPath = "/" + RouteParameterRegex
                    .Replace(combinedRoute, StripRouteConstraint)
                    .Trim('/');
                var signature = RouteParameterRegex.Replace(openApiPath, "{}");
                foreach (var httpMethod in attribute.HttpMethods)
                {
                    yield return new RouteInfo(
                        httpMethod,
                        openApiPath,
                        signature,
                        $"{controllerType.Name}.{method.Name}"
                    );
                }
            }
        }
    }

    private static string CombineRoutes(string? controllerRouteTemplate, string? actionRouteTemplate)
    {
        if (string.IsNullOrWhiteSpace(actionRouteTemplate))
            return controllerRouteTemplate?.Trim('/') ?? string.Empty;
        if (actionRouteTemplate.StartsWith("~/", StringComparison.Ordinal))
            return actionRouteTemplate[2..].Trim('/');
        if (actionRouteTemplate.StartsWith("/", StringComparison.Ordinal))
            return actionRouteTemplate.Trim('/');
        if (string.IsNullOrWhiteSpace(controllerRouteTemplate))
            return actionRouteTemplate.Trim('/');

        return $"{controllerRouteTemplate.TrimEnd('/')}/{actionRouteTemplate.TrimStart('/')}";
    }

    private static string StripRouteConstraint(Match match)
    {
        var parameter = match.Groups[1].Value.TrimStart('*');
        var constraintStart = parameter.IndexOfAny([':', '=', '?']);
        var name = constraintStart < 0
            ? parameter
            : parameter[..constraintStart];
        return "{" + name + "}";
    }

    private sealed record RouteInfo(
        string HttpMethod,
        string OpenApiPath,
        string Signature,
        string Action);
}

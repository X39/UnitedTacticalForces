using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace X39.UnitedTacticalForces.Api.OpenApi;

/// <summary>
/// Keeps operation identifiers unique when one controller action accepts more than one HTTP method.
/// </summary>
internal sealed class UniqueOperationIdTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (operation.OperationId is "LoginSteamAsync" or "LoginDiscordAsync")
        {
            var httpMethod = context.Description.HttpMethod?.ToUpperInvariant()
                             ?? throw new InvalidOperationException("The OpenAPI operation has no HTTP method.");
            operation.OperationId = $"{operation.OperationId}_{httpMethod}";
        }

        return Task.CompletedTask;
    }
}

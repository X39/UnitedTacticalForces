using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace X39.UnitedTacticalForces.Api.OpenApi;

/// <summary>
/// Represents an operation transformer that tags an OpenAPI operation with metadata based on
/// the presence of anonymous access markers (e.g., <see cref="AllowAnonymousAttribute"/>
/// or <see cref="IAllowAnonymous"/>).
/// </summary>
/// <remarks>
/// This transformer is intended for modifying OpenAPI operation metadata to include a custom
/// annotation ("x-anonymous") that indicates whether the operation allows anonymous access.
/// Additionally, it overrides the default security requirements for anonymous operations.
/// </remarks>
internal sealed class XAnonymousAnnotationOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;

        // Handle both attribute- and endpoint-metadata-based anonymous markers
        var isAnonymous = metadata.OfType<AllowAnonymousAttribute>()
                              .Any()
                          || metadata.OfType<IAllowAnonymous>()
                              .Any();
        operation.Metadata ??= new Dictionary<string, object>();

        if (isAnonymous)
        {
            operation.Metadata["x-anonymous"] = true;
            // Override document-level security: this operation does not require auth
            operation.Security = new List<OpenApiSecurityRequirement>();
        }
        else
        {
            operation.Metadata["x-anonymous"] = false;
        }

        operation.Metadata["x-comment"] = operation.Metadata.TryGetValue("x-comment", out var value) is false
            ? "Applied XAnonymousAnnotationOperationTransformer"
            : string.Join(Environment.NewLine, value, "Applied XAnonymousAnnotationOperationTransformer");
        return Task.CompletedTask;
    }
}

using System.Collections.Immutable;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace X39.UnitedTacticalForces.Api.OpenApi;

public sealed class KiotaFixNullableNumberTypeAnnotation : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        if (schema.Properties is null)
            return Task.CompletedTask;
        foreach (var (key, openApiSchemaCandidate) in schema.Properties.ToImmutableList())
        {
            if (openApiSchemaCandidate is not OpenApiSchema { Type: not null } openApiSchema)
                continue;
            if (openApiSchema.Type.Value.HasFlag(JsonSchemaType.Null) is false)
                continue;
            schema.Properties[key] = new OpenApiSchema
            {
                OneOf = new List<IOpenApiSchema> { new OpenApiSchema { Type = JsonSchemaType.Null }, openApiSchema},
            };
            openApiSchema.Type &= ~JsonSchemaType.Null;
        }

        return Task.CompletedTask;
    }
}

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace X39.UnitedTacticalForces.Api.OpenApi;

public sealed class FixBySuppressionOfNumberTypesExposingString : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        AdjustOpenApiSchemaTypes(schema);
        if (schema.Properties is null)
            return Task.CompletedTask;
        foreach (var (_, openApiSchemaCandidate) in schema.Properties)
        {
            if (openApiSchemaCandidate is not OpenApiSchema { Type: not null } openApiSchema)
                continue;
            AdjustOpenApiSchemaTypes(openApiSchema);
        }

        return Task.CompletedTask;
    }

    private static void AdjustOpenApiSchemaTypes(OpenApiSchema openApiSchema)
    {
        if (openApiSchema.Type is null)
            return;
        if (openApiSchema.Type.Value.HasFlag(JsonSchemaType.String) && openApiSchema.Format is "int32" or "int64")
            openApiSchema.Type = openApiSchema.Type.Value.HasFlag(JsonSchemaType.Integer)
                ? openApiSchema.Type & ~JsonSchemaType.String
                : openApiSchema.Type.Value.HasFlag(JsonSchemaType.Null)
                    ? JsonSchemaType.Null | JsonSchemaType.Integer
                    : JsonSchemaType.Integer;
        else if (openApiSchema.Type.Value.HasFlag(JsonSchemaType.String)
                 && openApiSchema.Format is "double" or "float")
            openApiSchema.Type = openApiSchema.Type.Value.HasFlag(JsonSchemaType.Number)
                ? openApiSchema.Type & ~JsonSchemaType.String
                : openApiSchema.Type.Value.HasFlag(JsonSchemaType.Null)
                    ? JsonSchemaType.Null | JsonSchemaType.Number
                    : JsonSchemaType.Number;
    }
}

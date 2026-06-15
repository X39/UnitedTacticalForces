using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace X39.UnitedTacticalForces.Api.OpenApi;

public class FixFileResultsSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        if (context.JsonTypeInfo.Type.IsAssignableTo(typeof(FileResult)) is false)
            return Task.CompletedTask;

        schema.Properties?.Clear();
        schema.Type     =   JsonSchemaType.String;
        schema.Format   =   "binary";
        schema.Metadata ??= new Dictionary<string, object>();
        schema.Metadata.Add("fix-file-results", true);

        return Task.CompletedTask;
    }
}

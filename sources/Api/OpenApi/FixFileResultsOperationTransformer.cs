using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace X39.UnitedTacticalForces.Api.OpenApi;

public class FixFileResultsOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        if (context.Document is null)
            throw new InvalidOperationException("Document is null");
        if (context.Document.Components is null)
            throw new InvalidOperationException("Document components are null");
        if (context.Document.Components.Schemas is null)
            throw new InvalidOperationException("Document schemas are null");
        if (operation.Responses is null)
            return Task.CompletedTask;
        foreach (var (key, openApiResponse) in operation.Responses)
        {
            if (openApiResponse.Content is null)
                continue;

            OpenApiSchema? openApiSchema = null;
            foreach (var (mimeType, openApiMediaType) in openApiResponse.Content)
            {
                if (openApiMediaType.Schema is null)
                    continue;
                var schemaCandidate = openApiMediaType.Schema;
                if (openApiMediaType.Schema is OpenApiSchemaReference { Reference.Id: not null } openApiSchemaReference)
                    schemaCandidate = context.Document.Components.Schemas[openApiSchemaReference.Reference.Id];
                if (schemaCandidate is not OpenApiSchema schema)
                    throw new InvalidOperationException(
                        "Schema is not an OpenApiSchema... Fuck this stupid piece of garbage API trash"
                    );
                schema.Metadata ??= new Dictionary<string, object>();
                if (!schema.Metadata.ContainsKey("fix-file-results"))
                    continue;
                openApiSchema = schema;
                break;
            }

            if (openApiSchema is null)
                continue;

            openApiResponse.Content.Clear();
            openApiResponse.Content.Add("application/octet-stream", new OpenApiMediaType { Schema = openApiSchema });
        }

        return Task.CompletedTask;
    }
}
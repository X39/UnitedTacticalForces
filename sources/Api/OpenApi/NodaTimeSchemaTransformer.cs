using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using X39.Util;

namespace X39.UnitedTacticalForces.Api.OpenApi;

public sealed class NodaTimeSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        var type = context.JsonTypeInfo.Type.GetDeNulledType();
        if (type == typeof(Instant))
        {
            schema.Type   = JsonSchemaType.String;
            schema.Format = "date-time";
            schema.Properties?.Clear();
        }
        else if (type == typeof(LocalDate))
        {
            schema.Type   = JsonSchemaType.String;
            schema.Format = "date";
            schema.Properties?.Clear();
        }
        else if (type == typeof(LocalTime))
        {
            schema.Type   = JsonSchemaType.String;
            schema.Format = "time";
            schema.Properties?.Clear();
        }
        else if (type == typeof(CalendarSystem))
        {
            schema.Properties?.Clear();
            schema.Type   = JsonSchemaType.String;
            schema.Format = null;
        }

        return Task.CompletedTask;
    }
}

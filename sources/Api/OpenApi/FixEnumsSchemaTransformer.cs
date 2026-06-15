using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using X39.Util;
using X39.Util.Collections;

namespace X39.UnitedTacticalForces.Api.OpenApi;

internal class FixEnumsSchemaTransformer : IOpenApiSchemaTransformer
{
    public async Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        var enumType = context.JsonTypeInfo.Type.GetDeNulledType();
        var isNullable = !enumType.IsEquivalentTo(context.JsonTypeInfo.Type);
        if (!context.JsonTypeInfo.Type.GetDeNulledType().IsEnum)
            return;

        schema.Enum       ??= new List<JsonNode>();
        schema.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        schema.OneOf      ??= new List<IOpenApiSchema>();

        // This is because of a bug that doesn't populate this.
        schema.Enum.Clear(); // Hack just in case they fix the bug.
        foreach (var name in Enum.GetNames(enumType))
            schema.Enum.Add(name);

        // Add x-ms-enum extension
        var enumValues = new JsonArray();
        enumValues.AddRange(
            Enum.GetNames(enumType)
                .Select(name => new JsonObject
                    {
                        ["name"]        = name,
                        ["value"]       = (int) Enum.Parse(enumType, name),
                        ["description"] = GetEnumDescription(enumType, name),
                    }
                )
        );

        schema.Extensions["x-ms-enum"] = new JsonNodeExtension(
            new JsonObject
            {
                ["name"]          = enumType.Name,
                ["modelAsString"] = false,
                ["values"]        = enumValues,
            }
        );

        schema.Comment = schema.Comment is null
            ? "Applied FixEnumsSchemaTransformer"
            : string.Join(Environment.NewLine, schema.Comment, "Applied FixEnumsSchemaTransformer");
    }

    private string GetEnumDescription(Type type, string name)
    {
        var memberInfo = type.GetMember(name)
            .FirstOrDefault();
        var attribute = memberInfo?.GetCustomAttribute<DescriptionAttribute>();
        return attribute?.Description ?? string.Empty;
    }
}

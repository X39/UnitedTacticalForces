using System.ComponentModel;
using System.Reflection;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

namespace X39.UnitedTacticalForces.Api.OpenApi;

internal class FixEnumsSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        Console.WriteLine(context.JsonTypeInfo.Type.Name);

        if (!context.JsonTypeInfo.Type.IsEnum)
            return Task.CompletedTask;

        var enumType = context.JsonTypeInfo.Type;

        //This is because of a bug that doesn't populate this.
        schema.Enum.Clear(); //Hack just in case they fix the bug.
        foreach (var name in Enum.GetNames(enumType)) schema.Enum.Add(new OpenApiString(name));

        // Add x-ms-enum extension
        var enumValues = new OpenApiArray();
        enumValues.AddRange(Enum.GetNames(enumType)
            .Select(name => new OpenApiObject
            {
                ["name"] = new OpenApiString(name),
                ["value"] = new OpenApiInteger((int)Enum.Parse(enumType, name)),
                ["description"] = new OpenApiString(GetEnumDescription(enumType, name))
            }));

        schema.Extensions["x-ms-enum"] = new OpenApiObject
        {
            ["name"] = new OpenApiString(enumType.Name),
            ["modelAsString"] = new OpenApiBoolean(false),
            ["values"] = enumValues
        };

        // Add enum schemas to OneOf
        foreach (var name in Enum.GetNames(enumType))
        {
            var enumValue = (int)Enum.Parse(enumType, name);
            var enumSchema = new OpenApiSchema
            {
                Type = "integer", Enum = new List<IOpenApiAny> { new OpenApiInteger(enumValue) }, Title = name
            };

            schema.OneOf.Add(enumSchema);
        }

        return Task.CompletedTask;
    }

    private string GetEnumDescription(Type type, string name)
    {
        var memberInfo = type.GetMember(name).FirstOrDefault();
        var attribute = memberInfo?.GetCustomAttribute<DescriptionAttribute>();
        return attribute?.Description ?? string.Empty;
    }
}

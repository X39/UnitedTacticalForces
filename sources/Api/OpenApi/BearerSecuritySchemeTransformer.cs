using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace X39.UnitedTacticalForces.Api.OpenApi;

internal sealed class BearerSecuritySchemeTransformer(IAuthenticationSchemeProvider authenticationSchemeProvider)
    : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        var authenticationSchemes = await authenticationSchemeProvider.GetAllSchemesAsync();
        if (authenticationSchemes.Any(authScheme => authScheme.Name == "Bearer"))
        {
            var requirements = new Dictionary<string, IOpenApiSecurityScheme>
            {
                ["Bearer"] = new OpenApiSecurityScheme
                {
                    Type         = SecuritySchemeType.Http,
                    Scheme       = "bearer",
                    BearerFormat = "Json Web Token",
                    In           = ParameterLocation.Header,
                    Name         = "Authorization",
                    Description =
                        "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                },
            };
            document.Components                 ??= new OpenApiComponents();
            document.Components.SecuritySchemes =   requirements;

            // Apply it as a requirement for all operations
            foreach (var operation in document.Paths.Values.SelectMany(path => path.Operations ?? []))
            {
                if (operation.Value.Metadata is not null
                    && operation.Value.Metadata.TryGetValue("x-anonymous", out var obj)
                    && obj is true)
                    continue;
                operation.Value.Security ??= new List<OpenApiSecurityRequirement>();
                operation.Value.Security.Add(
                    new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference("Bearer")] = [],
                    }
                );
            }
        }
    }
}

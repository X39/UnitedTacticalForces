using Xunit;

namespace Api.Tests.Security;

public class ApiSecurityConfigurationTests
{
    [Fact]
    public void ApiConfiguration_DisablesAutoVerifyByDefaultAndInProduction()
    {
        var baseline = ReadRepositoryFile("sources/Api/appsettings.json");
        var production = ReadRepositoryFile("sources/Api/appsettings.Production.json");

        Assert.Contains("\"AutoVerifyNewUsers\": false", baseline, StringComparison.Ordinal);
        Assert.Contains("\"AutoVerifyNewUsers\": false", production, StringComparison.Ordinal);
        Assert.DoesNotContain("\"AutoVerifyNewUsers\": true", baseline, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"AutoVerifyNewUsers\": true", production, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApiPipeline_EnablesWebSocketsBeforeAuthenticationAndControllers()
    {
        var source = ReadRepositoryFile("sources/Api/Program.cs");
        var webSocketsIndex = source.IndexOf("app.UseWebSockets();", StringComparison.Ordinal);
        var authenticationIndex = source.IndexOf("app.UseAuthentication();", StringComparison.Ordinal);
        var controllersIndex = source.IndexOf("app.MapControllers();", StringComparison.Ordinal);

        Assert.True(webSocketsIndex >= 0, "Program.cs must call app.UseWebSockets().");
        Assert.True(authenticationIndex >= 0, "Program.cs must call app.UseAuthentication().");
        Assert.True(controllersIndex >= 0, "Program.cs must call app.MapControllers().");
        Assert.True(
            webSocketsIndex < authenticationIndex,
            "Program.cs must call app.UseWebSockets() before app.UseAuthentication()."
        );
        Assert.True(
            webSocketsIndex < controllersIndex,
            "Program.cs must call app.UseWebSockets() before app.MapControllers()."
        );
    }

    [Fact]
    public void ApiPipeline_UsesForwardedHeadersBeforeHttpsAndAuthentication()
    {
        var source = ReadRepositoryFile("sources/Api/Program.cs");
        var addForwardedHeadersIndex = source.IndexOf("AddForwardedHeaders(builder);", StringComparison.Ordinal);
        var useForwardedHeadersIndex = source.IndexOf("app.UseForwardedHeaders();", StringComparison.Ordinal);
        var useHttpsRedirectionIndex = source.IndexOf("app.UseHttpsRedirection();", StringComparison.Ordinal);
        var authenticationIndex = source.IndexOf("app.UseAuthentication();", StringComparison.Ordinal);

        Assert.Contains("using Microsoft.AspNetCore.HttpOverrides;", source, StringComparison.Ordinal);
        Assert.Contains(
            "options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;",
            source,
            StringComparison.Ordinal
        );
        Assert.Contains("options.ForwardLimit", source, StringComparison.Ordinal);
        Assert.DoesNotContain("KnownNetworks.Clear()", source, StringComparison.Ordinal);
        Assert.DoesNotContain("KnownProxies.Clear()", source, StringComparison.Ordinal);
        Assert.True(addForwardedHeadersIndex >= 0, "Program.cs must configure forwarded headers.");
        Assert.True(useForwardedHeadersIndex >= 0, "Program.cs must enable forwarded headers middleware.");
        Assert.True(useHttpsRedirectionIndex >= 0, "Program.cs must call app.UseHttpsRedirection().");
        Assert.True(authenticationIndex >= 0, "Program.cs must call app.UseAuthentication().");
        Assert.True(
            useForwardedHeadersIndex < useHttpsRedirectionIndex,
            "Forwarded headers must run before HTTPS redirection behind TLS-terminating proxies."
        );
        Assert.True(
            useForwardedHeadersIndex < authenticationIndex,
            "Forwarded headers must run before authentication reads request scheme and host metadata."
        );
    }

    [Fact]
    public void ApiAuthCookie_UsesExplicitSecureHttpOnlySameSiteSettings()
    {
        var source = ReadRepositoryFile("sources/Api/Program.cs");

        Assert.Contains(
            "options.Cookie.SecurePolicy        = CookieSecurePolicy.Always;",
            source,
            StringComparison.Ordinal
        );
        Assert.Contains("options.Cookie.HttpOnly            = true;", source, StringComparison.Ordinal);
        Assert.Contains("options.Cookie.SameSite            = SameSiteMode.Lax;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void UsersLogout_OnlyPostCanSignOut()
    {
        var source = ReadRepositoryFile("sources/Api/Controllers/UsersController.cs");
        var methodStart = source.IndexOf("public IActionResult LogoutAsync", StringComparison.Ordinal);
        var nextMethodStart = source.IndexOf("public async Task<IActionResult> MeDeleteAsync", StringComparison.Ordinal);

        Assert.True(methodStart >= 0, "UsersController must define LogoutAsync.");
        Assert.True(nextMethodStart > methodStart, "UsersController must define MeDeleteAsync after LogoutAsync.");

        var logoutMethod = source[methodStart..nextMethodStart];
        Assert.Contains("[HttpPost(\"logout\", Name = nameof(LogoutAsync))]", source, StringComparison.Ordinal);
        Assert.DoesNotContain("[HttpGet(\"logout\"", source, StringComparison.Ordinal);
        Assert.Contains("SignOut(", logoutMethod, StringComparison.Ordinal);
    }

    [Fact]
    public void ApiPipeline_RegistersCorsServicesForRestrictedCorsMiddleware()
    {
        var source = ReadRepositoryFile("sources/Api/Program.cs");
        var addServicesIndex = source.IndexOf("AddServices(builder);", StringComparison.Ordinal);
        var useRestrictedCorsIndex = source.IndexOf("UseRestrictedCors(app);", StringComparison.Ordinal);

        Assert.Contains("builder.Services.AddCors();", source, StringComparison.Ordinal);
        Assert.Contains("app.UseCors(", source, StringComparison.Ordinal);
        Assert.True(addServicesIndex >= 0, "Program.cs must call AddServices(builder).");
        Assert.True(useRestrictedCorsIndex >= 0, "Program.cs must call UseRestrictedCors(app).");
        Assert.True(
            addServicesIndex < useRestrictedCorsIndex,
            "Program.cs must register services before UseRestrictedCors(app) runs."
        );
    }

    [Fact]
    public void ApiDatabaseConnectionString_DocumentationUsesRuntimeKey()
    {
        var readme = ReadRepositoryFile("ReadMe.md");
        var template = ReadRepositoryFile("sources/Api/appsettings.secret.json_template");

        Assert.Contains("\"database\": \"YOUR_POSTGRES_CONNECTION_STRING\"", readme, StringComparison.Ordinal);
        Assert.Contains("\"database\": \"YOUR_POSTGRES_CONNECTION_STRING\"", template, StringComparison.Ordinal);
        Assert.DoesNotContain("\"DatabaseContext\": \"YOUR_POSTGRES_CONNECTION_STRING\"", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("\"DatabaseContext\": \"YOUR_POSTGRES_CONNECTION_STRING\"", template, StringComparison.Ordinal);
    }

    [Fact]
    public void ApiConfiguration_RuntimeOverridesSecretAndUserJson()
    {
        var source = ReadRepositoryFile("sources/Api/Program.cs");
        var secretIndex = source.IndexOf("AddJsonFile(\"appsettings.secret.json\"", StringComparison.Ordinal);
        var userIndex = source.IndexOf("AddJsonFile(\"appsettings.user.json\"", StringComparison.Ordinal);
        var environmentIndex = source.IndexOf("AddEnvironmentVariables();", StringComparison.Ordinal);
        var commandLineIndex = source.IndexOf("AddCommandLine(args);", StringComparison.Ordinal);

        Assert.True(secretIndex >= 0, "Program.cs must load optional appsettings.secret.json.");
        Assert.True(userIndex > secretIndex, "Program.cs must load appsettings.user.json after appsettings.secret.json.");
        Assert.True(environmentIndex > userIndex, "Environment variables must override local JSON configuration.");
        Assert.True(commandLineIndex > environmentIndex, "Command-line arguments must override all JSON configuration.");
    }

    private static string ReadRepositoryFile(string repositoryRelativePath)
        => File.ReadAllText(Path.Combine(RepositoryRoot, repositoryRelativePath));

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "X39.UnitedTacticalForces.slnx")))
                directory = directory.Parent;

            return directory?.FullName
                   ?? throw new InvalidOperationException("Could not find repository root.");
        }
    }
}

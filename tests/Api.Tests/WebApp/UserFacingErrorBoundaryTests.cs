using Xunit;

namespace Api.Tests.WebApp;

public class UserFacingErrorBoundaryTests
{
    [Fact]
    public void WebApp_UsesCentralizedExceptionSanitizerForUserFacingErrors()
    {
        var failures = new List<string>();
        foreach (var file in EnumerateWebAppSourceFiles())
        {
            var relativePath = Path.GetRelativePath(RepositoryRoot, file).Replace('\\', '/');
            var source = File.ReadAllText(file);

            AssertDoesNotContain(source, "ex.Message", relativePath, failures);
            AssertDoesNotContain(source, "ex.StackTrace", relativePath, failures);
            AssertDoesNotContain(source, "exception.Message", relativePath, failures);
            AssertDoesNotContain(source, "StackTrace", relativePath, failures);
        }

        var imports = ReadRepositoryFile("sources/WebApp/_Imports.razor");
        AssertContains(
            imports,
            "@using X39.UnitedTacticalForces.WebApp.Services",
            "sources/WebApp/_Imports.razor",
            failures
        );

        var sanitizer = ReadRepositoryFile("sources/WebApp/Services/UserFacingError.cs");
        AssertContains(sanitizer, "public static string Message(Exception exception", "UserFacingError", failures);
        AssertContains(sanitizer, "ContainsUnsafeDiagnosticText", "UserFacingError", failures);

        AssertNoFailures(failures);
    }

    private static IEnumerable<string> EnumerateWebAppSourceFiles()
        => Directory.EnumerateFiles(Path.Combine(RepositoryRoot, "sources/WebApp"), "*.*", SearchOption.AllDirectories)
            .Where((file) =>
            {
                var normalized = file.Replace('\\', '/');
                return (normalized.EndsWith(".cs", StringComparison.Ordinal)
                        || normalized.EndsWith(".razor", StringComparison.Ordinal))
                       && !normalized.Contains("/UtfApi/", StringComparison.Ordinal)
                       && !normalized.EndsWith("/Services/UserFacingError.cs", StringComparison.Ordinal);
            });

    private static string ReadRepositoryFile(string repositoryRelativePath)
        => File.ReadAllText(Path.Combine(RepositoryRoot, repositoryRelativePath));

    private static void AssertContains(
        string source,
        string marker,
        string file,
        ICollection<string> failures)
    {
        if (!source.Contains(marker, StringComparison.Ordinal))
            failures.Add($"{file}: expected to contain '{marker}'.");
    }

    private static void AssertDoesNotContain(
        string source,
        string marker,
        string file,
        ICollection<string> failures)
    {
        if (source.Contains(marker, StringComparison.Ordinal))
            failures.Add($"{file}: should not contain '{marker}'.");
    }

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

    private static void AssertNoFailures(IEnumerable<string> failures)
    {
        var failureList = failures.ToArray();
        Assert.True(failureList.Length == 0, string.Join(Environment.NewLine, failureList));
    }
}

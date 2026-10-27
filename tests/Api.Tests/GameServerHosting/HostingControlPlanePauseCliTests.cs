using System.Diagnostics;
using System.Text.Json;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>Runs the actual API executable's pause check without creating a host or contacting a database.</summary>
public sealed class HostingControlPlanePauseCliTests
{
    /// <summary>Only a validated paused setting produces the successful machine-readable receipt.</summary>
    /// <param name="setting">The setting in the public configuration, or null to omit it.</param>
    /// <param name="success">Whether the executable must attest that hosting is paused.</param>
    /// <returns>A task completing after the real executable exits and its streams are checked.</returns>
    [Theory]
    [InlineData(null, true)]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("invalid", false)]
    public Task CheckPause_ReportsOnlyValidatedPausedState(string? setting, bool success)
        => AssertCliAsync(setting, null, null, null, null, success);

    /// <summary>The CLI check uses the same final configuration precedence as normal startup.</summary>
    /// <param name="publicSetting">The public configuration setting.</param>
    /// <param name="secretSetting">An optional synthetic private configuration override.</param>
    /// <param name="userSetting">An optional synthetic user configuration override.</param>
    /// <param name="environmentSetting">An optional environment override.</param>
    /// <param name="commandLineSetting">An optional command-line override.</param>
    /// <param name="success">Whether the final setting must attest that hosting is paused.</param>
    /// <returns>A task completing after the actual executable's configuration decision is checked.</returns>
    [Theory]
    [InlineData("true", "false", null, null, null, false)]
    [InlineData("false", "true", null, null, null, true)]
    [InlineData("false", "false", "true", null, null, true)]
    [InlineData("true", "true", "false", null, null, false)]
    [InlineData("true", "true", "true", "false", null, false)]
    [InlineData("false", "false", "false", "true", null, true)]
    [InlineData("false", "false", "false", "false", "true", true)]
    [InlineData("true", "true", "true", "true", "false", false)]
    [InlineData("true", "true", "true", "invalid", null, false)]
    public Task CheckPause_UsesFinalConfigurationPrecedence(
        string publicSetting,
        string? secretSetting,
        string? userSetting,
        string? environmentSetting,
        string? commandLineSetting,
        bool success)
        => AssertCliAsync(
            publicSetting,
            secretSetting,
            userSetting,
            environmentSetting,
            commandLineSetting,
            success);

    /// <summary>Runs the existing API assembly against isolated JSON fixtures and checks its exact output.</summary>
    /// <param name="publicSetting">The public configuration setting, or null to omit it.</param>
    /// <param name="secretSetting">An optional synthetic private configuration override.</param>
    /// <param name="userSetting">An optional synthetic user configuration override.</param>
    /// <param name="environmentSetting">An optional environment override.</param>
    /// <param name="commandLineSetting">An optional command-line override.</param>
    /// <param name="success">Whether the check must return a successful paused receipt.</param>
    /// <returns>A task completing after the subprocess and its temporary fixtures are cleaned up.</returns>
    private static async Task AssertCliAsync(
        string? publicSetting,
        string? secretSetting,
        string? userSetting,
        string? environmentSetting,
        string? commandLineSetting,
        bool success)
    {
        var fixtureDirectory = Path.Combine(Path.GetTempPath(), $"utf-pause-check-{Guid.NewGuid():N}");
        Directory.CreateDirectory(fixtureDirectory);
        try
        {
            await WriteConfigurationAsync(fixtureDirectory, "appsettings.json", publicSetting, includeInvalidDatabase: true);
            if (secretSetting is not null)
                await WriteConfigurationAsync(fixtureDirectory, "appsettings.secret.json", secretSetting);
            if (userSetting is not null)
                await WriteConfigurationAsync(fixtureDirectory, "appsettings.user.json", userSetting);
            var startInfo = new ProcessStartInfo
            {
                FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
                WorkingDirectory = fixtureDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            startInfo.Environment.Clear();
            foreach (var variable in new[] { "PATH", "SystemRoot", "WINDIR", "TEMP", "TMP", "DOTNET_ROOT", "DOTNET_ROOT_X64" })
            {
                if (Environment.GetEnvironmentVariable(variable) is { } value)
                    startInfo.Environment[variable] = value;
            }
            startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
            startInfo.Environment["DOTNET_ENVIRONMENT"] = "Production";
            startInfo.Environment["DOTNET_NOLOGO"] = "1";
            startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
            if (environmentSetting is not null)
                startInfo.Environment["Hosting__ControlPlane__Paused"] = environmentSetting;
            startInfo.ArgumentList.Add(typeof(HostingControlPlaneState).Assembly.Location);
            startInfo.ArgumentList.Add("--check-hosting-paused");
            if (commandLineSetting is not null)
                startInfo.ArgumentList.Add($"--Hosting:ControlPlane:Paused={commandLineSetting}");
            using var process = Process.Start(startInfo);
            Assert.NotNull(process);
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            finally
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync();
                }
            }

            Assert.Equal(success ? 0 : 1, process.ExitCode);
            Assert.Equal(success ? "{\"paused\":true}" + Environment.NewLine : string.Empty, await output);
            Assert.Equal(string.Empty, await error);
        }
        finally
        {
            Directory.Delete(fixtureDirectory, recursive: true);
        }
    }

    /// <summary>Writes fixture values only; no application configuration or real secrets are copied.</summary>
    /// <param name="directory">The temporary fixture directory.</param>
    /// <param name="fileName">The configuration filename consumed by the API.</param>
    /// <param name="setting">The pause setting, or null to omit it.</param>
    /// <param name="includeInvalidDatabase">Whether to add a database value that cannot initialize EF Core.</param>
    /// <returns>A task completing after the bounded JSON fixture has been written.</returns>
    private static Task WriteConfigurationAsync(
        string directory,
        string fileName,
        string? setting,
        bool includeInvalidDatabase = false)
    {
        var values = new Dictionary<string, object?>();
        if (setting is not null)
            values["Hosting"] = new { ControlPlane = new { Paused = setting } };
        if (includeInvalidDatabase)
            values["ConnectionStrings"] = new { database = "Deliberately invalid connection string: do not initialize EF." };
        return File.WriteAllTextAsync(Path.Combine(directory, fileName), JsonSerializer.Serialize(values));
    }
}

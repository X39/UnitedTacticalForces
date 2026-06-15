using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Playwright;
using Xunit;

namespace Ui.Tests;

public sealed class WebAppHostFixture : IAsyncLifetime
{
    private readonly List<string> _output = [];
    private Process? _process;
    private IPlaywright? _playwright;

    public string BaseUrl { get; private set; } = string.Empty;

    public IBrowser Browser { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var suppliedBaseUrl = Environment.GetEnvironmentVariable("UTF_UI_TEST_BASE_URL");
        if (!string.IsNullOrWhiteSpace(suppliedBaseUrl))
        {
            BaseUrl = suppliedBaseUrl.TrimEnd('/') + "/";
        }
        else
        {
            BaseUrl = $"http://127.0.0.1:{GetFreeTcpPort()}/";
            StartWebApp();
            await WaitForWebAppAsync();
        }

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
        });
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null)
            await Browser.CloseAsync();
        _playwright?.Dispose();

        if (_process is null)
            return;

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync();
            }
        }
        finally
        {
            _process.Dispose();
        }
    }

    public string GetRecentOutput()
    {
        lock (_output)
        {
            return string.Join(Environment.NewLine, _output.TakeLast(80));
        }
    }

    private void StartWebApp()
    {
        var repoRoot = FindRepoRoot();
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(Path.Combine(repoRoot, "sources", "WebApp", "WebApp.csproj"));
        startInfo.ArgumentList.Add("--no-build");
        startInfo.ArgumentList.Add("--no-launch-profile");
        startInfo.ArgumentList.Add("--urls");
        startInfo.ArgumentList.Add(BaseUrl.TrimEnd('/'));

        _process = Process.Start(startInfo)
                   ?? throw new InvalidOperationException("Failed to start the WebApp test server.");
        _process.OutputDataReceived += (_, args) => CaptureOutput(args.Data);
        _process.ErrorDataReceived += (_, args) => CaptureOutput(args.Data);
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
    }

    private async Task WaitForWebAppAsync()
    {
        using var httpClient = new HttpClient();
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(90));

        while (!cancellationTokenSource.IsCancellationRequested)
        {
            if (_process?.HasExited is true)
                throw new InvalidOperationException(
                    $"WebApp test server exited early with code {_process.ExitCode}.{Environment.NewLine}{GetRecentOutput()}"
                );

            try
            {
                using var response = await httpClient.GetAsync(BaseUrl, cancellationTokenSource.Token);
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch
            {
                // The development server is still booting.
            }

            await Task.Delay(250, cancellationTokenSource.Token);
        }

        throw new TimeoutException($"Timed out waiting for WebApp test server.{Environment.NewLine}{GetRecentOutput()}");
    }

    private void CaptureOutput(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        lock (_output)
        {
            _output.Add(line);
            if (_output.Count > 200)
                _output.RemoveRange(0, _output.Count - 200);
        }
    }

    private static int GetFreeTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint) listener.LocalEndpoint).Port;
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "X39.UnitedTacticalForces.slnx")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("Failed to find repository root.");
    }
}

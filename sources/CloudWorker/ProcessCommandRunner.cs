using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace X39.UnitedTacticalForces.CloudWorker;

public sealed class ProcessCommandRunner : IProcessCommandRunner
{
    private const int OutputLimit = 16 * 1024;

    public async Task<ProcessCommandResult> RunAsync(
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken)
    {
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        try
        {
            using var process = Process.Start(startInfo)
                                ?? throw new InvalidOperationException("Worker command process could not be created.");
            using var cancellationRegistration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                }
            });
            var outputTask = ReadBoundedTailAsync(process.StandardOutput, cancellationToken);
            var errorTask = ReadBoundedTailAsync(process.StandardError, cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var output = await outputTask.ConfigureAwait(false);
            var error = await errorTask.ConfigureAwait(false);
            return new ProcessCommandResult(
                process.ExitCode == 0,
                process.ExitCode,
                WorkerSecretRedactor.Redact(output) ?? string.Empty,
                WorkerSecretRedactor.Redact(error) ?? string.Empty);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception
                                      or InvalidOperationException)
        {
            return new ProcessCommandResult(
                false,
                -1,
                string.Empty,
                WorkerSecretRedactor.Redact(ex.Message) ?? "Worker command failed.");
        }
    }

    private static async Task<string> ReadBoundedTailAsync(
        StreamReader reader,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        var buffer = new char[2048];
        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0)
                return builder.ToString();
            builder.Append(buffer, 0, read);
            if (builder.Length > OutputLimit)
                builder.Remove(0, builder.Length - OutputLimit);
        }
    }
}

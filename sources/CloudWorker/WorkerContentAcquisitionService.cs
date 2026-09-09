using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Runtime.ExceptionServices;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>Obtains registered provider content and applies it through game-specific safe installers.</summary>
internal sealed partial class WorkerContentAcquisitionService
{
    private const int MaximumReferences = 256;
    private readonly CloudWorkerOptions _options;
    private readonly ICloudWorkerStorageTransfer _storage;
    private readonly IProcessCommandRunner _runner;
    private readonly IHostAddressResolver _resolver;
    private readonly IContentDirectoryCleaner _cleaner;

    /// <summary>Initializes a worker content acquisition service.</summary>
    /// <param name="options">Worker safety options.</param>
    /// <param name="storage">Command-bound artifact transport.</param>
    /// <param name="runner">Restricted external process runner.</param>
    /// <param name="resolver">Repository host resolver.</param>
    /// <param name="cleaner">Verified provider staging cleaner.</param>
    public WorkerContentAcquisitionService(
        CloudWorkerOptions options,
        ICloudWorkerStorageTransfer storage,
        IProcessCommandRunner? runner = null,
        IHostAddressResolver? resolver = null,
        IContentDirectoryCleaner? cleaner = null)
    {
        _options = options;
        _storage = storage;
        _runner = runner ?? new ProcessCommandRunner();
        _resolver = resolver ?? new DnsHostAddressResolver();
        _cleaner = cleaner ?? new ContentDirectoryCleaner();
    }

    /// <summary>Acquires and installs non-Steam content selected for a Start command.</summary>
    /// <param name="command">Claimed Start command.</param>
    /// <param name="references">Normalized effective content references.</param>
    /// <param name="gameIdentifier">Worker-owned game implementation identifier.</param>
    /// <param name="installRoot">Worker-owned game installation root.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="includeClientOnly">Whether client-only content applies to this game process.</param>
    /// <param name="includeServerOnly">Whether server-only content applies to this game process.</param>
    /// <returns>Concrete versions observed during acquisition.</returns>
    public async Task<IReadOnlyCollection<WorkerObservedContentVersion>> AcquireAndInstallAsync(
        WorkerCommandClaimResponse command,
        IReadOnlyCollection<WorkerContentReference> references,
        string gameIdentifier,
        string installRoot,
        CancellationToken cancellationToken,
        bool includeClientOnly = false,
        bool includeServerOnly = true)
    {
        if (references.Count > MaximumReferences)
            throw Invalid("content-reference-limit", "The command contains too many content references.");
        installRoot = Path.GetFullPath(installRoot);
        var stagingRoot = Path.Combine(
            Path.GetFullPath(_options.StateDirectory),
            "content-staging",
            $"{command.CommandId:N}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingRoot);
        var targets = new HashSet<string>(OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal);
        var observed = new List<WorkerObservedContentVersion>();
        IReadOnlyCollection<WorkerObservedContentVersion>? result = null;
        Exception? primaryFailure = null;
        try
        {
            foreach (var reference in references
                         .Where(item => includeClientOnly
                                        || !string.Equals(item.UsageRole, "ClientOnly", StringComparison.OrdinalIgnoreCase))
                         .Where(item => includeServerOnly
                                        || !string.Equals(item.UsageRole, "ServerOnly", StringComparison.OrdinalIgnoreCase))
                         .OrderBy(item => InstallerOrder(gameIdentifier, item.InstallerIdentifier))
                         .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(item => item.Identifier, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                switch (reference.Source)
                {
                    case "steam-workshop":
                        if (string.Equals(gameIdentifier, "cs2", StringComparison.OrdinalIgnoreCase))
                            throw Invalid("provider-incompatible", "Steam Workshop content is not compatible with the CS2 worker.");
                        break;
                    case "uploaded-artifact":
                        await InstallArtifactAsync(command, reference, gameIdentifier, installRoot, stagingRoot, targets,
                                cancellationToken)
                            .ConfigureAwait(false);
                        observed.Add(Observed(reference, reference.ChecksumSha256!));
                        break;
                    case "git":
                        var commit = await InstallGitAsync(reference, gameIdentifier, installRoot, stagingRoot, targets,
                                cancellationToken)
                            .ConfigureAwait(false);
                        observed.Add(Observed(reference, commit));
                        break;
                    case "built-in":
                        break;
                    default:
                        throw Invalid("provider-unsupported", "The selected content provider is unsupported by this worker.");
                }
            }
            result = observed;
        }
        catch (Exception exception)
        {
            primaryFailure = exception;
        }
        Exception? cleanupFailure = null;
        try
        {
            _cleaner.DeleteRequired(stagingRoot);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            cleanupFailure = exception;
        }
        if (primaryFailure is not null)
        {
            if (cleanupFailure is not null)
                primaryFailure.Data["ProviderStagingCleanupFailure"] = cleanupFailure.GetType().Name;
            ExceptionDispatchInfo.Capture(primaryFailure).Throw();
        }
        if (cleanupFailure is not null)
        {
            throw Invalid(
                "content-cleanup-failed",
                "Provider staging content could not be removed safely after installation.");
        }
        return result!;
    }

    private async Task InstallArtifactAsync(
        WorkerCommandClaimResponse command,
        WorkerContentReference reference,
        string gameIdentifier,
        string installRoot,
        string stagingRoot,
        ISet<string> targets,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reference.ArtifactIdentifier) || !Sha256Regex().IsMatch(reference.ChecksumSha256 ?? ""))
            throw Invalid("artifact-invalid", "The uploaded artifact declaration is incomplete.");
        var archivePath = Path.Combine(stagingRoot, $"{Guid.NewGuid():N}.zip");
        try
        {
            await _storage.DownloadContentArtifactAsync(command, reference.ArtifactIdentifier, archivePath, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            if (exception.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.Conflict)
                throw Invalid("artifact-unavailable", "The declared uploaded artifact is unavailable.");
            throw Retryable("artifact-transfer-unavailable", "The uploaded artifact is temporarily unavailable.", exception);
        }
        await using (var stream = File.OpenRead(archivePath))
        {
            var checksum = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
            if (!string.Equals(checksum, reference.ChecksumSha256, StringComparison.OrdinalIgnoreCase))
                throw Invalid("artifact-checksum-invalid", "The uploaded artifact checksum does not match its immutable version.");
        }
        TrustedArchiveInstaller.Install(
            archivePath,
            installRoot,
            gameIdentifier,
            reference.InstallerIdentifier,
            targets,
            string.Equals(gameIdentifier, "arma3", StringComparison.OrdinalIgnoreCase)
                ? WorkerContentPath.GetArma3ModDirectoryName(reference)
                : null);
    }

    private async Task<string> InstallGitAsync(
        WorkerContentReference reference,
        string gameIdentifier,
        string installRoot,
        string stagingRoot,
        ISet<string> targets,
        CancellationToken cancellationToken)
    {
        var repository = await ValidateRepositoryAsync(reference.Identifier, cancellationToken).ConfigureAwait(false);
        var checkoutPath = Path.Combine(stagingRoot, $"git-{Guid.NewGuid():N}");
        var hooksPath = Path.Combine(stagingRoot, "disabled-hooks");
        Directory.CreateDirectory(hooksPath);
        var selector = string.IsNullOrWhiteSpace(reference.VersionIdentifier) ? null : reference.VersionIdentifier.Trim();
        if (selector is not null && !SafeRefRegex().IsMatch(selector) && !CommitRegex().IsMatch(selector))
            throw Invalid("git-ref-invalid", "The Git reference is malformed.");

        var cloneArguments = new List<string>
        {
            "-c", "credential.helper=", "-c", "http.followRedirects=false", "-c", "protocol.file.allow=never",
            "-c", $"core.hooksPath={hooksPath}",
            "-c", "submodule.recurse=false", "-c", "filter.lfs.smudge=", "-c", "filter.lfs.required=false",
            "clone", "--no-tags", "--depth", "1", "--no-recurse-submodules",
        };
        if (selector is not null && !CommitRegex().IsMatch(selector))
        {
            cloneArguments.Add("--branch");
            cloneArguments.Add(selector);
        }
        cloneArguments.Add(repository.AbsoluteUri);
        cloneArguments.Add(checkoutPath);
        await RunGitAsync(cloneArguments, stagingRoot, cancellationToken).ConfigureAwait(false);
        if (selector is not null && CommitRegex().IsMatch(selector))
        {
            await RunGitAsync(
                    ["-C", checkoutPath, "-c", "credential.helper=", "-c", "http.followRedirects=false", "fetch",
                        "--depth", "1", "--no-tags", "origin", selector],
                    stagingRoot,
                    cancellationToken)
                .ConfigureAwait(false);
            await RunGitAsync(["-C", checkoutPath, "checkout", "--detach", selector], stagingRoot, cancellationToken)
                .ConfigureAwait(false);
        }
        var revision = await RunGitAsync(["-C", checkoutPath, "rev-parse", "HEAD"], stagingRoot, cancellationToken)
            .ConfigureAwait(false);
        var commit = revision.Output.Trim();
        if (!CommitRegex().IsMatch(commit))
            throw Invalid("git-version-invalid", "Git did not return a full checked-out commit identity.");
        ValidateTree(checkoutPath, _options.Git.MaximumRepositoryBytes);
        var metadataPath = Path.Combine(checkoutPath, ".git");
        try
        {
            _cleaner.DeleteRequired(metadataPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw Invalid("git-metadata-cleanup-failed", "Git repository metadata could not be removed safely.");
        }
        TrustedArchiveInstaller.InstallDirectory(
            checkoutPath,
            installRoot,
            gameIdentifier,
            reference.InstallerIdentifier,
            targets,
            string.Equals(gameIdentifier, "arma3", StringComparison.OrdinalIgnoreCase)
                ? WorkerContentPath.GetArma3ModDirectoryName(reference)
                : null);
        return commit.ToLowerInvariant();
    }

    private async Task<Uri> ValidateRepositoryAsync(string value, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !uri.IsDefaultPort
            || string.IsNullOrWhiteSpace(uri.Host)
            || !_options.Git.AllowedHosts.Contains(uri.IdnHost))
        {
            throw Invalid("git-source-invalid", "The Git repository must use credential-free HTTPS on an allowed host.");
        }
        IPAddress[] addresses;
        try
        {
            addresses = await _resolver.ResolveAsync(uri.DnsSafeHost, cancellationToken).ConfigureAwait(false);
        }
        catch (SocketException exception)
        {
            throw Retryable("git-network-unavailable", "The Git repository host could not be resolved.", exception);
        }
        if (addresses.Length == 0 || addresses.Any(IsForbiddenAddress))
            throw Invalid("git-address-forbidden", "The Git repository host resolves to a non-public address.");
        return uri;
    }

    private async Task<ProcessCommandResult> RunGitAsync(
        IReadOnlyCollection<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(_options.Git.ExecutablePath)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        startInfo.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        startInfo.Environment["GIT_LFS_SKIP_SMUDGE"] = "1";
        startInfo.Environment["HOME"] = workingDirectory;
        startInfo.Environment["XDG_CONFIG_HOME"] = workingDirectory;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.Git.CommandTimeout);
        ProcessCommandResult result;
        try
        {
            result = await _runner.RunAsync(startInfo, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw Retryable("git-network-timeout", "The Git provider operation timed out.", exception);
        }
        if (result.Succeeded) return result;
        var diagnostic = WorkerSecretRedactor.Redact(result.Error);
        if (diagnostic?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true
            || diagnostic?.Contains("couldn't find remote ref", StringComparison.OrdinalIgnoreCase) == true
            || diagnostic?.Contains("repository", StringComparison.OrdinalIgnoreCase) == true
               && diagnostic.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
        {
            throw Invalid("git-source-not-found", "The Git repository or reference does not exist.");
        }
        if (diagnostic?.Contains("authentication", StringComparison.OrdinalIgnoreCase) == true
            || diagnostic?.Contains("redirect", StringComparison.OrdinalIgnoreCase) == true)
        {
            throw Invalid("git-source-inaccessible", "The Git source requires authentication or an unsafe redirect.");
        }
        throw Retryable("git-network-unavailable", "The Git provider operation failed temporarily.");
    }

    private static void ValidateTree(string root, long maximumBytes)
    {
        long bytes = 0;
        var entries = 0;
        foreach (var path in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories))
        {
            if (++entries > 100_000) throw Invalid("git-resource-limit", "The Git checkout contains too many entries.");
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw Invalid("git-link-forbidden", "Git content may not contain links.");
            if (File.Exists(path) && checked(bytes += new FileInfo(path).Length) > maximumBytes)
                throw Invalid("git-resource-limit", "The Git checkout exceeds the configured size limit.");
        }
    }

    private static bool IsForbiddenAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast)
            return true;
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var ipv6 = address.GetAddressBytes();
            return address.Equals(IPAddress.IPv6Any) || (ipv6[0] & 0xFE) == 0xFC;
        }
        if (address.AddressFamily != AddressFamily.InterNetwork) return true;
        var bytes = address.GetAddressBytes();
        return bytes[0] is 0 or 10 or 127
               || bytes[0] == 169 && bytes[1] == 254
               || bytes[0] == 172 && bytes[1] is >= 16 and <= 31
               || bytes[0] == 192 && bytes[1] == 168
               || bytes[0] >= 224;
    }

    private static WorkerObservedContentVersion Observed(WorkerContentReference reference, string version)
        => new()
        {
            ProviderIdentifier = reference.Source,
            SourceIdentifier = reference.Identifier,
            ObservedVersion = version,
        };

    private static int InstallerOrder(string gameIdentifier, string installerIdentifier)
        => string.Equals(gameIdentifier, "cs2", StringComparison.OrdinalIgnoreCase)
            ? installerIdentifier switch
            {
                "cs2-metamod" => 10,
                "cs2-counterstrikesharp" => 20,
                "cs2-plugin" => 30,
                "archive-overlay" => 40,
                _ => 100,
            }
            : 10;

    private static ContentAcquisitionException Invalid(string code, string message)
        => new(code, message, false);

    private static ContentAcquisitionException Retryable(string code, string message, Exception? inner = null)
        => new(code, message, true, inner);

    [GeneratedRegex("^[a-fA-F0-9]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Regex();

    [GeneratedRegex("^[a-fA-F0-9]{40}$", RegexOptions.CultureInvariant)]
    private static partial Regex CommitRegex();

    [GeneratedRegex("^(?![./])(?!.*(?:\\.\\.|//|@\\{|\\\\))[A-Za-z0-9._/-]{1,256}(?<![./])$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeRefRegex();
}

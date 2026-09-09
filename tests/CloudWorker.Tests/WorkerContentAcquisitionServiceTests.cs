using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using X39.UnitedTacticalForces.CloudWorker;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using Xunit;

namespace CloudWorker.Tests;

/// <summary>Verifies registered provider acquisition and installer safety boundaries.</summary>
public sealed class WorkerContentAcquisitionServiceTests
{
    /// <summary>Verifies immutable artifacts are downloaded, checked, and installed through the CS2 installer.</summary>
    [Fact]
    public async Task UploadedArtifact_IsCommandBoundVerifiedAndInstalled()
    {
        using var directory = new TemporaryDirectory();
        var archive = Path.Combine(directory.Path, "source.zip");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            using var writer = new StreamWriter(zip.CreateEntry("game/csgo/addons/example/plugin.dll").Open());
            await writer.WriteAsync("plugin");
        }
        var checksum = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(archive)));
        var storage = new RecordingArtifactTransfer(archive);
        var service = new WorkerContentAcquisitionService(
            Options(directory.Path),
            storage,
            new RejectingProcessRunner(),
            new FixedResolver(IPAddress.Parse("93.184.216.34")));
        var install = Path.Combine(directory.Path, "install");

        var observed = await service.AcquireAndInstallAsync(
            Command(),
            [Reference("uploaded-artifact") with { ArtifactIdentifier = "artifact-1", ChecksumSha256 = checksum }],
            "cs2",
            install,
            CancellationToken.None);

        Assert.Equal("artifact-1", storage.ArtifactIdentifier);
        Assert.True(File.Exists(Path.Combine(install, "game", "csgo", "addons", "example", "plugin.dll")));
        Assert.Equal(checksum, Assert.Single(observed).ObservedVersion);
    }

    /// <summary>Verifies CS2 rejects Steam instead of silently skipping incompatible content.</summary>
    [Fact]
    public async Task SteamWorkshop_ForCs2_IsPermanentIncompatibility()
    {
        using var directory = new TemporaryDirectory();
        var service = new WorkerContentAcquisitionService(
            Options(directory.Path),
            new RejectingArtifactTransfer(),
            new RejectingProcessRunner(),
            new FixedResolver(IPAddress.Parse("93.184.216.34")));

        var exception = await Assert.ThrowsAsync<ContentAcquisitionException>(() => service.AcquireAndInstallAsync(
            Command(),
            [Reference("steam-workshop")],
            "cs2",
            Path.Combine(directory.Path, "install"),
            CancellationToken.None));

        Assert.False(exception.Retryable);
        Assert.Equal("provider-incompatible", exception.ErrorCode);
    }

    /// <summary>Verifies allowlisted Git names resolving privately are rejected before process execution.</summary>
    [Fact]
    public async Task Git_PrivateResolvedAddress_IsRejectedBeforeClone()
    {
        using var directory = new TemporaryDirectory();
        var runner = new RejectingProcessRunner();
        var service = new WorkerContentAcquisitionService(
            Options(directory.Path),
            new RejectingArtifactTransfer(),
            runner,
            new FixedResolver(IPAddress.Loopback));

        var exception = await Assert.ThrowsAsync<ContentAcquisitionException>(() => service.AcquireAndInstallAsync(
            Command(),
            [Reference("git") with { Identifier = "https://github.com/example/repository.git" }],
            "cs2",
            Path.Combine(directory.Path, "install"),
            CancellationToken.None));

        Assert.Equal("git-address-forbidden", exception.ErrorCode);
        Assert.Equal(0, runner.InvocationCount);
    }

    /// <summary>Verifies a floating Git branch is shallow, disables risky features, and records its commit.</summary>
    [Fact]
    public async Task Git_FloatingBranch_IsShallowHardenedAndReportsCommit()
    {
        using var directory = new TemporaryDirectory();
        var runner = new SimulatedGitRunner();
        var service = new WorkerContentAcquisitionService(
            Options(directory.Path),
            new RejectingArtifactTransfer(),
            runner,
            new FixedResolver(IPAddress.Parse("93.184.216.34")));

        var observed = await service.AcquireAndInstallAsync(
            Command(),
            [Reference("git") with
            {
                Identifier = "https://github.com/example/repository.git",
                VersionIdentifier = "stable",
            }],
            "cs2",
            Path.Combine(directory.Path, "install"),
            CancellationToken.None);

        var clone = runner.Arguments[0];
        Assert.Contains("--depth", clone);
        Assert.Contains("--no-recurse-submodules", clone);
        Assert.Contains("http.followRedirects=false", clone);
        Assert.Contains("filter.lfs.required=false", clone);
        Assert.Contains("--branch", clone);
        Assert.Equal(SimulatedGitRunner.Commit, Assert.Single(observed).ObservedVersion);
    }

    /// <summary>Verifies a full commit selector is fetched shallowly and checked out detached.</summary>
    [Fact]
    public async Task Git_FullCommit_IsFetchedShallowAndPinned()
    {
        using var directory = new TemporaryDirectory();
        var runner = new SimulatedGitRunner();
        var service = new WorkerContentAcquisitionService(
            Options(directory.Path),
            new RejectingArtifactTransfer(),
            runner,
            new FixedResolver(IPAddress.Parse("93.184.216.34")));

        var observed = await service.AcquireAndInstallAsync(
            Command(),
            [Reference("git") with
            {
                Identifier = "https://github.com/example/repository.git",
                VersionIdentifier = SimulatedGitRunner.Commit,
            }],
            "cs2",
            Path.Combine(directory.Path, "install"),
            CancellationToken.None);

        Assert.Contains(runner.Arguments, arguments => arguments.Contains("fetch")
                                                      && arguments.Contains("--depth")
                                                      && arguments.Contains(SimulatedGitRunner.Commit));
        Assert.Contains(runner.Arguments, arguments => arguments.Contains("checkout")
                                                      && arguments.Contains("--detach"));
        Assert.Equal(SimulatedGitRunner.Commit, Assert.Single(observed).ObservedVersion);
    }

    /// <summary>Verifies credentials, custom ports, and malformed refs are permanent validation failures.</summary>
    /// <param name="repository">Unsafe repository URL.</param>
    /// <param name="selector">Optional malformed reference.</param>
    [Theory]
    [InlineData("https://user:token@github.com/example/repository.git", null)]
    [InlineData("https://github.com:8443/example/repository.git", null)]
    [InlineData("https://github.com/example/repository.git", "../main")]
    public async Task Git_UnsafeSourceOrRef_IsRejected(string repository, string? selector)
    {
        using var directory = new TemporaryDirectory();
        var service = new WorkerContentAcquisitionService(
            Options(directory.Path),
            new RejectingArtifactTransfer(),
            new RejectingProcessRunner(),
            new FixedResolver(IPAddress.Parse("93.184.216.34")));

        var exception = await Assert.ThrowsAsync<ContentAcquisitionException>(() => service.AcquireAndInstallAsync(
            Command(),
            [Reference("git") with { Identifier = repository, VersionIdentifier = selector }],
            "cs2",
            Path.Combine(directory.Path, "install"),
            CancellationToken.None));

        Assert.False(exception.Retryable);
    }

    /// <summary>Verifies the worker accepts the API's maximum Git ref length.</summary>
    [Fact]
    public async Task Git_RefAtMaximumLength_IsAccepted()
    {
        using var directory = new TemporaryDirectory();
        var runner = new SimulatedGitRunner();
        var service = new WorkerContentAcquisitionService(
            Options(directory.Path),
            new RejectingArtifactTransfer(),
            runner,
            new FixedResolver(IPAddress.Parse("93.184.216.34")));
        var selector = "branch-" + new string('a', 249);

        var observed = await service.AcquireAndInstallAsync(
            Command(),
            [Reference("git") with
            {
                Identifier = "https://github.com/example/repository.git",
                VersionIdentifier = selector,
            }],
            "cs2",
            Path.Combine(directory.Path, "install"),
            CancellationToken.None);

        Assert.Equal(256, selector.Length);
        Assert.Contains(selector, runner.Arguments[0]);
        Assert.Single(observed);
    }

    /// <summary>Verifies the worker rejects a Git ref beyond the API's maximum length.</summary>
    [Fact]
    public async Task Git_RefBeyondMaximumLength_IsRejected()
    {
        using var directory = new TemporaryDirectory();
        var runner = new RejectingProcessRunner();
        var service = new WorkerContentAcquisitionService(
            Options(directory.Path),
            new RejectingArtifactTransfer(),
            runner,
            new FixedResolver(IPAddress.Parse("93.184.216.34")));
        var selector = "branch-" + new string('a', 250);

        var exception = await Assert.ThrowsAsync<ContentAcquisitionException>(() => service.AcquireAndInstallAsync(
            Command(),
            [Reference("git") with
            {
                Identifier = "https://github.com/example/repository.git",
                VersionIdentifier = selector,
            }],
            "cs2",
            Path.Combine(directory.Path, "install"),
            CancellationToken.None));

        Assert.Equal(257, selector.Length);
        Assert.Equal("git-ref-invalid", exception.ErrorCode);
        Assert.Equal(0, runner.InvocationCount);
    }

    /// <summary>Verifies Git metadata cleanup failure prevents installation success.</summary>
    [Fact]
    public async Task Git_MetadataCleanupFailure_IsPermanentAndPreventsSuccess()
    {
        using var directory = new TemporaryDirectory();
        var service = new WorkerContentAcquisitionService(
            Options(directory.Path),
            new RejectingArtifactTransfer(),
            new SimulatedGitRunner(),
            new FixedResolver(IPAddress.Parse("93.184.216.34")),
            new SelectiveFailingCleaner(path => Path.GetFileName(path) == ".git"));

        var exception = await Assert.ThrowsAsync<ContentAcquisitionException>(() => service.AcquireAndInstallAsync(
            Command(),
            [Reference("git") with { Identifier = "https://github.com/example/repository.git" }],
            "cs2",
            Path.Combine(directory.Path, "install"),
            CancellationToken.None));

        Assert.False(exception.Retryable);
        Assert.Equal("git-metadata-cleanup-failed", exception.ErrorCode);
    }

    /// <summary>Verifies staging cleanup failure turns an otherwise successful installation into a failure.</summary>
    [Fact]
    public async Task StagingCleanupFailure_DoesNotReportSuccessfulInstallation()
    {
        using var directory = new TemporaryDirectory();
        var archive = Path.Combine(directory.Path, "source.zip");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            using var writer = new StreamWriter(zip.CreateEntry("game/csgo/addons/example/plugin.dll").Open());
            await writer.WriteAsync("plugin");
        }
        var checksum = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(archive)));
        var service = new WorkerContentAcquisitionService(
            Options(directory.Path),
            new RecordingArtifactTransfer(archive),
            new RejectingProcessRunner(),
            new FixedResolver(IPAddress.Parse("93.184.216.34")),
            new SelectiveFailingCleaner(_ => true));

        var exception = await Assert.ThrowsAsync<ContentAcquisitionException>(() => service.AcquireAndInstallAsync(
            Command(),
            [Reference("uploaded-artifact") with { ArtifactIdentifier = "artifact-1", ChecksumSha256 = checksum }],
            "cs2",
            Path.Combine(directory.Path, "install"),
            CancellationToken.None));

        Assert.Equal("content-cleanup-failed", exception.ErrorCode);
    }

    /// <summary>Verifies a cleanup failure records evidence without replacing the primary provider failure.</summary>
    [Fact]
    public async Task CleanupFailure_DoesNotMaskPrimaryProviderFailure()
    {
        using var directory = new TemporaryDirectory();
        var service = new WorkerContentAcquisitionService(
            Options(directory.Path),
            new RejectingArtifactTransfer(),
            new RejectingProcessRunner(),
            new FixedResolver(IPAddress.Parse("93.184.216.34")),
            new SelectiveFailingCleaner(_ => true));

        var exception = await Assert.ThrowsAsync<ContentAcquisitionException>(() => service.AcquireAndInstallAsync(
            Command(),
            [Reference("steam-workshop")],
            "cs2",
            Path.Combine(directory.Path, "install"),
            CancellationToken.None));

        Assert.Equal("provider-incompatible", exception.ErrorCode);
        Assert.Equal("IOException", exception.Data["ProviderStagingCleanupFailure"]);
    }

    private static WorkerContentReference Reference(string source)
        => new()
        {
            Source = source,
            Identifier = "source-1",
            DisplayName = "Example",
            InstallerIdentifier = "cs2-plugin",
        };

    private static WorkerCommandClaimResponse Command()
        => new() { CommandId = Guid.NewGuid() };

    private static CloudWorkerOptions Options(string root)
        => new(
            Path.Combine(root, "bootstrap.json"),
            Path.Combine(root, "state"),
            Path.Combine(root, "runtime"),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1),
            true,
            true,
            new Arma3WorkerOptions("steamcmd", "anonymous", null, null, "arma3server_x64", TimeSpan.FromSeconds(30)))
        {
            Git = new GitContentOptions(
                "git",
                new HashSet<string>(["github.com"], StringComparer.OrdinalIgnoreCase),
                1024 * 1024,
                TimeSpan.FromSeconds(30)),
        };

    private sealed class RecordingArtifactTransfer(string sourcePath) : ICloudWorkerStorageTransfer
    {
        public string? ArtifactIdentifier { get; private set; }

        public Task DownloadContentArtifactAsync(
            WorkerCommandClaimResponse command,
            string artifactIdentifier,
            string destinationPath,
            CancellationToken cancellationToken)
        {
            ArtifactIdentifier = artifactIdentifier;
            File.Copy(sourcePath, destinationPath);
            return Task.CompletedTask;
        }

        public Task ResetPersistentOverlayAsync(string deploymentRoot, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task ApplyPersistentSnapshotAsync(
            WorkerCommandClaimResponse command,
            string deploymentRoot,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task PublishPreservationAsync(
            WorkerCommandClaimResponse command,
            string deploymentRoot,
            CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class RejectingArtifactTransfer : ICloudWorkerStorageTransfer
    {
        public Task ResetPersistentOverlayAsync(string deploymentRoot, CancellationToken cancellationToken)
            => throw new InvalidOperationException();

        public Task ApplyPersistentSnapshotAsync(
            WorkerCommandClaimResponse command,
            string deploymentRoot,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException();

        public Task PublishPreservationAsync(
            WorkerCommandClaimResponse command,
            string deploymentRoot,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException();
    }

    private sealed class FixedResolver(params IPAddress[] addresses) : IHostAddressResolver
    {
        public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken)
            => Task.FromResult(addresses);
    }

    private sealed class RejectingProcessRunner : IProcessCommandRunner
    {
        public int InvocationCount { get; private set; }

        public Task<ProcessCommandResult> RunAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken)
        {
            InvocationCount++;
            throw new InvalidOperationException("Process execution was not expected.");
        }
    }

    private sealed class SimulatedGitRunner : IProcessCommandRunner
    {
        public const string Commit = "0123456789abcdef0123456789abcdef01234567";
        public List<string[]> Arguments { get; } = [];

        public Task<ProcessCommandResult> RunAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken)
        {
            var arguments = startInfo.ArgumentList.ToArray();
            Arguments.Add(arguments);
            if (arguments.Contains("clone"))
            {
                var checkout = arguments[^1];
                Directory.CreateDirectory(Path.Combine(checkout, ".git"));
                var plugin = Path.Combine(checkout, "game", "csgo", "addons", "example");
                Directory.CreateDirectory(plugin);
                File.WriteAllText(Path.Combine(plugin, "plugin.dll"), "plugin");
            }
            return Task.FromResult(new ProcessCommandResult(true, 0, arguments.Contains("rev-parse") ? Commit : "", ""));
        }
    }

    private sealed class SelectiveFailingCleaner(Func<string, bool> shouldFail) : IContentDirectoryCleaner
    {
        private readonly ContentDirectoryCleaner _inner = new();

        /// <inheritdoc />
        public void DeleteRequired(string path)
        {
            if (shouldFail(path)) throw new IOException("Simulated cleanup failure.");
            _inner.DeleteRequired(path);
        }
    }
}

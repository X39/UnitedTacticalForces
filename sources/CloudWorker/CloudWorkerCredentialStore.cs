using System.Text.Json;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>
/// Persists the post-registration credential only on the dedicated worker volume.
/// </summary>
public sealed class CloudWorkerCredentialStore(string stateDirectory)
{
    private const string CredentialFileName = "worker-credential";

    public string CredentialPath => Path.Combine(Path.GetFullPath(stateDirectory), CredentialFileName);

    public async Task<StoredWorkerCredential?> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(CredentialPath))
            return null;
        RestrictedFilePermissions.EnsureOwnerOnlyReadable(CredentialPath, "Persisted worker credential");
        await using var stream = new FileStream(
            CredentialPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var value = await JsonSerializer.DeserializeAsync<StoredWorkerCredential>(
                stream,
                CloudWorkerJson.Options,
                cancellationToken)
            .ConfigureAwait(false)
                    ?? throw new InvalidDataException("Persisted worker credential metadata is empty.");
        Validate(value);
        return value;
    }

    public async Task SaveAsync(StoredWorkerCredential credential, CancellationToken cancellationToken = default)
    {
        Validate(credential);
        var directory = Path.GetDirectoryName(CredentialPath)!;
        var json = JsonSerializer.Serialize(credential, CloudWorkerJson.Options);
        using var temporary = await RestrictedTemporaryFile
            .CreateAsync(directory, CredentialFileName, ".tmp", json, cancellationToken)
            .ConfigureAwait(false);
        File.Move(temporary.Path, CredentialPath, true);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(CredentialPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    private static void Validate(StoredWorkerCredential value)
    {
        if (value.Connection is null)
            throw new InvalidDataException("Persisted worker connection identity is missing.");
        value.Connection.Validate();
        if (string.IsNullOrWhiteSpace(value.Credential) || value.Credential.Length > 512)
            throw new InvalidDataException("Persisted worker credential is invalid.");
        if (value.ExpiresAt == default || value.HeartbeatIntervalSeconds <= 0)
            throw new InvalidDataException("Persisted worker credential metadata is invalid.");
        if (value.IssuedAt is { } issuedAt && issuedAt >= value.ExpiresAt)
            throw new InvalidDataException("Persisted worker credential lifetime is invalid.");
    }
}

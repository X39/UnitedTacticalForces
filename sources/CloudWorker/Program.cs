using System.Runtime.Loader;
using X39.UnitedTacticalForces.CloudWorker;

var options = CloudWorkerOptions.FromArgs(args);
using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};
AssemblyLoadContext.Default.Unloading += _ => shutdown.Cancel();

try
{
    var credentialStore = new CloudWorkerCredentialStore(options.StateDirectory);
    var storedCredential = await credentialStore.ReadAsync(shutdown.Token).ConfigureAwait(false);
    CloudWorkerBootstrapConfig? bootstrap = null;
    CloudWorkerConnectionConfig connection;
    if (storedCredential is null)
    {
        bootstrap = await CloudWorkerBootstrapConfig
            .LoadAsync(options.BootstrapPath, shutdown.Token)
            .ConfigureAwait(false);
        connection = bootstrap.ToConnectionConfig();
    }
    else
    {
        connection = storedCredential.Connection;
    }
    using var controlHandler = new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        ConnectTimeout = TimeSpan.FromSeconds(15),
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    };
    using var controlHttpClient = new HttpClient(controlHandler)
    {
        Timeout = TimeSpan.FromSeconds(45),
    };
    using var storageHandler = new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        ConnectTimeout = TimeSpan.FromSeconds(15),
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    };
    using var storageHttpClient = new HttpClient(storageHandler)
    {
        // Snapshot and preservation bodies can legitimately take longer than a control request.
        // Their command/worker cancellation token remains the transfer deadline.
        Timeout = Timeout.InfiniteTimeSpan,
    };
    var executor = new Arma3WorkerCommandExecutor(
        connection,
        options,
        storageTransfer: new CloudWorkerStorageTransfer(
            storageHttpClient,
            credentialStore,
            connection,
            options.StateDirectory));
    var runtime = new CloudWorkerRuntime(
        new CloudWorkerApiClient(controlHttpClient),
        credentialStore,
        new FlatFileWorkerCommandJournal(options.StateDirectory),
        executor);
    return await runtime.RunAsync(connection, bootstrap, options, shutdown.Token).ConfigureAwait(false);
}
catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
{
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(WorkerSecretRedactor.Redact(ex.Message) ?? "Cloud worker failed.");
    return 1;
}

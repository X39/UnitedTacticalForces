using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var db = builder.AddPostgres("pg-database")
    .WithDataVolume()
    .AddDatabase("database");
var api = builder.AddProject<Projects.Api>("api")
    .WithReference(db)
    .WaitFor(db);
var app = builder.AddProject<Projects.WebApp>("app")
    .WaitFor(api);

AddDevelopmentCloudWorker(builder, api);

builder.Build()
    .Run();

static void AddDevelopmentCloudWorker(
    IDistributedApplicationBuilder builder,
    IResourceBuilder<ProjectResource> api)
{
    const string sectionName = "Hosting:DevelopmentWorker";
    var section = builder.Configuration.GetSection(sectionName);
    if (!section.GetValue<bool>("Enabled"))
        return;

    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            $"{sectionName} can only be enabled in the Development environment.");
    }

    var configuredBootstrapPath = section["BootstrapPath"];
    if (string.IsNullOrWhiteSpace(configuredBootstrapPath))
    {
        throw new InvalidOperationException(
            $"{sectionName}:BootstrapPath must identify a worker bootstrap JSON file when enabled.");
    }

    var bootstrapPath = Path.IsPathFullyQualified(configuredBootstrapPath)
        ? Path.GetFullPath(configuredBootstrapPath)
        : Path.GetFullPath(configuredBootstrapPath, builder.AppHostDirectory);
    if (!File.Exists(bootstrapPath))
    {
        throw new FileNotFoundException(
            "The configured development worker bootstrap JSON file does not exist.",
            bootstrapPath);
    }

    var repositoryRoot = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", ".."));
    var dockerfilePath = Path.Combine(repositoryRoot, "sources", "CloudWorker", "Dockerfile");
    if (!File.Exists(dockerfilePath))
        throw new FileNotFoundException("The CloudWorker Dockerfile does not exist.", dockerfilePath);

    builder.AddDockerfile(
            "cloud-worker-development",
            repositoryRoot,
            Path.GetRelativePath(repositoryRoot, dockerfilePath))
        .WithBindMount(
            bootstrapPath,
            "/run/secrets/utf-worker-bootstrap.json",
            isReadOnly: true)
        .WithVolume(
            "utf-cloud-worker-development-state",
            "/var/lib/united-tactical-forces/worker")
        .WithVolume(
            "utf-cloud-worker-development-runtime",
            "/srv/united-tactical-forces/runtime")
        .WithEnvironment(
            "UTF_WORKER_ALLOW_INSECURE_HTTP",
            section.GetValue<bool>("AllowInsecureHttp") ? "true" : "false")
        .WaitFor(api);
}

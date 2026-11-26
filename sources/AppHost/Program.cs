using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var db = builder.AddPostgres("pg-database")
    .WithDataVolume()
    .AddDatabase("database");
var api = builder.AddProject<Projects.Api>("api")
    .WithReference(db)
    .WaitFor(db);
if (builder.Configuration.GetValue<bool>("Uat:Enabled"))
{
    // Manual UAT profile (scripts/Start-Uat.ps1): dev login bypass, emulated Hetzner, seeded fleet servers.
    api.WithEnvironment("Testing__AuthorizationBypass__Enabled", "true")
        .WithEnvironment("Testing__UatScenarios__Enabled", "true")
        .WithEnvironment("Hosting__Providers__Hetzner__Emulated", "true")
        // The control plane is paused unless configured otherwise; UAT needs lifecycle actions to run.
        .WithEnvironment("Hosting__ControlPlane__Paused", "false")
        // These values only speed up the server automation for UAT so a warning and the stop that follows it can be
        // observed within minutes; production keeps the defaults of Hosting:Automation.
        .WithEnvironment("Hosting__Automation__EvaluationInterval", "00:00:05")
        .WithEnvironment("Hosting__Automation__WarningLead", "00:01:00")
        .WithEnvironment("Hosting__Automation__IdleMinutesMin", "1")
        // The emulated worker claims commands sooner, so an automatic stop completes quickly in UAT.
        .WithEnvironment("Testing__UatScenarios__WorkerChannelEmulator__ClaimPollInterval", "00:00:03")
        // A start renders the worker bootstrap, which needs Steam credentials for Arma 3. The emulated provider never
        // uses them, so placeholders keep a started UAT server from failing at the preflight and keep real credentials
        // of a developer machine out of the UAT stack.
        .WithEnvironment("Steam__Username", "uat-emulated")
        .WithEnvironment("Steam__Password", "uat-emulated-placeholder");

    // Mailpit is the UAT inbox for cost alert emails (http://localhost:8025) and is never part of a deployment.
    var mailpit = builder.AddContainer("mailpit", "axllent/mailpit", "v1.34.0")
        .WithHttpEndpoint(port: 8025, targetPort: 8025, name: "ui")
        .WithEndpoint(port: 1025, targetPort: 1025, scheme: "tcp", name: "smtp");
    var smtp = mailpit.GetEndpoint("smtp");
    api.WithEnvironment("Email__Smtp__Host", smtp.Property(EndpointProperty.Host))
        .WithEnvironment("Email__Smtp__Port", smtp.Property(EndpointProperty.Port))
        // Mailpit offers no TLS; this value is only acceptable for a local development sink.
        .WithEnvironment("Email__Smtp__SecureSocket", "None")
        .WithEnvironment("Email__Smtp__FromAddress", "cost-alerts@utf.test")
        .WaitFor(mailpit);
}
var app = builder.AddProject<Projects.WebApp>("app")
    .WaitFor(api);
if (builder.Configuration.GetValue<bool>("Uat:Enabled"))
{
    // The classic Blazor host only serves static assets to the React dev server; keep its links off the dashboard front page.
    app.WithUrls(context =>
    {
        foreach (var url in context.Urls)
            url.DisplayLocation = UrlDisplayLocation.DetailsOnly;
    });
}
var reactApp = builder.AddViteApp("react-app", "../ReactApp")
    .WithNpm(installCommand: "ci")
    // vite.config.ts pins the dev server to port 5173 (strictPort), so expose exactly that port unproxied.
    .WithEndpoint(
        "http",
        endpoint =>
        {
            endpoint.Port = 5173;
            endpoint.TargetPort = 5173;
            endpoint.IsProxied = false;
            endpoint.UriScheme = "http";
        })
    .WithUrlForEndpoint(
        "http",
        url =>
        {
            url.Url = "/next/command/servers";
            url.DisplayText = "Canary UI (fleet)";
        })
    .WithReference(api)
    .WithReference(app)
    .WaitFor(api)
    .WaitFor(app);

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
        throw new FileNotFoundException("The shared CloudWorker Dockerfile does not exist.", dockerfilePath);

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

var builder = DistributedApplication.CreateBuilder(args);

var db = builder.AddPostgres("pg-database")
    .WithDataVolume()
    .AddDatabase("database");
var api = builder.AddProject<Projects.Api>("api")
    .WithReference(db)
    .WaitFor(db);
var app = builder.AddProject<Projects.WebApp>("app")
    .WaitFor(api);

builder.Build()
    .Run();

var builder = DistributedApplication.CreateBuilder(args);

var keycloak = builder
    .AddKeycloak("keycloak", port: 8888)
    .WithOtlpExporter()
    .WithDataVolume();

var cache = builder
    .AddGarnet("cache");

var apiService = builder
    .AddProject<Projects.blog_keycloak_series_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints()
    .WithReference(keycloak)
    .WithReference(cache)
    .WaitFor(keycloak)
    .WaitFor(cache);

builder
    .AddProject<Projects.blog_keycloak_series_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WithReference(keycloak)
    .WaitFor(apiService)
    .WaitFor(keycloak);

builder.Build().Run();
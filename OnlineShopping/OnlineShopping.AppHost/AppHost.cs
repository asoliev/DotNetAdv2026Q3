var builder = DistributedApplication.CreateBuilder(args);

var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithImageTag("4-management")
    .WithManagementPlugin()
    .WithDataVolume();

builder.AddProject<Projects.IdentityService_Api>("identity-service", launchProfileName: null)
    .WithHttpEndpoint(port: 5003)
    .WithHttpHealthCheck("/health/ready")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("Telemetry__ExportEnabled", "true");

var catalog = builder.AddProject<Projects.CatalogService_Api>("catalog-service", launchProfileName: null)
    .WithHttpEndpoint(port: 5002)
    .WithHttpHealthCheck("/health/ready")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("Telemetry__ExportEnabled", "true")
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq);

var cart = builder.AddProject<Projects.CartService_Api>("cart-service", launchProfileName: null)
    .WithHttpEndpoint(port: 5001)
    .WithHttpHealthCheck("/health/ready")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("Telemetry__ExportEnabled", "true")
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq);

builder.AddProject<Projects.ApiGateway_Api>("api-gateway", launchProfileName: null)
    .WithHttpEndpoint(port: 5004)
    .WithHttpHealthCheck("/health/ready")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("Telemetry__ExportEnabled", "true")
    .WithEnvironment("Downstream__Catalog__BaseUrl", catalog.GetEndpoint("http"))
    .WithEnvironment("Downstream__Cart__BaseUrl", cart.GetEndpoint("http"))
    .WithReference(catalog)
    .WithReference(cart)
    .WaitFor(catalog)
    .WaitFor(cart);

await builder.Build().RunAsync().ConfigureAwait(false);
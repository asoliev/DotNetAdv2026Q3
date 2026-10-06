using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

using RabbitMQ.Client;

namespace ShoppingTelemetry;

public static class ShoppingHealthExtensions
{
    public static IHealthChecksBuilder AddShoppingHealthChecks(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);
    }

    public static IHealthChecksBuilder AddDependencyCheck(this IHealthChecksBuilder builder, string name, Func<IServiceProvider, CancellationToken, Task> probe)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(probe);
        return builder.Add(new HealthCheckRegistration(name,
            services => new DependencyHealthCheck(token => probe(services, token)),
            HealthStatus.Unhealthy, ["ready"], TimeSpan.FromSeconds(5)));
    }

    public static IHealthChecksBuilder AddShoppingRabbitMqCheck(this IHealthChecksBuilder checks, IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return checks.AddDependencyCheck("rabbitmq", async (_, token) =>
        {
            var factory = new ConnectionFactory
            {
                HostName = builder.Configuration["RabbitMq:Host"] ?? "localhost",
                RequestedConnectionTimeout = TimeSpan.FromSeconds(3),
                AutomaticRecoveryEnabled = false,
            };
            if (builder.Configuration["ConnectionStrings:rabbitmq"] is { } connectionString)
            {
                factory.Uri = new Uri(connectionString);
            }
            else
            {
                factory.UserName = builder.Configuration["RabbitMq:Username"] ?? "guest";
                factory.Password = builder.Configuration["RabbitMq:Password"] ?? "guest";
            }

            IConnection connection = await factory.CreateConnectionAsync(token).ConfigureAwait(false);
            await using (connection.ConfigureAwait(false))
            {
                if (!connection.IsOpen)
                {
                    throw new InvalidOperationException("Broker connection is not open.");
                }
            }
        });
    }

    public static IEndpointRouteBuilder MapShoppingHealthChecks(this IEndpointRouteBuilder application)
    {
        ArgumentNullException.ThrowIfNull(application);
        application.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("live"),
        }).AllowAnonymous();
        application.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = _ => true,
        }).AllowAnonymous();
        return application;
    }

    private sealed class DependencyHealthCheck(Func<CancellationToken, Task> probe) : IHealthCheck
    {
        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Dependency failures must become health status, never escape or expose sensitive exception details.")]
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                await probe(cancellationToken).ConfigureAwait(false);
                return HealthCheckResult.Healthy();
            }
            catch (Exception)
            {
                return HealthCheckResult.Unhealthy("Dependency unavailable.");
            }
        }
    }
}
using System.Diagnostics;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using RabbitMQ.Client;

namespace ShoppingTelemetry;

public static class ShoppingTelemetryExtensions
{
    public static IHostApplicationBuilder AddShoppingTelemetry(this IHostApplicationBuilder builder, string serviceName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        OpenTelemetryBuilder telemetryBuilder = builder.AddOpenTelemetry();
        telemetryBuilder
            .ConfigureResource(resource => resource
                .AddService(serviceName,
                    serviceVersion: typeof(ShoppingTelemetryExtensions).Assembly.GetName().Version?.ToString(),
                    serviceInstanceId: Guid.NewGuid().ToString())
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment.name"] = builder.Environment.EnvironmentName,
                }))
            .WithTracing(tracing => tracing
                .SetSampler(new ParentBasedSampler(new AlwaysOnSampler()))
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSource(RabbitMQActivitySource.PublisherSourceName)
                .AddSource(RabbitMQActivitySource.SubscriberSourceName)
                .AddSource("CartService.Dal.LiteDbCartRepository")
                .AddSource("CatalogService.Infrastructure.SqliteProductRepository")
                .AddSource("CatalogService.Infrastructure.SqliteCategoryRepository"))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(ShoppingTelemetryMetrics.MeterName));

        telemetryBuilder.WithLogging(
            configureBuilder: null,
            options =>
            {
                options.IncludeFormattedMessage = true;
                options.IncludeScopes = true;
                options.ParseStateValues = true;
            });

        if (bool.TryParse(builder.Configuration["Telemetry:ExportEnabled"], out var exportEnabled) && exportEnabled)
        {
            telemetryBuilder.UseOtlpExporter();
        }

        builder.Logging.AddFilter<OpenTelemetryLoggerProvider>(
            "CartService.Api.Middleware.AccessTokenLoggingMiddleware",
            LogLevel.None);

        return builder;
    }

    public static WebApplication UseTraceIdResponseHeader(this WebApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);

        application.Use(async (context, next) =>
        {
            string? traceId = Activity.Current?.TraceId.ToHexString();
            if (traceId is not null)
            {
                context.Response.OnStarting(static state =>
                {
                    (HttpResponse response, string currentTraceId) = ((HttpResponse Response, string TraceId))state;
                    response.Headers["X-Trace-Id"] = currentTraceId;
                    return Task.CompletedTask;
                }, (context.Response, traceId));
            }

            await next(context).ConfigureAwait(false);
        });

        return application;
    }
}

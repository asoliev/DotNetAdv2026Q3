using System.Collections.Concurrent;
using System.Diagnostics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

using ShoppingTelemetry;

namespace ApiGateway.Tests;

public sealed class ShoppingTelemetryTests
{
    private static readonly Action<ILogger, string, Exception?> LogOutcome =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(1), "Processing {Outcome}");
    private static readonly Action<ILogger, Exception?> LogSensitiveMarker =
        LoggerMessage.Define(LogLevel.Information, new EventId(2), "Sensitive test marker");

    [Fact]
    public async Task ApplicationProviderExportsResourceAndRespectsParentSampling()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["Telemetry:ExportEnabled"] = "false";
        builder.AddShoppingTelemetry("telemetry-test");
        var spans = new ConcurrentQueue<Activity>();
        builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing
            .AddProcessor(new SimpleActivityExportProcessor(new CaptureExporter<Activity>(spans.Enqueue))));
        using var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);
        using var source = new ActivitySource("CartService.Dal.LiteDbCartRepository");
        var traceId = ActivityTraceId.CreateRandom();
        var parent = new ActivityContext(traceId, ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, isRemote: true);
        using (var activity = source.StartActivity("sampled", ActivityKind.Internal, parent))
        {
            Assert.NotNull(activity);
            Assert.True(activity.Recorded);
        }

        using (var activity = source.StartActivity("unsampled", ActivityKind.Internal,
            new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None, isRemote: true)))
        {
            Assert.False(activity?.Recorded ?? false);
        }

        var exported = Assert.Single(spans);
        Assert.Equal(traceId, exported.TraceId);
        Assert.Equal(parent.SpanId, exported.ParentSpanId);
        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource().Attributes.ToDictionary();
        Assert.Equal("telemetry-test", resource["service.name"]);
        Assert.Equal(builder.Environment.EnvironmentName, resource["deployment.environment.name"]);
        Assert.NotNull(resource["service.version"]);
        Assert.True(Guid.TryParse(resource["service.instance.id"].ToString(), out _));
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ApplicationLoggingExportsCorrelationAndScopesButFiltersTokenDetails()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["Telemetry:ExportEnabled"] = "false";
        builder.AddShoppingTelemetry("telemetry-test");
        var records = new ConcurrentQueue<(string? Category, ActivityTraceId TraceId, ActivitySpanId SpanId, string? Message, bool HasScope, bool HasState)>();
        builder.Services.AddOpenTelemetry().WithLogging(logging => logging
            .AddProcessor(new SimpleLogRecordExportProcessor(new CaptureExporter<LogRecord>(record =>
            {
                var hasScope = false;
                record.ForEachScope((scope, _) =>
                {
                    foreach (var attribute in scope)
                    {
                        hasScope |= attribute.Key == "message.id" && Equals(attribute.Value, "safe-message-id");
                    }
                }, 0);
                records.Enqueue((record.CategoryName, record.TraceId, record.SpanId, record.FormattedMessage,
                    hasScope, record.Attributes?.Any(attribute => attribute.Key == "Outcome" && Equals(attribute.Value, "success")) == true));
            }))));
        using var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);
        var factory = host.Services.GetRequiredService<ILoggerFactory>();
        using var activity = new Activity("correlation-test");
        activity.Start();
        var logger = factory.CreateLogger("Safe.Telemetry.Test");
        using (logger.BeginScope(new Dictionary<string, object> { ["message.id"] = "safe-message-id" }))
        {
            LogOutcome(logger, "success", null);
            LogSensitiveMarker(factory.CreateLogger("CartService.Api.Middleware.AccessTokenLoggingMiddleware"), null);
        }

        var record = Assert.Single(records, record => record.Category == "Safe.Telemetry.Test");
        Assert.Equal(activity.TraceId, record.TraceId);
        Assert.Equal(activity.SpanId, record.SpanId);
        Assert.Equal("Processing success", record.Message);
        Assert.True(record.HasScope);
        Assert.True(record.HasState);
        Assert.DoesNotContain(records, record => record.Category == "CartService.Api.Middleware.AccessTokenLoggingMiddleware");
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    private sealed class CaptureExporter<T>(Action<T> capture) : BaseExporter<T>
        where T : class
    {
        public override ExportResult Export(in Batch<T> batch)
        {
            foreach (var item in batch)
            {
                capture(item);
            }

            return ExportResult.Success;
        }
    }
}
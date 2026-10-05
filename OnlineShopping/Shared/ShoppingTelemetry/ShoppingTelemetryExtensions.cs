using System.Diagnostics;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ShoppingTelemetry;

public static class ShoppingTelemetryExtensions
{
	public static IHostApplicationBuilder AddShoppingTelemetry(this IHostApplicationBuilder builder, string serviceName)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

		builder.AddOpenTelemetry()
			.ConfigureResource(resource => resource.AddService(serviceName))
			.WithTracing(tracing => tracing
				.AddAspNetCoreInstrumentation()
				.AddHttpClientInstrumentation())
			.WithMetrics(metrics => metrics
				.AddAspNetCoreInstrumentation()
				.AddRuntimeInstrumentation())
			.UseOtlpExporter();

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
					var (response, currentTraceId) = ((HttpResponse Response, string TraceId))state;
					response.Headers["X-Trace-Id"] = currentTraceId;
					return Task.CompletedTask;
				}, (context.Response, traceId));
			}

			await next(context).ConfigureAwait(false);
		});

		return application;
	}
}
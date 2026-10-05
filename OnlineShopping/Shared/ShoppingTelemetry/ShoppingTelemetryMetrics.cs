using System.Diagnostics.Metrics;

namespace ShoppingTelemetry;

public static class ShoppingTelemetryMetrics
{
    public const string MeterName = "ShoppingTelemetry";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> MessagingOperations = Meter.CreateCounter<long>("shopping.messaging.operations");
    private static readonly Histogram<double> MessageProcessingDuration = Meter.CreateHistogram<double>("shopping.messaging.processing.duration", "ms");

    public static void RecordMessagingOperation(string serviceName, string operation, string eventType, string outcome) =>
        MessagingOperations.Add(1,
            new KeyValuePair<string, object?>("service.name", serviceName),
            new KeyValuePair<string, object?>("messaging.operation.name", operation),
            new KeyValuePair<string, object?>("messaging.event.type", eventType),
            new KeyValuePair<string, object?>("outcome", outcome));

    public static void RecordMessageProcessingDuration(string eventType, string outcome, double durationMilliseconds) =>
        MessageProcessingDuration.Record(durationMilliseconds,
            new KeyValuePair<string, object?>("service.name", "cart-service"),
            new KeyValuePair<string, object?>("messaging.event.type", eventType),
            new KeyValuePair<string, object?>("outcome", outcome));
}

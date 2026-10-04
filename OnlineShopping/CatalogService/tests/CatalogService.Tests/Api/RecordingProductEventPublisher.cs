using System.Collections.Concurrent;

using CatalogService.Api.Messaging;

namespace CatalogService.Tests.Api;

/// <summary>
/// Replaces the RabbitMQ publisher in API tests and records every published event.
/// </summary>
public sealed class RecordingProductEventPublisher : IProductEventPublisher
{
    private readonly ConcurrentQueue<ProductChangedMessage> _upserted = new();
    private readonly ConcurrentQueue<Guid> _deleted = new();

    public IReadOnlyCollection<ProductChangedMessage> Upserted => _upserted;

    public IReadOnlyCollection<Guid> Deleted => _deleted;

    public Task PublishUpsertedAsync(ProductChangedMessage message, CancellationToken cancellationToken = default)
    {
        _upserted.Enqueue(message);
        return Task.CompletedTask;
    }

    public Task PublishDeletedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _deleted.Enqueue(id);
        return Task.CompletedTask;
    }
}

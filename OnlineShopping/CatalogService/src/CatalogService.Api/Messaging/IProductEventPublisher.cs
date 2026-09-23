using CatalogService.Domain;

namespace CatalogService.Api.Messaging;

public interface IProductEventPublisher
{
    Task PublishUpsertedAsync(ProductChangedMessage message, CancellationToken cancellationToken = default);

    Task PublishDeletedAsync(Guid id, CancellationToken cancellationToken = default);
}
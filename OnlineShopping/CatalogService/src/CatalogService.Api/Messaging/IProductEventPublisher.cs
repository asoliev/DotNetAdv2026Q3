namespace CatalogService.Api.Messaging;

internal interface IProductEventPublisher
{
    Task PublishUpsertedAsync(ProductChangedMessage message, CancellationToken cancellationToken = default);

    Task PublishDeletedAsync(Guid id, CancellationToken cancellationToken = default);
}

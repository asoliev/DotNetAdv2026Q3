namespace CartService.Api.Messaging;

internal sealed record ProductImageMessage(string Url, string? AltText);

internal sealed record ProductChangedMessage(Guid Id, string Name, string? Description, ProductImageMessage? Image, Guid CategoryId, decimal Price, int Amount);

internal sealed record ProductDeletedMessage(Guid Id);
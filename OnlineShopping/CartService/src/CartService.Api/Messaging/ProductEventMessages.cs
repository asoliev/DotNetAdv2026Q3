namespace CartService.Api.Messaging;

public sealed record ProductImageMessage(Uri Url, string? AltText);

public sealed record ProductChangedMessage(Guid Id, string Name, string? Description, ProductImageMessage? Image, Guid CategoryId, decimal Price, int Amount);

public sealed record ProductDeletedMessage(Guid Id);

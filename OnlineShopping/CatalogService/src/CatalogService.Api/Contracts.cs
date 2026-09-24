namespace CatalogService.Api;

internal sealed record CategoryResponse(Guid Id, string Name, ImageResponse? Image, Guid? ParentCategoryId);

internal sealed record CategoryUpsertRequest(string Name, ImageRequest? Image, Guid? ParentCategoryId);

internal sealed record ProductResponse(Guid Id, string Name, string? Description, ImageResponse? Image, Guid CategoryId, decimal Price, int Amount);

internal sealed record ProductUpsertRequest(string Name, string? Description, ImageRequest? Image, Guid CategoryId, decimal Price, int Amount);

internal sealed record ImageRequest(string Url, string? AltText);

internal sealed record ImageResponse(string Url, string? AltText);

internal sealed record PageResponse<T>(IReadOnlyList<T> Items, int TotalCount, int PageNumber, int PageSize);

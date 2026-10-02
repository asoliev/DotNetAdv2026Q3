using System.Text.Json.Serialization;

namespace CatalogService.Api;

public sealed record CategoryResponse(Guid Id, string Name, ImageResponse? Image, Guid? ParentCategoryId);

public sealed record CategoryUpsertRequest(string Name, ImageRequest? Image, Guid? ParentCategoryId);

public sealed record ProductResponse(Guid Id, string Name, string? Description, ImageResponse? Image, Guid CategoryId, decimal Price, int Amount);

public sealed record ProductUpsertRequest(
    string Name,
    string? Description,
    ImageRequest? Image,
    [property: JsonRequired] Guid CategoryId,
    [property: JsonRequired] decimal Price,
    [property: JsonRequired] int Amount);

public sealed record ImageRequest(string Url, string? AltText);

public sealed record ImageResponse(string Url, string? AltText);

public sealed record PageResponse<T>(IReadOnlyList<T> Items, int TotalCount, int PageNumber, int PageSize);

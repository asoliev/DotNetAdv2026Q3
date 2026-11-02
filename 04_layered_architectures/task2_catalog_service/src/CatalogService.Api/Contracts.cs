using System.ComponentModel.DataAnnotations;

namespace CatalogService.Api;

/// <summary>A navigable API link.</summary>
public sealed record LinkResponse(string Href, string Rel, string Method);

/// <summary>Catalog category returned by the API.</summary>
public sealed record CategoryResponse(Guid Id, string Name, ImageResponse? Image, Guid? ParentCategoryId, IReadOnlyList<LinkResponse> Links);

/// <summary>Category details submitted to create or replace a category.</summary>
public sealed record CategoryUpsertRequest(
    [property: Required, StringLength(50)] string Name,
    ImageRequest? Image,
    Guid? ParentCategoryId);

/// <summary>Catalog product returned by the API.</summary>
public sealed record ProductResponse(Guid Id, string Name, string? Description, ImageResponse? Image, Guid CategoryId, decimal Price, int Amount, IReadOnlyList<LinkResponse> Links);

/// <summary>Product details submitted to create or replace a product.</summary>
public sealed record ProductUpsertRequest(
    [property: Required, StringLength(50)] string Name,
    string? Description,
    ImageRequest? Image,
    Guid CategoryId,
    [property: Range(typeof(decimal), "0.01", "79228162514264337593543950335")] decimal Price,
    [property: Range(1, int.MaxValue)] int Amount);

/// <summary>Image details submitted with a catalog resource.</summary>
public sealed record ImageRequest([property: Required] string Url, string? AltText);

/// <summary>Image details returned for a catalog resource.</summary>
public sealed record ImageResponse(string Url, string? AltText);

/// <summary>A page of catalog results and navigation links.</summary>
public sealed record PageResponse<T>(IReadOnlyList<T> Items, int TotalCount, int PageNumber, int PageSize, IReadOnlyList<LinkResponse> Links);

using CatalogService.Application;
using CatalogService.Domain;

namespace CatalogService.Api;

/// <summary>
/// Maps products to API responses for the controllers that return them.
/// </summary>
internal static class ProductResponseMapper
{
    public static ProductResponse ToResponse(Product product) => new(product.Id, product.Name, product.Description, ToResponse(product.Image), product.CategoryId, product.Price, product.Amount);

    public static PageResponse<ProductResponse> ToPageResponse(PagedResult<Product> page) => new([.. page.Items.Select(ToResponse)], page.TotalCount, page.PageNumber, page.PageSize);

    private static ImageResponse? ToResponse(ImageInfo? image) => image is null ? null : new ImageResponse(image.Url, image.AltText);
}

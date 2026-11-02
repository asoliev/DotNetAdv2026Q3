using Asp.Versioning;
using CatalogService.Application;
using CatalogService.Domain;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api;

/// <summary>
/// Manages catalog products.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/products")]
public sealed class ProductsController : ControllerBase
{
    private readonly ProductService _productService;

    public ProductsController(ProductService productService)
    {
        _productService = productService;
    }

    /// <summary>
    /// Returns a product by identifier.
    /// </summary>
    /// <remarks>The response includes hypermedia links for related actions.</remarks>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var product = await _productService.GetByIdAsync(id, cancellationToken);
        return product is null ? NotFound() : Ok(Map(product));
    }

    /// <summary>
    /// Returns a page of products.
    /// </summary>
    /// <remarks>The response includes hypermedia links to navigate available pages.</remarks>
    [HttpGet]
    public Task<ActionResult<PageResponse<ProductResponse>>> GetPage([FromQuery] Guid? categoryId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        return GetPageInternal(categoryId, pageNumber, pageSize, cancellationToken);
    }

    /// <summary>
    /// Returns a page of products for a specific category.
    /// </summary>
    [HttpGet("~/api/v{version:apiVersion}/categories/{categoryId:guid}/products")]
    public Task<ActionResult<PageResponse<ProductResponse>>> GetPageByCategory(Guid categoryId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        return GetPageInternal(categoryId, pageNumber, pageSize, cancellationToken);
    }

    /// <summary>
    /// Creates a product.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ProductResponse>> Create([FromBody] ProductUpsertRequest request, CancellationToken cancellationToken)
    {
        var product = new Product(Guid.NewGuid(), request.Name, request.Description, MapImage(request.Image), request.CategoryId, request.Price, request.Amount);
        await _productService.AddAsync(product, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = product.Id, version = Version }, Map(product));
    }

    /// <summary>
    /// Updates a product.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] ProductUpsertRequest request, CancellationToken cancellationToken)
    {
        var product = new Product(id, request.Name, request.Description, MapImage(request.Image), request.CategoryId, request.Price, request.Amount);
        return await _productService.UpdateAsync(product, cancellationToken) ? NoContent() : NotFound();
    }

    /// <summary>
    /// Deletes a product.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        return await _productService.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();
    }

    private async Task<ActionResult<PageResponse<ProductResponse>>> GetPageInternal(Guid? categoryId, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        if (categoryId is not null && !await _productService.CategoryExistsAsync(categoryId.Value, cancellationToken))
        {
            return NotFound();
        }

        var page = await _productService.GetPageAsync(categoryId, pageNumber, pageSize, cancellationToken);
        return Ok(new PageResponse<ProductResponse>(page.Items.Select(Map).ToList(), page.TotalCount, page.PageNumber, page.PageSize, BuildPageLinks(categoryId, page)));
    }

    private string Version => HttpContext.GetRequestedApiVersion()?.ToString("VVV") ?? "1";

    private ProductResponse Map(Product product)
    {
        var routeValues = new { id = product.Id, version = Version };
        var links = new List<LinkResponse>
        {
            new(Url.ActionLink(nameof(GetById), "Products", routeValues)!, "self", "GET"),
            new(Url.ActionLink(nameof(Update), "Products", routeValues)!, "update", "PUT"),
            new(Url.ActionLink(nameof(Delete), "Products", routeValues)!, "delete", "DELETE"),
            new(Url.ActionLink(nameof(CategoriesController.GetById), "Categories", new { id = product.CategoryId, version = Version })!, "category", "GET")
        };

        return new ProductResponse(product.Id, product.Name, product.Description, MapImage(product.Image), product.CategoryId, product.Price, product.Amount, links);
    }

    private List<LinkResponse> BuildPageLinks(Guid? categoryId, PagedResult<Product> page)
    {
        string PageLink(int number) => Url.ActionLink(nameof(GetPage), "Products", new
        {
            version = Version,
            categoryId,
            pageNumber = number,
            pageSize = page.PageSize
        })!;

        var links = new List<LinkResponse> { new(PageLink(page.PageNumber), "self", "GET") };
        var totalPages = (int)Math.Ceiling(page.TotalCount / (double)page.PageSize);

        if (page.PageNumber > 1)
        {
            links.Add(new LinkResponse(PageLink(page.PageNumber - 1), "prev", "GET"));
        }

        if (page.PageNumber < totalPages)
        {
            links.Add(new LinkResponse(PageLink(page.PageNumber + 1), "next", "GET"));
        }

        return links;
    }

    private static ImageResponse? MapImage(ImageInfo? image)
    {
        return image is null ? null : new ImageResponse(image.Url, image.AltText);
    }

    private static ImageInfo? MapImage(ImageRequest? image)
    {
        return image is null ? null : new ImageInfo(image.Url, image.AltText);
    }
}

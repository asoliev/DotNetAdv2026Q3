using Asp.Versioning;

using CatalogService.Application;
using CatalogService.Domain;

using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api;

/// <summary>
/// Lists the products of a category.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/categories/{categoryId:guid}/products")]
public sealed class CategoryProductsController(ProductService productService, ICategoryRepository categoryRepository) : ControllerBase
{
    private readonly ProductService _productService = productService;
    private readonly ICategoryRepository _categoryRepository = categoryRepository;

    /// <summary>
    /// Returns a page of products for a specific category.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PageResponse<ProductResponse>>> GetPage(Guid categoryId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        if (!await _categoryRepository.ExistsAsync(categoryId, cancellationToken).ConfigureAwait(false))
        {
            return NotFound();
        }

        PagedResult<Product> page = await _productService.GetPageAsync(categoryId, pageNumber, pageSize, cancellationToken).ConfigureAwait(false);
        return Ok(ProductResponseMapper.ToPageResponse(page));
    }
}

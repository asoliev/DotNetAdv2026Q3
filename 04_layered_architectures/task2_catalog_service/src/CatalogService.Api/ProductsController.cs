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
    private readonly IProductRepository _productRepository;
    private readonly ICategoryRepository _categoryRepository;

    public ProductsController(ProductService productService, IProductRepository productRepository, ICategoryRepository categoryRepository)
    {
        _productService = productService;
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
    }

    /// <summary>
    /// Returns a product by identifier.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(id, cancellationToken);
        return product is null ? NotFound() : Ok(Map(product));
    }

    /// <summary>
    /// Returns a page of products.
    /// </summary>
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
        try
        {
            var product = new Product(Guid.NewGuid(), request.Name, request.Description, MapImage(request.Image), request.CategoryId, request.Price, request.Amount);
            await _productService.AddAsync(product, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = product.Id, version = "1" }, Map(product));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    /// <summary>
    /// Updates a product.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] ProductUpsertRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var product = new Product(id, request.Name, request.Description, MapImage(request.Image), request.CategoryId, request.Price, request.Amount);
            await _productService.UpdateAsync(product, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    /// <summary>
    /// Deletes a product.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(id, cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        await _productService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    private async Task<ActionResult<PageResponse<ProductResponse>>> GetPageInternal(Guid? categoryId, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        if (categoryId is not null && !await _categoryRepository.ExistsAsync(categoryId.Value, cancellationToken))
        {
            return NotFound();
        }

        var page = await _productService.GetPageAsync(categoryId, pageNumber, pageSize, cancellationToken);
        return Ok(new PageResponse<ProductResponse>(page.Items.Select(Map).ToList(), page.TotalCount, page.PageNumber, page.PageSize));
    }

    private static ProductResponse Map(Product product)
    {
        return new ProductResponse(product.Id, product.Name, product.Description, MapImage(product.Image), product.CategoryId, product.Price, product.Amount);
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

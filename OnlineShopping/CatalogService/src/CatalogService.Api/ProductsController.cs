using Asp.Versioning;

using CatalogService.Api.Messaging;
using CatalogService.Application;
using CatalogService.Domain;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using ShoppingAuth;

namespace CatalogService.Api;

/// <summary>
/// Manages catalog products.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/products")]
internal sealed class ProductsController(ProductService productService, IProductRepository productRepository, ICategoryRepository categoryRepository, IProductEventPublisher productEventPublisher) : ControllerBase
{
    private readonly ProductService _productService = productService;
    private readonly IProductRepository _productRepository = productRepository;
    private readonly ICategoryRepository _categoryRepository = categoryRepository;
    private readonly IProductEventPublisher _productEventPublisher = productEventPublisher;

    /// <summary>
    /// Returns a product by identifier.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        Product? product = await _productRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return product is null ? NotFound() : Ok(MapToResponse(product));
    }

    /// <summary>
    /// Returns a page of products.
    /// </summary>
    [HttpGet]
    public Task<ActionResult<PageResponse<ProductResponse>>> GetPage([FromQuery] Guid? categoryId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) => GetPageInternal(categoryId, pageNumber, pageSize, cancellationToken);

    /// <summary>
    /// Returns a page of products for a specific category.
    /// </summary>
    [HttpGet("~/api/v{version:apiVersion}/categories/{categoryId:guid}/products")]
    public Task<ActionResult<PageResponse<ProductResponse>>> GetPageByCategory(Guid categoryId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) => GetPageInternal(categoryId, pageNumber, pageSize, cancellationToken);

    /// <summary>
    /// Creates a product.
    /// </summary>
    [Authorize(Roles = AuthRoles.Manager)]
    [HttpPost]
    public async Task<ActionResult<ProductResponse>> Create([FromBody] ProductUpsertRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var product = new Product(Guid.NewGuid(), request.Name, request.Description, MapImage(request.Image), request.CategoryId, request.Price, request.Amount);
            await _productService.AddAsync(product, cancellationToken).ConfigureAwait(false);
            await _productEventPublisher.PublishUpsertedAsync(MapToMessage(product), cancellationToken).ConfigureAwait(false);
            return CreatedAtAction(nameof(GetById), new { id = product.Id, version = "1" }, MapToResponse(product));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    /// <summary>
    /// Updates a product.
    /// </summary>
    [Authorize(Roles = AuthRoles.Manager)]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] ProductUpsertRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var product = new Product(id, request.Name, request.Description, MapImage(request.Image), request.CategoryId, request.Price, request.Amount);
            await _productService.UpdateAsync(product, cancellationToken).ConfigureAwait(false);
            await _productEventPublisher.PublishUpsertedAsync(MapToMessage(product), cancellationToken).ConfigureAwait(false);
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
    [Authorize(Roles = AuthRoles.Manager)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        Product? product = await _productRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (product is null)
        {
            return NotFound();
        }

        await _productService.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        await _productEventPublisher.PublishDeletedAsync(id, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    private async Task<ActionResult<PageResponse<ProductResponse>>> GetPageInternal(Guid? categoryId, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        if (categoryId is not null && !await _categoryRepository.ExistsAsync(categoryId.Value, cancellationToken).ConfigureAwait(false))
        {
            return NotFound();
        }

        PagedResult<Product> page = await _productService.GetPageAsync(categoryId, pageNumber, pageSize, cancellationToken).ConfigureAwait(false);
        return Ok(new PageResponse<ProductResponse>(page.Items.Select(MapToResponse).ToList(), page.TotalCount, page.PageNumber, page.PageSize));
    }

    private static ProductResponse MapToResponse(Product product) => new ProductResponse(product.Id, product.Name, product.Description, MapImage(product.Image), product.CategoryId, product.Price, product.Amount);

    private static ProductChangedMessage MapToMessage(Product product) => new ProductChangedMessage(product.Id, product.Name, product.Description, MapProductImage(product.Image), product.CategoryId, product.Price, product.Amount);

    private static ImageResponse? MapImage(ImageInfo? image) => image is null ? null : new ImageResponse(image.Url, image.AltText);

    private static ProductImageMessage? MapProductImage(ImageInfo? image) => image is null ? null : new ProductImageMessage(image.Url, image.AltText);

    private static ImageInfo? MapImage(ImageRequest? image) => image is null ? null : new ImageInfo(image.Url, image.AltText);
}

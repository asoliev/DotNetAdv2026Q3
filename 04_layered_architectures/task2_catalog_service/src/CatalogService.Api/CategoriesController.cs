using Asp.Versioning;
using CatalogService.Application;
using CatalogService.Domain;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api;

/// <summary>
/// Manages catalog categories.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/categories")]
public sealed class CategoriesController : ControllerBase
{
    private readonly CategoryService _categoryService;

    public CategoriesController(CategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    /// <summary>
    /// Returns all categories.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var categories = await _categoryService.GetAllAsync(cancellationToken);
        return Ok(categories.Select(Map).ToList());
    }

    /// <summary>
    /// Returns a category by identifier.
    /// </summary>
    /// <remarks>The response includes hypermedia links for related actions.</remarks>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CategoryResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var category = await _categoryService.GetByIdAsync(id, cancellationToken);
        return category is null ? NotFound() : Ok(Map(category));
    }

    /// <summary>
    /// Creates a category.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CategoryResponse>> Create([FromBody] CategoryUpsertRequest request, CancellationToken cancellationToken)
    {
        var category = new Category(Guid.NewGuid(), request.Name, MapImage(request.Image), request.ParentCategoryId);
        await _categoryService.AddAsync(category, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = category.Id, version = Version }, Map(category));
    }

    /// <summary>
    /// Updates a category.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CategoryUpsertRequest request, CancellationToken cancellationToken)
    {
        var category = new Category(id, request.Name, MapImage(request.Image), request.ParentCategoryId);
        return await _categoryService.UpdateAsync(category, cancellationToken) ? NoContent() : NotFound();
    }

    /// <summary>
    /// Deletes a category and its products.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        return await _categoryService.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();
    }

    private string Version => HttpContext.GetRequestedApiVersion()?.ToString("VVV") ?? "1";

    private CategoryResponse Map(Category category)
    {
        var links = new List<LinkResponse>
        {
            new(Url.ActionLink(nameof(GetById), "Categories", new { id = category.Id, version = Version })!, "self", "GET"),
            new(Url.ActionLink(nameof(Update), "Categories", new { id = category.Id, version = Version })!, "update", "PUT"),
            new(Url.ActionLink(nameof(Delete), "Categories", new { id = category.Id, version = Version })!, "delete", "DELETE"),
            new(Url.ActionLink(nameof(ProductsController.GetPageByCategory), "Products", new { categoryId = category.Id, version = Version })!, "products", "GET")
        };

        return new CategoryResponse(category.Id, category.Name, MapImage(category.Image), category.ParentCategoryId, links);
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

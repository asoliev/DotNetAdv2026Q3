using Asp.Versioning;

using CatalogService.Application;
using CatalogService.Domain;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using ShoppingAuth;

namespace CatalogService.Api;

/// <summary>
/// Manages catalog categories.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/categories")]
internal sealed class CategoriesController(CategoryService categoryService, ICategoryRepository categoryRepository) : ControllerBase
{
    private readonly CategoryService _categoryService = categoryService;
    private readonly ICategoryRepository _categoryRepository = categoryRepository;

    /// <summary>
    /// Returns all categories.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> GetAll(CancellationToken cancellationToken)
    {
        IReadOnlyList<Category> categories = await _categoryRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return Ok(categories.Select(Map).ToList());
    }

    /// <summary>
    /// Returns a category by identifier.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CategoryResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        Category? category = await _categoryRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return category is null ? NotFound() : Ok(Map(category));
    }

    /// <summary>
    /// Creates a category.
    /// </summary>
    [Authorize(Roles = AuthRoles.Manager)]
    [HttpPost]
    public async Task<ActionResult<CategoryResponse>> Create([FromBody] CategoryUpsertRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var category = new Category(Guid.NewGuid(), request.Name, MapImage(request.Image), request.ParentCategoryId);
            await _categoryService.AddAsync(category, cancellationToken).ConfigureAwait(false);
            return CreatedAtAction(nameof(GetById), new { id = category.Id, version = "1" }, Map(category));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    /// <summary>
    /// Updates a category.
    /// </summary>
    [Authorize(Roles = AuthRoles.Manager)]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CategoryUpsertRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var category = new Category(id, request.Name, MapImage(request.Image), request.ParentCategoryId);
            await _categoryService.UpdateAsync(category, cancellationToken).ConfigureAwait(false);
            return NoContent();
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    /// <summary>
    /// Deletes a category and its products.
    /// </summary>
    [Authorize(Roles = AuthRoles.Manager)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        Category? category = await _categoryRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (category is null)
        {
            return NotFound();
        }

        await _categoryService.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    private static CategoryResponse Map(Category category) => new CategoryResponse(category.Id, category.Name, MapImage(category.Image), category.ParentCategoryId);

    private static ImageResponse? MapImage(ImageInfo? image) => image is null ? null : new ImageResponse(image.Url, image.AltText);

    private static ImageInfo? MapImage(ImageRequest? image) => image is null ? null : new ImageInfo(image.Url, image.AltText);
}

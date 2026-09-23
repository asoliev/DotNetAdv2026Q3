using Asp.Versioning;
using CatalogService.Application;
using CatalogService.Domain;
using ShoppingAuth;
using Microsoft.AspNetCore.Authorization;
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
    private readonly ICategoryRepository _categoryRepository;

    public CategoriesController(CategoryService categoryService, ICategoryRepository categoryRepository)
    {
        _categoryService = categoryService;
        _categoryRepository = categoryRepository;
    }

    /// <summary>
    /// Returns all categories.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var categories = await _categoryRepository.GetAllAsync(cancellationToken);
        return Ok(categories.Select(Map).ToList());
    }

    /// <summary>
    /// Returns a category by identifier.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CategoryResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdAsync(id, cancellationToken);
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
            await _categoryService.AddAsync(category, cancellationToken);
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
            await _categoryService.UpdateAsync(category, cancellationToken);
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
        var category = await _categoryRepository.GetByIdAsync(id, cancellationToken);
        if (category is null)
        {
            return NotFound();
        }

        await _categoryService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    private static CategoryResponse Map(Category category)
    {
        return new CategoryResponse(category.Id, category.Name, MapImage(category.Image), category.ParentCategoryId);
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

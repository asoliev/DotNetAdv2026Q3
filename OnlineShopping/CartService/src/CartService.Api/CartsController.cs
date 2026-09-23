using Asp.Versioning;
using CartService.Bll;
using Microsoft.AspNetCore.Mvc;

namespace CartService.Api;

/// <summary>
/// Manages carts.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/carts/{cartKey}")]
public sealed class CartsController : ControllerBase
{
    private readonly CartService.Bll.CartService _cartService;

    public CartsController(CartService.Bll.CartService cartService)
    {
        _cartService = cartService;
    }

    /// <summary>
    /// Returns cart information for version 1.
    /// </summary>
    [HttpGet]
    [MapToApiVersion("1.0")]
    public async Task<ActionResult<CartResponse>> GetV1(string cartKey, CancellationToken cancellationToken)
    {
        var items = await _cartService.GetItemsAsync(cartKey, cancellationToken);
        return Ok(new CartResponse(cartKey, items.Select(Map).ToList()));
    }

    /// <summary>
    /// Returns cart items for version 2.
    /// </summary>
    [HttpGet]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<IReadOnlyList<CartItemResponse>>> GetV2(string cartKey, CancellationToken cancellationToken)
    {
        var items = await _cartService.GetItemsAsync(cartKey, cancellationToken);
        return Ok(items.Select(Map).ToList());
    }

    /// <summary>
    /// Adds an item to the cart.
    /// </summary>
    [HttpPost("items")]
    [MapToApiVersion("1.0")]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<CartResponse>> AddItem(string cartKey, [FromBody] CartItemRequest request, CancellationToken cancellationToken)
    {
        var items = await _cartService.AddItemAsync(cartKey, Map(request), cancellationToken);
        return Ok(new CartResponse(cartKey, items.Select(Map).ToList()));
    }

    /// <summary>
    /// Deletes an item from the cart.
    /// </summary>
    [HttpDelete("items/{itemId:guid}")]
    [MapToApiVersion("1.0")]
    [MapToApiVersion("2.0")]
    public async Task<IActionResult> DeleteItem(string cartKey, Guid itemId, CancellationToken cancellationToken)
    {
        var removed = await _cartService.RemoveItemAsync(cartKey, itemId, cancellationToken);
        return removed ? Ok() : NotFound();
    }

    private static CartItemResponse Map(CartItem item)
    {
        return new CartItemResponse(item.Id, item.Name, item.Image is null ? null : new CartItemImageResponse(item.Image.Url, item.Image.AltText), item.Price, item.Quantity);
    }

    private static CartItem Map(CartItemRequest request)
    {
        return new CartItem(
            request.Id,
            request.Name,
            request.Image is null ? null : new CartItemImage(request.Image.Url, request.Image.AltText),
            request.Price,
            request.Quantity);
    }
}

using Asp.Versioning;

using CartService.Bll;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using ShoppingAuth;

namespace CartService.Api;

/// <summary>
/// Manages carts.
/// </summary>
[Authorize(Roles = AuthRoles.AnyServiceUser)]
[ApiController]
[ApiVersion("1.0")]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/carts/{cartKey}")]
internal sealed class CartsController(CartService.Bll.CartService cartService) : ControllerBase
{
    private readonly CartService.Bll.CartService _cartService = cartService;

    /// <summary>
    /// Returns cart information for version 1.
    /// </summary>
    [HttpGet]
    [MapToApiVersion("1.0")]
    public async Task<ActionResult<CartResponse>> GetV1(string cartKey, CancellationToken cancellationToken)
    {
        IReadOnlyList<CartItem> items = await _cartService.GetItemsAsync(cartKey, cancellationToken).ConfigureAwait(false);
        return Ok(new CartResponse(cartKey, items.Select(Map).ToList()));
    }

    /// <summary>
    /// Returns cart items for version 2.
    /// </summary>
    [HttpGet]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<IReadOnlyList<CartItemResponse>>> GetV2(string cartKey, CancellationToken cancellationToken)
    {
        IReadOnlyList<CartItem> items = await _cartService.GetItemsAsync(cartKey, cancellationToken).ConfigureAwait(false);
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
        IReadOnlyList<CartItem> items = await _cartService.AddItemAsync(cartKey, Map(request), cancellationToken).ConfigureAwait(false);
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
        var removed = await _cartService.RemoveItemAsync(cartKey, itemId, cancellationToken).ConfigureAwait(false);
        return removed ? Ok() : NotFound();
    }

    private static CartItemResponse Map(CartItem item) => new CartItemResponse(item.Id, item.Name, item.Image is null ? null : new CartItemImageResponse(item.Image.Url, item.Image.AltText), item.Price, item.Quantity);

    private static CartItem Map(CartItemRequest request) => new CartItem(
            request.Id,
            request.Name,
            request.Image is null ? null : new CartItemImage(request.Image.Url, request.Image.AltText),
            request.Price,
            request.Quantity);
}

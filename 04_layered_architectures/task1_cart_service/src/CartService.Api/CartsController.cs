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

    /// <summary>Initializes the carts controller.</summary>
    /// <param name="cartService">Application service for cart operations.</param>
    public CartsController(CartService.Bll.CartService cartService)
    {
        _cartService = cartService;
    }

    /// <summary>Returns cart information for version 1.</summary>
    /// <param name="cartKey">Unique cart key.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <response code="200">Cart model, or an empty cart when the key has no cart yet.</response>
    [ProducesResponseType(typeof(CartResponse), StatusCodes.Status200OK)]
    [HttpGet]
    [MapToApiVersion("1.0")]
    public async Task<ActionResult<CartResponse>> GetV1(string cartKey, CancellationToken cancellationToken)
    {
        var items = await _cartService.GetItemsAsync(cartKey, cancellationToken);
        return Ok(new CartResponse(cartKey, items.Select(Map).ToList()));
    }

    /// <summary>Returns cart items for version 2.</summary>
    /// <param name="cartKey">Unique cart key.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <response code="200">Cart items, or an empty list when the key has no cart yet.</response>
    [ProducesResponseType(typeof(IReadOnlyList<CartItemResponse>), StatusCodes.Status200OK)]
    [HttpGet]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<IReadOnlyList<CartItemResponse>>> GetV2(string cartKey, CancellationToken cancellationToken)
    {
        var items = await _cartService.GetItemsAsync(cartKey, cancellationToken);
        return Ok(items.Select(Map).ToList());
    }

    /// <summary>Adds an item to the cart, creating the cart if needed.</summary>
    /// <param name="cartKey">Unique cart key.</param>
    /// <param name="request">Item to add.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <response code="200">Item was added.</response>
    /// <response code="400">The item or cart key is invalid.</response>
    [ProducesResponseType(typeof(CartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [HttpPost("items")]
    [MapToApiVersion("1.0")]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<CartResponse>> AddItem(string cartKey, [FromBody] CartItemRequest request, CancellationToken cancellationToken)
    {
        var items = await _cartService.AddItemAsync(cartKey, Map(request), cancellationToken);
        return Ok(new CartResponse(cartKey, items.Select(Map).ToList()));
    }

    /// <summary>Deletes an item from the cart.</summary>
    /// <param name="cartKey">Unique cart key.</param>
    /// <param name="itemId">Item identifier.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <response code="200">Item was removed.</response>
    /// <response code="404">Cart or item was not found.</response>
    /// <response code="400">The cart key is invalid.</response>
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [HttpDelete("items/{itemId:int}")]
    [MapToApiVersion("1.0")]
    [MapToApiVersion("2.0")]
    public async Task<IActionResult> DeleteItem(string cartKey, int itemId, CancellationToken cancellationToken)
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

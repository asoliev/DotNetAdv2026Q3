using System.ComponentModel.DataAnnotations;

namespace CartService.Api;

/// <summary>Cart item returned by the API.</summary>
public sealed record CartItemResponse(int Id, string Name, CartItemImageResponse? Image, decimal Price, int Quantity);

/// <summary>Cart key and its items.</summary>
public sealed record CartResponse(string CartKey, IReadOnlyList<CartItemResponse> Items);

/// <summary>Cart item submitted to the API.</summary>
public sealed record CartItemRequest(
    [property: Range(1, int.MaxValue)] int Id,
    [property: Required] string Name,
    CartItemImageRequest? Image,
    [property: Range(typeof(decimal), "0.01", "79228162514264337593543950335")] decimal Price,
    [property: Range(1, int.MaxValue)] int Quantity);

/// <summary>Image details submitted with a cart item.</summary>
public sealed record CartItemImageRequest([property: Required] string Url, string? AltText);

/// <summary>Image details returned for a cart item.</summary>
public sealed record CartItemImageResponse(string Url, string? AltText);

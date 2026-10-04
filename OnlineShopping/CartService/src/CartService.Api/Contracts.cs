using System.Text.Json.Serialization;

namespace CartService.Api;

public sealed record CartItemResponse(Guid Id, string Name, CartItemImageResponse? Image, decimal Price, int Quantity);

public sealed record CartResponse(string CartKey, IReadOnlyList<CartItemResponse> Items);

public sealed record CartItemRequest(
    [property: JsonRequired] Guid Id,
    string Name,
    CartItemImageRequest? Image,
    [property: JsonRequired] decimal Price,
    [property: JsonRequired] int Quantity);

public sealed record CartItemImageRequest(Uri Url, string? AltText);

public sealed record CartItemImageResponse(Uri Url, string? AltText);

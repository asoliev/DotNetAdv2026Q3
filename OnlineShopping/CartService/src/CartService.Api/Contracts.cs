namespace CartService.Api;

internal sealed record CartItemResponse(Guid Id, string Name, CartItemImageResponse? Image, decimal Price, int Quantity);

internal sealed record CartResponse(string CartKey, IReadOnlyList<CartItemResponse> Items);

internal sealed record CartItemRequest(Guid Id, string Name, CartItemImageRequest? Image, decimal Price, int Quantity);

internal sealed record CartItemImageRequest(string Url, string? AltText);

internal sealed record CartItemImageResponse(string Url, string? AltText);

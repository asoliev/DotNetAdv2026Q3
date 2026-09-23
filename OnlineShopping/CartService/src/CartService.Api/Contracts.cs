namespace CartService.Api;

public sealed record CartItemResponse(Guid Id, string Name, CartItemImageResponse? Image, decimal Price, int Quantity);

public sealed record CartResponse(string CartKey, IReadOnlyList<CartItemResponse> Items);

public sealed record CartItemRequest(Guid Id, string Name, CartItemImageRequest? Image, decimal Price, int Quantity);

public sealed record CartItemImageRequest(string Url, string? AltText);

public sealed record CartItemImageResponse(string Url, string? AltText);

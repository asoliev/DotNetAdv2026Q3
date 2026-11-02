namespace CartService.Api;

/// <summary>Cart item returned by the API.</summary>
public sealed record CartItemResponse(int Id, string Name, CartItemImageResponse? Image, decimal Price, int Quantity);

/// <summary>Cart key and its items.</summary>
public sealed record CartResponse(string CartKey, IReadOnlyList<CartItemResponse> Items);

/// <summary>Cart item submitted to the API.</summary>
public sealed record CartItemRequest(int Id, string Name, CartItemImageRequest? Image, decimal Price, int Quantity);

/// <summary>Image details submitted with a cart item.</summary>
public sealed record CartItemImageRequest(string Url, string? AltText);

/// <summary>Image details returned for a cart item.</summary>
public sealed record CartItemImageResponse(string Url, string? AltText);

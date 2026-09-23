using System.Collections.ObjectModel;

using LiteDB;

namespace CartService.Dal;

public sealed class CartDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    public Collection<CartItemDocument> Items { get; } = new();
}

public sealed class CartItemDocument
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public CartItemImageDocument? Image { get; set; }

    public decimal Price { get; set; }

    public int Quantity { get; set; }
}

public sealed class CartItemImageDocument
{
    public Uri Url { get; set; } = new("http://localhost");

    public string? AltText { get; set; }
}

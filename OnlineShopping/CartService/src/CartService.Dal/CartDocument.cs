using LiteDB;

namespace CartService.Dal;

public sealed class CartDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    public List<CartItemDocument> Items { get; set; } = new();
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
    public string Url { get; set; } = string.Empty;

    public string? AltText { get; set; }
}
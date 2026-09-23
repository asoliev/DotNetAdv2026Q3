using CartService.Bll;
using LiteDB;

namespace CartService.Dal;

public sealed class LiteDbCartRepository : ICartRepository, IDisposable
{
    private readonly LiteDatabase _database;

    public LiteDbCartRepository(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Database path is required.", nameof(databasePath));
        }

        _database = new LiteDatabase($"Filename={databasePath};Connection=shared");
    }

    public Task<Cart?> GetByIdAsync(string cartKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var document = GetCollection().FindById(cartKey);
        return Task.FromResult(document is null ? null : MapToDomain(document));
    }

    public Task<IReadOnlyList<Cart>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var carts = GetCollection().FindAll().Select(MapToDomain).ToList();
        return Task.FromResult<IReadOnlyList<Cart>>(carts);
    }

    public Task UpsertAsync(Cart cart, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cart);
        cancellationToken.ThrowIfCancellationRequested();

        var collection = GetCollection();
        collection.Upsert(MapToDocument(cart));

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _database.Dispose();
    }

    private ILiteCollection<CartDocument> GetCollection()
    {
        return _database.GetCollection<CartDocument>("carts");
    }

    private static CartDocument MapToDocument(Cart cart)
    {
        return new CartDocument
        {
            Id = cart.Id,
            Items = cart.GetItems().Select(MapItemToDocument).ToList()
        };
    }

    private static Cart MapToDomain(CartDocument document)
    {
        return new Cart(
            document.Id,
            document.Items.Select(MapItemToDomain));
    }

    private static CartItemDocument MapItemToDocument(CartItem item)
    {
        return new CartItemDocument
        {
            Id = item.Id,
            Name = item.Name,
            Image = item.Image is null
                ? null
                : new CartItemImageDocument
                {
                    Url = item.Image.Url,
                    AltText = item.Image.AltText
                },
            Price = item.Price,
            Quantity = item.Quantity
        };
    }

    private static CartItem MapItemToDomain(CartItemDocument document)
    {
        return new CartItem(
            document.Id,
            document.Name,
            document.Image is null
                ? null
                : new CartItemImage(document.Image.Url, document.Image.AltText),
            document.Price,
            document.Quantity);
    }
}
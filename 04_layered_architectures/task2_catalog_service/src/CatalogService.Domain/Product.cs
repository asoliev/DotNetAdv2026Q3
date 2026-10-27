namespace CatalogService.Domain;

public sealed class Product
{
    public Product(Guid id, string name, string? description, ImageInfo? image, Guid categoryId, decimal price, int amount)
    {
        if (id == Guid.Empty)
        {
            id = Guid.NewGuid();
        }

        ValidateName(name);
        ValidatePrice(price);
        ValidateAmount(amount);
        ValidateCategoryId(categoryId);

        Id = id;
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description;
        Image = image;
        CategoryId = categoryId;
        Price = price;
        Amount = amount;
    }

    public Guid Id { get; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public ImageInfo? Image { get; private set; }

    public Guid CategoryId { get; private set; }

    public decimal Price { get; private set; }

    public int Amount { get; private set; }

    public void Update(string name, string? description, ImageInfo? image, Guid categoryId, decimal price, int amount)
    {
        ValidateName(name);
        ValidatePrice(price);
        ValidateAmount(amount);
        ValidateCategoryId(categoryId);

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description;
        Image = image;
        CategoryId = categoryId;
        Price = price;
        Amount = amount;
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Product name is required.", nameof(name));
        }

        if (name.Trim().Length > 50)
        {
            throw new ArgumentException("Product name must be 50 characters or less.", nameof(name));
        }
    }

    private static void ValidateCategoryId(Guid categoryId)
    {
        if (categoryId == Guid.Empty)
        {
            throw new ArgumentException("Product category is required.", nameof(categoryId));
        }
    }

    private static void ValidatePrice(decimal price)
    {
        if (price <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "Product price must be positive.");
        }
    }

    private static void ValidateAmount(int amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Product amount must be positive.");
        }
    }
}
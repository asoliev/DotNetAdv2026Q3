namespace CatalogService.Domain;

public sealed class Category
{
    public Category(Guid id, string name, ImageInfo? image = null, Guid? parentCategoryId = null)
    {
        if (id == Guid.Empty)
        {
            id = Guid.NewGuid();
        }

        ValidateName(name);

        Id = id;
        Name = name.Trim();
        Image = image;
        ParentCategoryId = parentCategoryId;
    }

    public Guid Id { get; }

    public string Name { get; private set; }

    public ImageInfo? Image { get; private set; }

    public Guid? ParentCategoryId { get; private set; }

    public void Update(string name, ImageInfo? image, Guid? parentCategoryId)
    {
        ValidateName(name);
        Name = name.Trim();
        Image = image;
        ParentCategoryId = parentCategoryId;
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Category name is required.", nameof(name));
        }

        if (name.Trim().Length > 50)
        {
            throw new ArgumentException("Category name must be 50 characters or less.", nameof(name));
        }
    }
}
using CatalogService.Application;
using CatalogService.Domain;
using Microsoft.Data.Sqlite;

namespace CatalogService.Infrastructure;

public sealed class SqliteProductRepository : IProductRepository
{
    private readonly CatalogDatabase _database;

    public SqliteProductRepository(string databasePath)
    {
        _database = new CatalogDatabase(databasePath);
    }

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = _database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, Description, ImageUrl, ImageAltText, CategoryId, Price, Amount FROM Products WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id.ToString());

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return Map(reader);
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var items = new List<Product>();
        using var connection = _database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, Description, ImageUrl, ImageAltText, CategoryId, Price, Amount FROM Products ORDER BY Name";

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(Map(reader));
        }

        return items;
    }

    public async Task AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        using var connection = _database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Products (Id, Name, Description, ImageUrl, ImageAltText, CategoryId, Price, Amount)
            VALUES ($id, $name, $description, $imageUrl, $imageAltText, $categoryId, $price, $amount);
            """;
        ApplyParameters(command, product);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(Product product, CancellationToken cancellationToken = default)
    {
        using var connection = _database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Products
            SET Name = $name,
                Description = $description,
                ImageUrl = $imageUrl,
                ImageAltText = $imageAltText,
                CategoryId = $categoryId,
                Price = $price,
                Amount = $amount
            WHERE Id = $id;
            """;
        ApplyParameters(command, product);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = _database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Products WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void ApplyParameters(SqliteCommand command, Product product)
    {
        command.Parameters.AddWithValue("$id", product.Id.ToString());
        command.Parameters.AddWithValue("$name", product.Name);
        command.Parameters.AddWithValue("$description", string.IsNullOrWhiteSpace(product.Description) ? (object)DBNull.Value : product.Description!);
        command.Parameters.AddWithValue("$imageUrl", product.Image?.Url ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$imageAltText", product.Image?.AltText ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$categoryId", product.CategoryId.ToString());
        command.Parameters.AddWithValue("$price", product.Price);
        command.Parameters.AddWithValue("$amount", product.Amount);
    }

    private static Product Map(SqliteDataReader reader)
    {
        var id = Guid.Parse(reader.GetString(0));
        var name = reader.GetString(1);
        string? description = reader.IsDBNull(2) ? null : reader.GetString(2);
        ImageInfo? image = null;
        if (!reader.IsDBNull(3))
        {
            image = new ImageInfo(reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4));
        }

        var categoryId = Guid.Parse(reader.GetString(5));
        var price = reader.GetDecimal(6);
        var amount = reader.GetInt32(7);
        return new Product(id, name, description, image, categoryId, price, amount);
    }
}
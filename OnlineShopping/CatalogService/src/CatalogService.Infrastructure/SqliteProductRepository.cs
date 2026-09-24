using CatalogService.Application;
using CatalogService.Domain;

using Microsoft.Data.Sqlite;

namespace CatalogService.Infrastructure;

public sealed class SqliteProductRepository(string databasePath) : IProductRepository
{
    private readonly CatalogDatabase _database = new CatalogDatabase(databasePath);

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, Description, ImageUrl, ImageAltText, CategoryId, Price, Amount FROM Products WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id.ToString());

        using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return Map(reader);
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var items = new List<Product>();
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, Description, ImageUrl, ImageAltText, CategoryId, Price, Amount FROM Products ORDER BY Name";

        using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            items.Add(Map(reader));
        }

        return items;
    }

    public async Task<PagedResult<Product>> GetPageAsync(Guid? categoryId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var offset = (pageNumber - 1) * pageSize;
        var filterClause = categoryId is null ? string.Empty : "WHERE CategoryId = $categoryId";

        using SqliteConnection connection = _database.CreateConnection();

        int totalCount;
        using (SqliteCommand countCommand = connection.CreateCommand())
        {
            countCommand.CommandText = $"SELECT COUNT(*) FROM Products {filterClause}";
            if (categoryId is not null)
            {
                countCommand.Parameters.AddWithValue("$categoryId", categoryId.Value.ToString());
            }

            var result = await countCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            totalCount = Convert.ToInt32(result);
        }

        var items = new List<Product>();
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = $"""
                SELECT Id, Name, Description, ImageUrl, ImageAltText, CategoryId, Price, Amount
                FROM Products
                {filterClause}
                ORDER BY Name
                LIMIT $pageSize OFFSET $offset;
                """;
            if (categoryId is not null)
            {
                command.Parameters.AddWithValue("$categoryId", categoryId.Value.ToString());
            }

            command.Parameters.AddWithValue("$pageSize", pageSize);
            command.Parameters.AddWithValue("$offset", offset);

            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                items.Add(Map(reader));
            }
        }

        return new PagedResult<Product>(items, totalCount, pageNumber, pageSize);
    }

    public async Task AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Products (Id, Name, Description, ImageUrl, ImageAltText, CategoryId, Price, Amount)
            VALUES ($id, $name, $description, $imageUrl, $imageAltText, $categoryId, $price, $amount);
            """;
        ApplyParameters(command, product);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(Product product, CancellationToken cancellationToken = default)
    {
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteCommand command = connection.CreateCommand();
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
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Products WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteByCategoryIdAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Products WHERE CategoryId = $categoryId";
        command.Parameters.AddWithValue("$categoryId", categoryId.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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
        var description = reader.IsDBNull(2) ? null : reader.GetString(2);
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

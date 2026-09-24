using CatalogService.Application;
using CatalogService.Domain;

using Microsoft.Data.Sqlite;

namespace CatalogService.Infrastructure;

public sealed class SqliteCategoryRepository(string databasePath) : ICategoryRepository
{
    private readonly CatalogDatabase _database = new CatalogDatabase(databasePath);

    public async Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, ImageUrl, ImageAltText, ParentCategoryId FROM Categories WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id.ToString());

        using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return Map(reader);
    }

    public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var items = new List<Category>();
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, ImageUrl, ImageAltText, ParentCategoryId FROM Categories ORDER BY Name";

        using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            items.Add(Map(reader));
        }

        return items;
    }

    public async Task AddAsync(Category category, CancellationToken cancellationToken = default)
    {
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Categories (Id, Name, ImageUrl, ImageAltText, ParentCategoryId)
            VALUES ($id, $name, $imageUrl, $imageAltText, $parentCategoryId);
            """;
        ApplyParameters(command, category);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(Category category, CancellationToken cancellationToken = default)
    {
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Categories
            SET Name = $name,
                ImageUrl = $imageUrl,
                ImageAltText = $imageAltText,
                ParentCategoryId = $parentCategoryId
            WHERE Id = $id;
            """;
        ApplyParameters(command, category);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Categories WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM Categories WHERE Id = $id LIMIT 1";
        command.Parameters.AddWithValue("$id", id.ToString());

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is not null;
    }

    private static void ApplyParameters(SqliteCommand command, Category category)
    {
        command.Parameters.AddWithValue("$id", category.Id.ToString());
        command.Parameters.AddWithValue("$name", category.Name);
        command.Parameters.AddWithValue("$imageUrl", category.Image?.Url ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$imageAltText", category.Image?.AltText ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$parentCategoryId", category.ParentCategoryId?.ToString() ?? (object)DBNull.Value);
    }

    private static Category Map(SqliteDataReader reader)
    {
        var id = Guid.Parse(reader.GetString(0));
        var name = reader.GetString(1);
        ImageInfo? image = null;
        if (!reader.IsDBNull(2))
        {
            image = new ImageInfo(reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3));
        }

        Guid? parentCategoryId = null;
        if (!reader.IsDBNull(4))
        {
            parentCategoryId = Guid.Parse(reader.GetString(4));
        }

        return new Category(id, name, image, parentCategoryId);
    }
}

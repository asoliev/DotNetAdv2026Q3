using Microsoft.Data.Sqlite;

namespace CatalogService.Infrastructure;

internal sealed class CatalogDatabase
{
    private readonly string _connectionString;

    public CatalogDatabase(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Database path is required.", nameof(databasePath));
        }

        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true
        }.ToString();

        EnsureCreated();
    }

    public SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private void EnsureCreated()
    {
        using SqliteConnection connection = CreateConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Categories (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                ImageUrl TEXT NULL,
                ImageAltText TEXT NULL,
                ParentCategoryId TEXT NULL,
                FOREIGN KEY (ParentCategoryId) REFERENCES Categories(Id)
            );

            CREATE TABLE IF NOT EXISTS Products (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Description TEXT NULL,
                ImageUrl TEXT NULL,
                ImageAltText TEXT NULL,
                CategoryId TEXT NOT NULL,
                Price REAL NOT NULL,
                Amount INTEGER NOT NULL,
                FOREIGN KEY (CategoryId) REFERENCES Categories(Id)
            );
            """;
        command.ExecuteNonQuery();
    }
}

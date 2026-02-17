using FileSyncApp.Core.Interfaces;
using FileSyncApp.Core.Models;
using Microsoft.Data.Sqlite;

namespace FileSyncApp.Core.Services;

public class DatabaseService : IDatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(string dbPath = "metadata.db")
    {
        _connectionString = $"Data Source={dbPath}";
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText =
        @"
            CREATE TABLE IF NOT EXISTS SyncSnapshot (
                Path TEXT PRIMARY KEY,
                Size INTEGER,
                LastModified TEXT,
                Checksum TEXT,
                S3Key TEXT,
                VersionId TEXT
            );
        ";
        command.ExecuteNonQuery();
    }

    public void SaveSnapshot(FileNode node)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText =
        @"
            INSERT OR REPLACE INTO SyncSnapshot (Path, Size, LastModified, S3Key, VersionId)
            VALUES ($path, $size, $lastModified, $s3Key, $versionId)
        ";
        command.Parameters.AddWithValue("$path", node.Path);
        command.Parameters.AddWithValue("$size", node.Size);
        command.Parameters.AddWithValue("$lastModified", node.LastModified.ToString("O"));
        command.Parameters.AddWithValue("$s3Key", node.IsS3 ? node.Path : "");
        command.Parameters.AddWithValue("$versionId", node.VersionId);
        command.ExecuteNonQuery();
    }

    public void DeleteSnapshot(string path)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM SyncSnapshot WHERE Path = $path";
        command.Parameters.AddWithValue("$path", path);
        command.ExecuteNonQuery();
    }

    public List<SnapshotEntry> GetSnapshots()
    {
        var result = new List<SnapshotEntry>();
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT Path, Size, LastModified, S3Key, VersionId FROM SyncSnapshot";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new SnapshotEntry(
                reader.GetString(0),
                reader.GetInt64(1),
                DateTime.Parse(reader.GetString(2)),
                reader.GetString(3),
                reader.GetString(4)
            ));
        }
        return result;
    }
}

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace FileSyncApp.Core.Services;

/// <summary>
/// SQLite-backed cache for file metadata and sync state
/// Enables delta sync by tracking last-known state of files
/// </summary>
public class MetadataCache : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ILogger<MetadataCache> _logger;
    private bool _disposed;

    public MetadataCache(string databasePath, ILogger<MetadataCache> logger)
    {
        _logger = logger;
        _connection = new SqliteConnection($"Data Source={databasePath}");
        _connection.Open();
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS FileRecords (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                BasePath TEXT NOT NULL,
                RelativePath TEXT NOT NULL,
                Size INTEGER NOT NULL,
                LastModified TEXT NOT NULL,
                ETag TEXT,
                Hash TEXT,
                SyncedAt TEXT NOT NULL,
                UNIQUE(BasePath, RelativePath)
            );

            CREATE INDEX IF NOT EXISTS idx_file_records_base_path 
            ON FileRecords(BasePath);

            CREATE TABLE IF NOT EXISTS SyncHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                BasePath TEXT NOT NULL,
                SyncedAt TEXT NOT NULL,
                FilesUploaded INTEGER NOT NULL,
                FilesDownloaded INTEGER NOT NULL,
                FilesDeleted INTEGER NOT NULL,
                BytesTransferred INTEGER NOT NULL,
                Duration TEXT NOT NULL,
                Success INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Settings (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            );
        ";
        cmd.ExecuteNonQuery();
        _logger.LogInformation("Metadata database initialized");
    }

    public async Task<List<FileRecord>> GetAllRecordsAsync(string basePath)
    {
        var records = new List<FileRecord>();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT RelativePath, Size, LastModified, ETag, Hash, SyncedAt 
            FROM FileRecords 
            WHERE BasePath = @basePath";
        cmd.Parameters.AddWithValue("@basePath", basePath);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            records.Add(new FileRecord
            {
                RelativePath = reader.GetString(0),
                Size = reader.GetInt64(1),
                LastModified = DateTime.Parse(reader.GetString(2)),
                ETag = reader.IsDBNull(3) ? null : reader.GetString(3),
                Hash = reader.IsDBNull(4) ? null : reader.GetString(4),
                SyncedAt = DateTime.Parse(reader.GetString(5))
            });
        }

        return records;
    }

    public async Task<FileRecord?> GetRecordAsync(string basePath, string relativePath)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Size, LastModified, ETag, Hash, SyncedAt 
            FROM FileRecords 
            WHERE BasePath = @basePath AND RelativePath = @relativePath";
        cmd.Parameters.AddWithValue("@basePath", basePath);
        cmd.Parameters.AddWithValue("@relativePath", relativePath);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new FileRecord
            {
                RelativePath = relativePath,
                Size = reader.GetInt64(0),
                LastModified = DateTime.Parse(reader.GetString(1)),
                ETag = reader.IsDBNull(2) ? null : reader.GetString(2),
                Hash = reader.IsDBNull(3) ? null : reader.GetString(3),
                SyncedAt = DateTime.Parse(reader.GetString(4))
            };
        }

        return null;
    }

    public async Task UpdateRecordAsync(string basePath, string relativePath, long size, DateTime lastModified, string? etag = null, string? hash = null)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO FileRecords (BasePath, RelativePath, Size, LastModified, ETag, Hash, SyncedAt)
            VALUES (@basePath, @relativePath, @size, @lastModified, @etag, @hash, @syncedAt)
            ON CONFLICT(BasePath, RelativePath) DO UPDATE SET
                Size = @size,
                LastModified = @lastModified,
                ETag = @etag,
                Hash = @hash,
                SyncedAt = @syncedAt";

        cmd.Parameters.AddWithValue("@basePath", basePath);
        cmd.Parameters.AddWithValue("@relativePath", relativePath);
        cmd.Parameters.AddWithValue("@size", size);
        cmd.Parameters.AddWithValue("@lastModified", lastModified.ToString("O"));
        cmd.Parameters.AddWithValue("@etag", etag ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@hash", hash ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@syncedAt", DateTime.UtcNow.ToString("O"));

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteRecordAsync(string basePath, string relativePath)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            DELETE FROM FileRecords 
            WHERE BasePath = @basePath AND RelativePath = @relativePath";
        cmd.Parameters.AddWithValue("@basePath", basePath);
        cmd.Parameters.AddWithValue("@relativePath", relativePath);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ClearRecordsAsync(string basePath)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM FileRecords WHERE BasePath = @basePath";
        cmd.Parameters.AddWithValue("@basePath", basePath);

        await cmd.ExecuteNonQueryAsync();
        _logger.LogInformation("Cleared all records for {BasePath}", basePath);
    }

    public async Task<DateTime?> GetLastSyncTimeAsync(string basePath)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT MAX(SyncedAt) FROM SyncHistory 
            WHERE BasePath = @basePath AND Success = 1";
        cmd.Parameters.AddWithValue("@basePath", basePath);

        var result = await cmd.ExecuteScalarAsync();
        if (result != null && result != DBNull.Value)
        {
            return DateTime.Parse((string)result);
        }

        return null;
    }

    public async Task RecordSyncHistoryAsync(string basePath, int filesUploaded, int filesDownloaded, 
        int filesDeleted, long bytesTransferred, TimeSpan duration, bool success)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO SyncHistory 
            (BasePath, SyncedAt, FilesUploaded, FilesDownloaded, FilesDeleted, BytesTransferred, Duration, Success)
            VALUES 
            (@basePath, @syncedAt, @filesUploaded, @filesDownloaded, @filesDeleted, @bytesTransferred, @duration, @success)";

        cmd.Parameters.AddWithValue("@basePath", basePath);
        cmd.Parameters.AddWithValue("@syncedAt", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@filesUploaded", filesUploaded);
        cmd.Parameters.AddWithValue("@filesDownloaded", filesDownloaded);
        cmd.Parameters.AddWithValue("@filesDeleted", filesDeleted);
        cmd.Parameters.AddWithValue("@bytesTransferred", bytesTransferred);
        cmd.Parameters.AddWithValue("@duration", duration.ToString());
        cmd.Parameters.AddWithValue("@success", success ? 1 : 0);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<SyncHistoryEntry>> GetSyncHistoryAsync(string basePath, int limit = 50)
    {
        var history = new List<SyncHistoryEntry>();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT SyncedAt, FilesUploaded, FilesDownloaded, FilesDeleted, BytesTransferred, Duration, Success
            FROM SyncHistory
            WHERE BasePath = @basePath
            ORDER BY SyncedAt DESC
            LIMIT @limit";
        cmd.Parameters.AddWithValue("@basePath", basePath);
        cmd.Parameters.AddWithValue("@limit", limit);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            history.Add(new SyncHistoryEntry
            {
                SyncedAt = DateTime.Parse(reader.GetString(0)),
                FilesUploaded = reader.GetInt32(1),
                FilesDownloaded = reader.GetInt32(2),
                FilesDeleted = reader.GetInt32(3),
                BytesTransferred = reader.GetInt64(4),
                Duration = TimeSpan.Parse(reader.GetString(5)),
                Success = reader.GetInt32(6) == 1
            });
        }

        return history;
    }

    public async Task SetSettingAsync(string key, string value)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Settings (Key, Value) VALUES (@key, @value)
            ON CONFLICT(Key) DO UPDATE SET Value = @value";
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@value", value);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<string?> GetSettingAsync(string key)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT Value FROM Settings WHERE Key = @key";
        cmd.Parameters.AddWithValue("@key", key);

        var result = await cmd.ExecuteScalarAsync();
        return result as string;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _connection.Dispose();
            _disposed = true;
        }
    }
}

public class FileRecord
{
    public string RelativePath { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime LastModified { get; set; }
    public string? ETag { get; set; }
    public string? Hash { get; set; }
    public DateTime SyncedAt { get; set; }
}

public class SyncHistoryEntry
{
    public DateTime SyncedAt { get; set; }
    public int FilesUploaded { get; set; }
    public int FilesDownloaded { get; set; }
    public int FilesDeleted { get; set; }
    public long BytesTransferred { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
}

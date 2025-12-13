using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace InsaitTextEditor.Services.Database.Core;

/// <summary>
/// Менеджер для управління шардами баз даних
/// Відповідає за ротацію при досягненні ліміту розміру
/// </summary>
public class DatabaseShardManager
{
    private readonly DatabaseConfig _config;
    private readonly string _databaseName;
    private ShardMetadata _metadata;

    public DatabaseShardManager(DatabaseConfig config, string databaseName)
    {
        _config = config;
        _databaseName = databaseName;
        _metadata = LoadOrCreateMetadata();
    }

    /// <summary>
    /// Отримати шлях до активного шарда
    /// </summary>
    public string GetActiveShardPath()
    {
        var activeShard = _metadata.GetActiveShard();
        if (activeShard == null)
        {
            // Створити перший шард
            activeShard = CreateNewShard(1);
        }

        return Path.Combine(_config.GetDatabasePath(_databaseName), activeShard.FileName);
    }

    /// <summary>
    /// Перевірити, чи потрібна ротація
    /// </summary>
    public bool NeedsRotation()
    {
        var activeShard = _metadata.GetActiveShard();
        if (activeShard == null) return false;

        var shardPath = Path.Combine(_config.GetDatabasePath(_databaseName), activeShard.FileName);
        if (!File.Exists(shardPath)) return false;

        var fileInfo = new FileInfo(shardPath);
        var threshold = _config.MaxShardSizeBytes - _config.RotationBufferBytes;

        return fileInfo.Length >= threshold;
    }

    /// <summary>
    /// Виконати ротацію шарда
    /// </summary>
    public async Task<string> RotateShardAsync()
    {
        var currentShard = _metadata.GetActiveShard();
        if (currentShard != null)
        {
            // Позначити поточний шард як readonly
            currentShard.IsActive = false;
            currentShard.IsReadonly = true;
            
            // Оновити розмір
            var shardPath = Path.Combine(_config.GetDatabasePath(_databaseName), currentShard.FileName);
            if (File.Exists(shardPath))
            {
                currentShard.SizeBytes = new FileInfo(shardPath).Length;
            }
        }

        // Створити новий активний шард
        var newShardNumber = _metadata.ActiveShard + 1;
        var newShard = CreateNewShard(newShardNumber);
        _metadata.ActiveShard = newShardNumber;
        _metadata.LastRotation = DateTime.UtcNow;

        await SaveMetadataAsync();

        return Path.Combine(_config.GetDatabasePath(_databaseName), newShard.FileName);
    }

    /// <summary>
    /// Створити новий шард
    /// </summary>
    private ShardInfo CreateNewShard(int shardNumber)
    {
        var shard = new ShardInfo
        {
            ShardNumber = shardNumber,
            FileName = $"{_databaseName.ToLower()}_{shardNumber:D4}.litedb",
            IsActive = true,
            IsReadonly = false,
            CreatedAt = DateTime.UtcNow,
            EncryptionKeyId = "key_001"
        };

        _metadata.Shards.Add(shard);
        return shard;
    }

    /// <summary>
    /// Завантажити або створити метадані
    /// </summary>
    private ShardMetadata LoadOrCreateMetadata()
    {
        var metadataPath = _config.GetMetadataPath(_databaseName);
        
        if (File.Exists(metadataPath))
        {
            var json = File.ReadAllText(metadataPath);
            return JsonSerializer.Deserialize<ShardMetadata>(json) ?? CreateDefaultMetadata();
        }

        return CreateDefaultMetadata();
    }

    /// <summary>
    /// Створити метадані за замовчуванням
    /// </summary>
    private ShardMetadata CreateDefaultMetadata()
    {
        return new ShardMetadata
        {
            DatabaseType = _databaseName,
            ActiveShard = 1,
            MaxShardSizeBytes = _config.MaxShardSizeBytes,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Зберегти метадані
    /// </summary>
    public async Task SaveMetadataAsync()
    {
        var metadataPath = _config.GetMetadataPath(_databaseName);
        var directory = Path.GetDirectoryName(metadataPath);
        
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(_metadata, options);
        await File.WriteAllTextAsync(metadataPath, json).ConfigureAwait(false);
    }

    /// <summary>
    /// Оновити метрики активного шарда
    /// </summary>
    public async Task UpdateActiveShardMetricsAsync(long recordCount)
    {
        var activeShard = _metadata.GetActiveShard();
        if (activeShard == null) return;

        var shardPath = Path.Combine(_config.GetDatabasePath(_databaseName), activeShard.FileName);
        if (File.Exists(shardPath))
        {
            activeShard.SizeBytes = new FileInfo(shardPath).Length;
        }

        activeShard.RecordCount = recordCount;
        activeShard.LastAccessed = DateTime.UtcNow;

        await SaveMetadataAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Отримати всі шарди
    /// </summary>
    public ShardMetadata GetMetadata() => _metadata;
}

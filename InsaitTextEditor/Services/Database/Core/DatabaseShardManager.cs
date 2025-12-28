using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace InsaitTextEditor.Services.Database.Core;

/// <summary>
/// Manager for handling database shards
/// Responsible for rotation when size limit is reached
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
    /// Get path to the active shard
    /// </summary>
    public string GetActiveShardPath()
    {
        var activeShard = _metadata.GetActiveShard();
        if (activeShard == null)
        {
            // Create the first shard
            activeShard = CreateNewShard(1);
        }

        return Path.Combine(_config.GetDatabasePath(_databaseName), activeShard.FileName);
    }

    /// <summary>
    /// Check whether rotation is needed
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
    /// Perform shard rotation
    /// </summary>
    public async Task<string> RotateShardAsync()
    {
        var currentShard = _metadata.GetActiveShard();
        if (currentShard != null)
        {
            // Mark current shard readonly
            currentShard.IsActive = false;
            currentShard.IsReadonly = true;
            
            // Update size
            var shardPath = Path.Combine(_config.GetDatabasePath(_databaseName), currentShard.FileName);
            if (File.Exists(shardPath))
            {
                currentShard.SizeBytes = new FileInfo(shardPath).Length;
            }
        }

        // Create new active shard
        var newShardNumber = _metadata.ActiveShard + 1;
        var newShard = CreateNewShard(newShardNumber);
        _metadata.ActiveShard = newShardNumber;
        _metadata.LastRotation = DateTime.UtcNow;

        await SaveMetadataAsync();

        return Path.Combine(_config.GetDatabasePath(_databaseName), newShard.FileName);
    }

    /// <summary>
    /// Create a new shard
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
    /// Load or create metadata
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
    /// Create default metadata
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
    /// Save metadata
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
    /// Update metrics for active shard
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
    /// Get all shards
    /// </summary>
    public ShardMetadata GetMetadata() => _metadata;
}

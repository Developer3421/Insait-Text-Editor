using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace InsaitTextEditor.Services.Database.Core;

/// <summary>
/// Метадані для окремого шарда бази даних
/// </summary>
public class ShardMetadata
{
    [JsonPropertyName("database_type")]
    public string DatabaseType { get; set; } = string.Empty;

    [JsonPropertyName("active_shard")]
    public int ActiveShard { get; set; } = 1;

    [JsonPropertyName("shards")]
    public List<ShardInfo> Shards { get; set; } = new();

    [JsonPropertyName("max_shard_size_bytes")]
    public long MaxShardSizeBytes { get; set; } = 1_073_741_824; // 1 GB

    [JsonPropertyName("total_records")]
    public long TotalRecords { get; set; }

    [JsonPropertyName("last_rotation")]
    public DateTime? LastRotation { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Отримати активний шард
    /// </summary>
    public ShardInfo? GetActiveShard()
    {
        return Shards.Find(s => s.ShardNumber == ActiveShard);
    }

    /// <summary>
    /// Отримати всі шарди, відсортовані від нових до старих
    /// </summary>
    public List<ShardInfo> GetShardsNewestFirst()
    {
        var sorted = new List<ShardInfo>(Shards);
        sorted.Sort((a, b) => b.ShardNumber.CompareTo(a.ShardNumber));
        return sorted;
    }
}

/// <summary>
/// Інформація про окремий шард
/// </summary>
public class ShardInfo
{
    [JsonPropertyName("shard_number")]
    public int ShardNumber { get; set; }

    [JsonPropertyName("file_name")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("size_bytes")]
    public long SizeBytes { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }

    [JsonPropertyName("is_readonly")]
    public bool IsReadonly { get; set; }

    [JsonPropertyName("encryption_key_id")]
    public string EncryptionKeyId { get; set; } = "key_001";

    [JsonPropertyName("record_count")]
    public long RecordCount { get; set; }

    [JsonPropertyName("last_accessed")]
    public DateTime? LastAccessed { get; set; }
}


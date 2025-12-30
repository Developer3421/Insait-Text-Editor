using System;
using System.IO;

namespace InsaitTextEditor.Services.Database.Core;

/// <summary>
/// Configuration for databases with encryption and sharding
/// </summary>
public class DatabaseConfig
{
    /// <summary>
    /// Optional override for base data directory.
    /// If set, we store DB/key material under this directory.
    /// </summary>
    private static readonly string? DataRootOverride =
        Environment.GetEnvironmentVariable("INSAIT_DATA_ROOT");

    /// <summary>
    /// Store-safe default data root (per-user, writable).
    /// NOTE: Under MSIX/Store, the installation folder is read-only.
    /// </summary>
    private static string GetDefaultDataRoot()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "InsaitTextEditor");
    }

    private static string GetDataRoot()
    {
        return string.IsNullOrWhiteSpace(DataRootOverride)
            ? GetDefaultDataRoot()
            : DataRootOverride;
    }

    /// <summary>
    /// Base path to the folder with encrypted databases
    /// </summary>
    public string EncryptedDataPath { get; set; } = Path.Combine(
        GetDataRoot(), "Data", "Encrypted");

    /// <summary>
    /// Path to the folder with encryption keys
    /// </summary>
    public string KeysPath { get; set; } = Path.Combine(
        GetDataRoot(), "Data", "Keys");

    /// <summary>
    /// Maximum shard size in bytes (default 1 GB)
    /// </summary>
    public long MaxShardSizeBytes { get; set; } = 1_073_741_824; // 1 GB

    /// <summary>
    /// Rotation buffer (when 50 MB left - create a new shard)
    /// </summary>
    public long RotationBufferBytes { get; set; } = 52_428_800; // 50 MB

    /// <summary>
    /// LiteDB connection type
    /// </summary>
    public string ConnectionType { get; set; } = "Shared";

    /// <summary>
    /// Timeout for DB operations (seconds)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Whether to use encryption
    /// </summary>
    public bool UseEncryption { get; set; } = true;

    /// <summary>
    /// Whether to automatically create DBs at startup
    /// </summary>
    public bool AutoCreateDatabase { get; set; } = true;

    /// <summary>
    /// Get path to the folder for a specific database
    /// </summary>
    public string GetDatabasePath(string databaseName)
    {
        return Path.Combine(EncryptedDataPath, databaseName);
    }

    /// <summary>
    /// Get path to the metadata file for a specific database
    /// </summary>
    public string GetMetadataPath(string databaseName)
    {
        return Path.Combine(GetDatabasePath(databaseName), "metadata.json");
    }

    /// <summary>
    /// Get path to the master key file
    /// </summary>
    public string GetMasterKeyPath()
    {
        return Path.Combine(KeysPath, "master.key");
    }

    /// <summary>
    /// Create all required directories
    /// </summary>
    public void EnsureDirectoriesExist()
    {
        try
        {
            Directory.CreateDirectory(EncryptedDataPath);
            Directory.CreateDirectory(KeysPath);

            // Subfolders for encrypted data
            string[] subfolders = { "ChatHistory", "Documents", "Memory", "Reasoning", "Settings" };
            foreach (var folder in subfolders)
            {
                Directory.CreateDirectory(Path.Combine(EncryptedDataPath, folder));
            }
        }
        catch
        {
            // Never crash during startup if the file system is restricted.
            // Lower layers will fall back to in-memory DBs.
        }
    }
}

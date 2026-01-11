using System;
using System.Threading.Tasks;
using LiteDB;
using InsaitTextEditor.Services;

namespace InsaitTextEditor.Services.Database.Core;

/// <summary>
    /// Base class for all database services
/// </summary>
public abstract class DatabaseServiceBase : IDisposable
{
    protected readonly DatabaseConfig Config;
    protected readonly DatabaseEncryptionManager EncryptionManager;
    protected readonly DatabaseShardManager ShardManager;
    protected readonly string DatabaseName;

    protected LiteDatabase? CurrentDatabase;
    protected bool IsDisposed;
    protected bool _isInitialized;

    protected DatabaseServiceBase(
        DatabaseConfig config,
        DatabaseEncryptionManager encryptionManager,
        string databaseName)
    {
        Config = config;
        EncryptionManager = encryptionManager;
        DatabaseName = databaseName;
        ShardManager = new DatabaseShardManager(config, databaseName);
    }

    /// <summary>
        /// Get or create connection to active database
    /// </summary>
    protected virtual LiteDatabase GetDatabase()
    {
            // Automatically initialize on first access
        if (!_isInitialized)
        {
            _isInitialized = true;

            // Create base connection
            if (CurrentDatabase == null)
            {
                var shardPath = ShardManager.GetActiveShardPath();
                CurrentDatabase = CreateDatabaseConnection(shardPath);
            }

            // Execute synchronous index initialization
            try
            {
                InitializeIndexes(CurrentDatabase);
            }
            catch (Exception ex)
            {

            }
        }

        if (CurrentDatabase != null && !IsDisposed)
        {
            // Check if rotation is needed
            if (ShardManager.NeedsRotation())
            {
                try
                {
                    CurrentDatabase.Dispose();
                }
                catch
                {
                    // ignore
                }
                CurrentDatabase = null;

            // Perform rotation
                try
                {
                    var newShardPath = ShardManager.GetActiveShardPath();
                    CurrentDatabase = CreateDatabaseConnection(newShardPath);
                }
                catch (Exception ex)
                {
                    // CreateDatabaseConnection should never throw, but keep this as an extra guard.

                    var shardPath = ShardManager.GetActiveShardPath();
                    CurrentDatabase = CreateDatabaseConnection(shardPath);
                }

                return CurrentDatabase;
            }

            return CurrentDatabase;
        }

        if (CurrentDatabase == null)
        {
            var shardPath = ShardManager.GetActiveShardPath();
            CurrentDatabase = CreateDatabaseConnection(shardPath);
        }

        return CurrentDatabase;
    }

    /// <summary>
    /// Create database connection
    /// </summary>
    protected virtual LiteDatabase CreateDatabaseConnection(string databasePath)
    {
        // In sandbox / restricted environments, file access can fail.
        // Contract for this method: NEVER throw; always return a usable LiteDatabase.

        LiteDatabase TryOpen(string? path)
        {
            var connectionString = new ConnectionString
            {
                Filename = path,
                Connection = Config.ConnectionType == "Shared" ? ConnectionType.Shared : ConnectionType.Direct,
                Upgrade = true
            };

            if (Config.UseEncryption)
            {
                // Getting master/db key can fail in restricted environments.
                // Let it bubble to our catch below and we'll recreate/fallback.
                var encryptionKey = EncryptionManager.GetDatabaseKey(DatabaseName);
                connectionString.Password = encryptionKey;
            }

            return new LiteDatabase(connectionString);
        }

        string? SafeGetDirectory(string path)
        {
            try { return System.IO.Path.GetDirectoryName(path); }
            catch { return null; }
        }

        void SafeEnsureDirectory(string? directory)
        {
            if (string.IsNullOrEmpty(directory))
                return;
            try { System.IO.Directory.CreateDirectory(directory); }
            catch { /* ignore */ }
        }

        void SafeDeleteFile(string path)
        {
            try
            {
                if (System.IO.File.Exists(path))
                    System.IO.File.Delete(path);
            }
            catch
            {
                // ignore
            }
        }

        // 1) Best-effort: open the intended file.
        try
        {
            SafeEnsureDirectory(SafeGetDirectory(databasePath));
            return TryOpen(databasePath);
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Error($"Failed to open DB '{DatabaseName}' at '{databasePath}'.", ex);
        }

        // 2) If opening failed: try recreating the database file and open again.
        try
        {
            try
            {
                CurrentDatabase?.Dispose();
                CurrentDatabase = null;
            }
            catch
            {
                // ignore
            }

            SafeDeleteFile(databasePath);

            SafeEnsureDirectory(SafeGetDirectory(databasePath));
            var db = TryOpen(databasePath);
            StartupDiagnostics.Info($"Recreated DB '{DatabaseName}' at '{databasePath}'.");
            return db;
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Error($"Failed to recreate DB '{DatabaseName}' at '{databasePath}'.", ex);
        }

        // 3) Last resort: in-memory database (Filename=null). Keeps app alive even if FS is blocked.
        try
        {
            StartupDiagnostics.Warn($"Falling back to in-memory DB for '{DatabaseName}'. Data won't persist.");
            return TryOpen(null);
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Error($"In-memory open failed for '{DatabaseName}'. Retrying without encryption.", ex);
            try
            {
                var original = Config.UseEncryption;
                Config.UseEncryption = false;
                try { return TryOpen(null); }
                finally { Config.UseEncryption = original; }
            }
            catch
            {
                return new LiteDatabase(new ConnectionString { Filename = null, Connection = ConnectionType.Shared });
            }
        }
    }

    /// <summary>
        /// Synchronous index initialization (called from GetDatabase)
    /// </summary>
    protected virtual void InitializeIndexes(LiteDatabase database)
    {
            // By default do nothing
            // Subclasses can override to create indexes
    }

    /// <summary>
        /// Initialize database (create indexes, settings, etc.)
    /// </summary>
    public virtual Task InitializeAsync()
    {
            // Execute synchronous initialization
        var db = GetDatabase();
        InitializeIndexes(db);
        return Task.CompletedTask;
    }

    /// <summary>
        /// Get number of records in active shard
    /// </summary>
    protected virtual long GetRecordCount(string collectionName)
    {
        var db = GetDatabase();
        var collection = db.GetCollection(collectionName);
        return collection.LongCount();
    }

    /// <summary>
    /// Update shard metrics
    /// </summary>
    protected virtual async Task UpdateMetricsAsync(string collectionName)
    {
        var count = GetRecordCount(collectionName);
        await ShardManager.UpdateActiveShardMetricsAsync(count).ConfigureAwait(false);
    }

    public virtual void Dispose()
    {
        if (IsDisposed) return;

        CurrentDatabase?.Dispose();
        CurrentDatabase = null;
        IsDisposed = true;

        GC.SuppressFinalize(this);
    }
}

using System;
using System.Threading.Tasks;
using LiteDB;

namespace InsaitTextEditor.Services.Database.Core;

/// <summary>
/// Базовий клас для всіх сервісів баз даних
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
    /// Отримати або створити з'єднання з активною БД
    /// </summary>
    protected virtual LiteDatabase GetDatabase()
    {
        // Автоматично ініціалізувати при першому доступі
        if (!_isInitialized)
        {
            _isInitialized = true;
            
            // Створити базове з'єднання
            if (CurrentDatabase == null)
            {
                var shardPath = ShardManager.GetActiveShardPath();
                CurrentDatabase = CreateDatabaseConnection(shardPath);
            }
            
            // Виконати синхронну ініціалізацію індексів
            try
            {
                InitializeIndexes(CurrentDatabase);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DatabaseServiceBase] Помилка ініціалізації індексів: {ex.Message}");
            }
        }

        if (CurrentDatabase != null && !IsDisposed)
        {
            // Перевірити чи потрібна ротація
            if (ShardManager.NeedsRotation())
            {
                CurrentDatabase.Dispose();
                CurrentDatabase = null;
                
                // Виконати ротацію
                try
                {
                    var newShardPath = ShardManager.GetActiveShardPath();
                    CurrentDatabase = CreateDatabaseConnection(newShardPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DatabaseServiceBase] Помилка ротації: {ex.Message}");
                    var shardPath = ShardManager.GetActiveShardPath();
                    CurrentDatabase = CreateDatabaseConnection(shardPath);
                }
            }
            else
            {
                return CurrentDatabase;
            }
        }

        if (CurrentDatabase == null)
        {
            var shardPath = ShardManager.GetActiveShardPath();
            CurrentDatabase = CreateDatabaseConnection(shardPath);
        }

        return CurrentDatabase;
    }

    /// <summary>
    /// Створити з'єднання з БД
    /// </summary>
    protected virtual LiteDatabase CreateDatabaseConnection(string databasePath)
    {
        var connectionString = new ConnectionString
        {
            Filename = databasePath,
            Connection = Config.ConnectionType == "Shared" 
                ? ConnectionType.Shared 
                : ConnectionType.Direct,
            Upgrade = true
        };

        // Додати шифрування якщо увімкнено
        if (Config.UseEncryption)
        {
            var encryptionKey = EncryptionManager.GetDatabaseKey(DatabaseName);
            connectionString.Password = encryptionKey;
        }

        // Створити папку якщо не існує
        var directory = System.IO.Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }

        return new LiteDatabase(connectionString);
    }

    /// <summary>
    /// Синхронна ініціалізація індексів (викликається з GetDatabase)
    /// </summary>
    protected virtual void InitializeIndexes(LiteDatabase database)
    {
        // За замовчуванням нічого не робити
        // Підкласи можуть перевизначити для створення індексів
    }

    /// <summary>
    /// Ініціалізувати БД (створити індекси, налаштування тощо)
    /// </summary>
    public virtual Task InitializeAsync()
    {
        // Виконати синхронну ініціалізацію
        var db = GetDatabase();
        InitializeIndexes(db);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Отримати кількість записів в активному шарді
    /// </summary>
    protected virtual long GetRecordCount(string collectionName)
    {
        var db = GetDatabase();
        var collection = db.GetCollection(collectionName);
        return collection.LongCount();
    }

    /// <summary>
    /// Оновити метрики шарда
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

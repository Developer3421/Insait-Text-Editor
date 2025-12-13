using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LiteDB;

namespace InsaitTextEditor.Services.Database.Core;

/// <summary>
/// Розширений сервіс БД з підтримкою запитів по всіх шардах
/// </summary>
public abstract class EncryptedDatabaseService<T> : DatabaseServiceBase where T : class
{
    protected abstract string CollectionName { get; }

    protected EncryptedDatabaseService(
        DatabaseConfig config,
        DatabaseEncryptionManager encryptionManager,
        string databaseName)
        : base(config, encryptionManager, databaseName)
    {
    }

    /// <summary>
    /// Отримати колекцію з активної БД
    /// </summary>
    protected virtual ILiteCollection<T> GetCollection()
    {
        var db = GetDatabase();
        return db.GetCollection<T>(CollectionName);
    }

    /// <summary>
    /// Додати запис
    /// </summary>
    public virtual async Task<BsonValue> InsertAsync(T entity)
    {
        var collection = GetCollection();
        var id = collection.Insert(entity);
        await UpdateMetricsAsync(CollectionName).ConfigureAwait(false);
        return id;
    }

    /// <summary>
    /// Додати багато записів
    /// </summary>
    public virtual async Task<int> InsertBulkAsync(IEnumerable<T> entities)
    {
        var collection = GetCollection();
        var count = collection.InsertBulk(entities);
        await UpdateMetricsAsync(CollectionName).ConfigureAwait(false);
        return count;
    }

    /// <summary>
    /// Оновити запис
    /// </summary>
    public virtual async Task<bool> UpdateAsync(T entity)
    {
        var collection = GetCollection();
        var result = collection.Update(entity);
        await UpdateMetricsAsync(CollectionName).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// Видалити запис по ID
    /// </summary>
    public virtual async Task<bool> DeleteAsync(BsonValue id)
    {
        var collection = GetCollection();
        var result = collection.Delete(id);
        await UpdateMetricsAsync(CollectionName).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// Знайти запис по ID
    /// </summary>
    public virtual T? FindById(BsonValue id)
    {
        var collection = GetCollection();
        return collection.FindById(id);
    }

    /// <summary>
    /// Знайти всі записи
    /// </summary>
    public virtual IEnumerable<T> FindAll()
    {
        var collection = GetCollection();
        return collection.FindAll();
    }

    /// <summary>
    /// Виконати запит
    /// </summary>
    public virtual IEnumerable<T> Query(Func<ILiteCollection<T>, IEnumerable<T>> queryFunc)
    {
        var collection = GetCollection();
        return queryFunc(collection);
    }

    /// <summary>
    /// Отримати загальну кількість записів
    /// </summary>
    public virtual long Count()
    {
        var collection = GetCollection();
        return collection.LongCount();
    }

    /// <summary>
    /// Виконати запит по всіх шардах (від нових до старих)
    /// </summary>
    protected virtual List<T> QueryAllShards(Func<ILiteCollection<T>, IEnumerable<T>> queryFunc, int? limit = null)
    {
        var results = new List<T>();
        var metadata = ShardManager.GetMetadata();
        var shards = metadata.GetShardsNewestFirst();

        foreach (var shard in shards)
        {
            if (limit.HasValue && results.Count >= limit.Value)
                break;

            try
            {
                var shardPath = System.IO.Path.Combine(
                    Config.GetDatabasePath(DatabaseName), 
                    shard.FileName);

                if (!System.IO.File.Exists(shardPath))
                    continue;

                using var shardDb = CreateDatabaseConnection(shardPath);
                var collection = shardDb.GetCollection<T>(CollectionName);
                var shardResults = queryFunc(collection);

                if (limit.HasValue)
                {
                    var remaining = limit.Value - results.Count;
                    results.AddRange(shardResults.Take(remaining));
                }
                else
                {
                    results.AddRange(shardResults);
                }
            }
            catch (Exception ex)
            {
                // Логування помилки (опціонально)
                Console.WriteLine($"Error querying shard {shard.FileName}: {ex.Message}");
            }
        }

        return results;
    }

    /// <summary>
    /// Отримати загальну кількість по всіх шардах
    /// </summary>
    public virtual long GetTotalCountAllShards()
    {
        var metadata = ShardManager.GetMetadata();
        return metadata.TotalRecords;
    }

    /// <summary>
    /// Очистити всю колекцію
    /// </summary>
    public virtual async Task ClearAsync()
    {
        var collection = GetCollection();
        collection.DeleteAll();
        await UpdateMetricsAsync(CollectionName).ConfigureAwait(false);
    }
}

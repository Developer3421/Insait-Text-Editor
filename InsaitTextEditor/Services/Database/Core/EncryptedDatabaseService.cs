using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LiteDB;

namespace InsaitTextEditor.Services.Database.Core;

/// <summary>
    /// Extended database service with support for queries across all shards
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
        /// Get collection from active database
    /// </summary>
    protected virtual ILiteCollection<T> GetCollection()
    {
        var db = GetDatabase();
        return db.GetCollection<T>(CollectionName);
    }

    /// <summary>
        /// Add record
    /// </summary>
    public virtual async Task<BsonValue> InsertAsync(T entity)
    {
        var collection = GetCollection();
        var id = collection.Insert(entity);
        await UpdateMetricsAsync(CollectionName).ConfigureAwait(false);
        return id;
    }

    /// <summary>
        /// Add many records
    /// </summary>
    public virtual async Task<int> InsertBulkAsync(IEnumerable<T> entities)
    {
        var collection = GetCollection();
        var count = collection.InsertBulk(entities);
        await UpdateMetricsAsync(CollectionName).ConfigureAwait(false);
        return count;
    }

    /// <summary>
        /// Update record
    /// </summary>
    public virtual async Task<bool> UpdateAsync(T entity)
    {
        var collection = GetCollection();
        var result = collection.Update(entity);
        await UpdateMetricsAsync(CollectionName).ConfigureAwait(false);
        return result;
    }

    /// <summary>
        /// Delete record by ID
    /// </summary>
    public virtual async Task<bool> DeleteAsync(BsonValue id)
    {
        var collection = GetCollection();
        var result = collection.Delete(id);
        await UpdateMetricsAsync(CollectionName).ConfigureAwait(false);
        return result;
    }

    /// <summary>
        /// Get record by ID
    /// </summary>
    public virtual T? FindById(BsonValue id)
    {
        var collection = GetCollection();
        return collection.FindById(id);
    }

    /// <summary>
        /// Get all records from active database
    /// </summary>
    public virtual IEnumerable<T> FindAll()
    {
        var collection = GetCollection();
        return collection.FindAll();
    }

    /// <summary>
        /// Execute custom query
    /// </summary>
    public virtual IEnumerable<T> Query(Func<ILiteCollection<T>, IEnumerable<T>> queryFunc)
    {
        var collection = GetCollection();
        return queryFunc(collection);
    }

    /// <summary>
        /// Get all records from all shards (for search across all history)
    /// </summary>
    public virtual long Count()
    {
        var collection = GetCollection();
        return collection.LongCount();
    }

    /// <summary>
        /// Execute query across all shards (from newest to oldest)
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
                // Error logging (optional)
                Console.WriteLine($"Error querying shard {shard.FileName}: {ex.Message}");
            }
        }

        return results;
    }

    /// <summary>
        /// Get total count across all shards
    /// </summary>
    public virtual long GetTotalCountAllShards()
    {
        var metadata = ShardManager.GetMetadata();
        return metadata.TotalRecords;
    }

    /// <summary>
    /// Clear entire collection
    /// </summary>
    public virtual async Task ClearAsync()
    {
        var collection = GetCollection();
        collection.DeleteAll();
        await UpdateMetricsAsync(CollectionName).ConfigureAwait(false);
    }
}

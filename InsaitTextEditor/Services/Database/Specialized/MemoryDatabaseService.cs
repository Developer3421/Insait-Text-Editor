using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InsaitTextEditor.Models.Memory;
using InsaitTextEditor.Services.Database.Core;
using LiteDB;

namespace InsaitTextEditor.Services.Database.Specialized;

/// <summary>
/// Спеціалізований сервіс для глобальної пам'яті з підтримкою шифрування
/// </summary>
public class MemoryDatabaseService : EncryptedDatabaseService<MemoryFact>
{
    protected override string CollectionName => "memory_facts";

    public MemoryDatabaseService(
        DatabaseConfig config,
        DatabaseEncryptionManager encryptionManager)
        : base(config, encryptionManager, "Memory")
    {
    }

    protected override void InitializeIndexes(LiteDatabase database)
    {
        var collection = database.GetCollection<MemoryFact>(CollectionName);
        
        // Створити індекси
        collection.EnsureIndex(x => x.Type);
        collection.EnsureIndex(x => x.ExtractedAt);
        collection.EnsureIndex(x => x.IsActive);
        collection.EnsureIndex(x => x.Tags);
    }

    public override async Task InitializeAsync()
    {
        var db = GetDatabase();
        InitializeIndexes(db);
        await UpdateMetricsAsync(CollectionName);
    }

    /// <summary>
    /// Додати новий факт
    /// </summary>
    public async Task<Guid> AddFactAsync(MemoryFact fact)
    {
        fact.Id = Guid.NewGuid();
        fact.ExtractedAt = DateTime.UtcNow;
        fact.LastAccessedAt = DateTime.UtcNow;
        
        await InsertAsync(fact);
        return fact.Id;
    }

    /// <summary>
    /// Отримати активні факти
    /// </summary>
    public List<MemoryFact> GetActiveFacts()
    {
        return QueryAllShards(collection =>
        {
            return collection
                .Query()
                .Where(x => x.IsActive)
                .OrderByDescending(x => x.ExtractedAt)
                .ToList();
        });
    }

    /// <summary>
    /// Отримати факти за типом
    /// </summary>
    public List<MemoryFact> GetFactsByType(FactType type)
    {
        return QueryAllShards(collection =>
        {
            return collection
                .Query()
                .Where(x => x.Type == type && x.IsActive)
                .OrderByDescending(x => x.LastAccessedAt)
                .ToList();
        });
    }

    /// <summary>
    /// Пошук фактів за текстом
    /// </summary>
    public List<MemoryFact> SearchFacts(string searchText)
    {
        searchText = searchText.ToLower();
        
        return QueryAllShards(collection =>
        {
            return collection
                .Query()
                .Where(x => x.IsActive && x.Content.ToLower().Contains(searchText))
                .OrderByDescending(x => x.Confidence)
                .ToList();
        });
    }

    /// <summary>
    /// Отримати факти за тегами
    /// </summary>
    public List<MemoryFact> GetFactsByTags(List<string> tags)
    {
        return QueryAllShards(collection =>
        {
            return collection
                .Query()
                .Where(x => x.IsActive && x.Tags.Any(tag => tags.Contains(tag)))
                .OrderByDescending(x => x.LastAccessedAt)
                .ToList();
        });
    }

    /// <summary>
    /// Оновити час доступу до факту
    /// </summary>
    public async Task UpdateLastAccessedAsync(Guid factId)
    {
        var fact = FindById(new BsonValue(factId));
        if (fact != null)
        {
            fact.LastAccessedAt = DateTime.UtcNow;
            await UpdateAsync(fact);
        }
    }

    /// <summary>
    /// Деактивувати факт
    /// </summary>
    public async Task DeactivateFactAsync(Guid factId)
    {
        var fact = FindById(new BsonValue(factId));
        if (fact != null)
        {
            fact.IsActive = false;
            await UpdateAsync(fact);
        }
    }

    /// <summary>
    /// Get all facts (including inactive)
    /// </summary>
    public List<MemoryFact> GetAllFacts(bool includeInactive = false)
    {
        return QueryAllShards(collection =>
        {
            if (includeInactive)
                return collection.FindAll();
            
            return collection
                .Query()
                .Where(x => x.IsActive)
                .ToList();
        });
    }
}

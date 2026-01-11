using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InsaitTextEditor.Models.Memory;
using InsaitTextEditor.Services.Database.Core;
using LiteDB;

namespace InsaitTextEditor.Services.Database.Specialized;

/// <summary>
    /// Service for working with memory facts database
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
        
        /// Create indexes for fast search
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
        /// Add memory fact
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
        /// Get all facts
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
        /// Get facts by category
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
        /// Search facts by text
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
        /// Delete fact
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
        /// Update fact
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
        /// Clear all facts
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

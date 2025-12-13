using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InsaitTextEditor.Models.Memory;
using LiteDB;

namespace InsaitTextEditor.Services.Memory;

/// <summary>
/// Зберігає факти пам'яті у LiteDB
/// </summary>
public class MemoryStorage
{
    private readonly DatabaseService _dbService;
    private const string FactsCollection = "memory_facts";

    public MemoryStorage(DatabaseService dbService)
    {
        _dbService = dbService;
        InitializeIndexes();
    }

    private void InitializeIndexes()
    {
        var collection = _dbService.GetCollection<MemoryFact>(FactsCollection);
        collection.EnsureIndex(x => x.Type);
        collection.EnsureIndex(x => x.IsActive);
        collection.EnsureIndex(x => x.LastAccessedAt);
        collection.EnsureIndex(x => x.ExtractedAt);
    }

    public Task SaveFactAsync(MemoryFact fact)
    {
        return Task.Run(() =>
        {
            var collection = _dbService.GetCollection<MemoryFact>(FactsCollection);
            collection.Upsert(fact);
        });
    }

    public Task UpdateFactAsync(MemoryFact fact)
    {
        return SaveFactAsync(fact);
    }

    public Task<MemoryFact?> GetFactByIdAsync(Guid id)
    {
        return Task.Run(() =>
        {
            var collection = _dbService.GetCollection<MemoryFact>(FactsCollection);
            return collection.FindById(id);
        });
    }

    public Task<List<MemoryFact>> GetFactsByTypeAsync(FactType type)
    {
        return Task.Run(() =>
        {
            var collection = _dbService.GetCollection<MemoryFact>(FactsCollection);
            return collection.Find(x => x.Type == type && x.IsActive)
                .OrderByDescending(x => x.LastAccessedAt)
                .ToList();
        });
    }

    public Task<List<MemoryFact>> GetRecentFactsAsync(int limit = 10)
    {
        return Task.Run(() =>
        {
            var collection = _dbService.GetCollection<MemoryFact>(FactsCollection);
            return collection.Find(x => x.IsActive)
                .OrderByDescending(x => x.LastAccessedAt)
                .ThenByDescending(x => x.Confidence)
                .Take(limit)
                .ToList();
        });
    }

    public Task<List<MemoryFact>> SearchByTagsAsync(List<string> tags)
    {
        return Task.Run(() =>
        {
            var collection = _dbService.GetCollection<MemoryFact>(FactsCollection);
            return collection.Find(x => x.IsActive && x.Tags.Any(t => tags.Contains(t)))
                .OrderByDescending(x => x.Confidence)
                .ToList();
        });
    }

    public Task DeleteFactAsync(Guid id)
    {
        return Task.Run(() =>
        {
            var collection = _dbService.GetCollection<MemoryFact>(FactsCollection);
            collection.Delete(id);
        });
    }

    public Task<int> GetTotalFactsCountAsync()
    {
        return Task.Run(() =>
        {
            var collection = _dbService.GetCollection<MemoryFact>(FactsCollection);
            return collection.Count(x => x.IsActive);
        });
    }
}


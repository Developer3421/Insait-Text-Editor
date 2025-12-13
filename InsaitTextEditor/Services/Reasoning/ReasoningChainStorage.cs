using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InsaitTextEditor.Models.Reasoning;
using LiteDB;

namespace InsaitTextEditor.Services.Reasoning;

/// <summary>
/// Зберігає reasoning chains у LiteDB
/// </summary>
public class ReasoningChainStorage
{
    private readonly DatabaseService _dbService;
    private const string CollectionName = "reasoning_chains";

    public ReasoningChainStorage(DatabaseService dbService)
    {
        _dbService = dbService;
        InitializeIndexes();
    }

    private void InitializeIndexes()
    {
        var collection = _dbService.GetCollection<ReasoningChain>(CollectionName);
        collection.EnsureIndex(x => x.ConversationId);
        collection.EnsureIndex(x => x.CreatedAt);
        collection.EnsureIndex(x => x.Status);
    }

    public Task SaveChainAsync(ReasoningChain chain)
    {
        return Task.Run(() =>
        {
            var collection = _dbService.GetCollection<ReasoningChain>(CollectionName);
            collection.Upsert(chain);
        });
    }

    public Task UpdateChainAsync(ReasoningChain chain)
    {
        return SaveChainAsync(chain);
    }

    public Task<ReasoningChain?> GetChainByIdAsync(Guid id)
    {
        return Task.Run(() =>
        {
            var collection = _dbService.GetCollection<ReasoningChain>(CollectionName);
            return collection.FindById(id);
        });
    }

    public Task<List<ReasoningChain>> GetChainsByConversationAsync(Guid conversationId)
    {
        return Task.Run(() =>
        {
            var collection = _dbService.GetCollection<ReasoningChain>(CollectionName);
            return collection.Find(x => x.ConversationId == conversationId)
                .OrderBy(x => x.CreatedAt)
                .ToList();
        });
    }

    public Task<List<ReasoningChain>> GetRecentChainsAsync(int limit = 10)
    {
        return Task.Run(() =>
        {
            var collection = _dbService.GetCollection<ReasoningChain>(CollectionName);
            return collection.FindAll()
                .OrderByDescending(x => x.CreatedAt)
                .Take(limit)
                .ToList();
        });
    }

    public Task DeleteChainAsync(Guid id)
    {
        return Task.Run(() =>
        {
            var collection = _dbService.GetCollection<ReasoningChain>(CollectionName);
            collection.Delete(id);
        });
    }
}


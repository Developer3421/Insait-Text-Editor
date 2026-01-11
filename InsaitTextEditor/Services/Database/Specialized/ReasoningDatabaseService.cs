using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InsaitTextEditor.Models.Reasoning;
using InsaitTextEditor.Services.Database.Core;
using LiteDB;

namespace InsaitTextEditor.Services.Database.Specialized;

/// <summary>
    /// Service for working with reasoning chains database
/// </summary>
public class ReasoningDatabaseService : EncryptedDatabaseService<ReasoningChain>
{
    protected override string CollectionName => "reasoning_chains";

    public ReasoningDatabaseService(
        DatabaseConfig config,
        DatabaseEncryptionManager encryptionManager)
        : base(config, encryptionManager, "Reasoning")
    {
    }

    protected override void InitializeIndexes(LiteDatabase database)
    {
        var collection = database.GetCollection<ReasoningChain>(CollectionName);
        
        /// Create indexes
        collection.EnsureIndex(x => x.ConversationId);
        collection.EnsureIndex(x => x.CreatedAt);
        collection.EnsureIndex(x => x.Status);
    }

    public override async Task InitializeAsync()
    {
        var db = GetDatabase();
        InitializeIndexes(db);
        await UpdateMetricsAsync(CollectionName);
    }

    /// <summary>
        /// Save reasoning chain
    /// </summary>
    public async Task<Guid> AddChainAsync(ReasoningChain chain)
    {
        chain.Id = Guid.NewGuid();
        chain.CreatedAt = DateTime.UtcNow;
        
        await InsertAsync(chain);
        return chain.Id;
    }

    /// <summary>
        /// Get reasoning chain by conversation ID
    /// </summary>
    public async Task SaveChainAsync(ReasoningChain chain)
    {
        if (chain.Id == Guid.Empty)
        {
            await AddChainAsync(chain);
        }
        else
        {
            await UpdateAsync(chain);
        }
    }

    /// <summary>
        /// Get all chains
    /// </summary>
    public async Task UpdateChainAsync(ReasoningChain chain)
    {
        await UpdateAsync(chain);
    }

    /// <summary>
        /// Delete chain
    /// </summary>
    public ReasoningChain? GetChainById(Guid chainId)
    {
        return FindById(new BsonValue(chainId));
    }

    /// <summary>
        /// Search chains by query
    /// </summary>
    public Task<ReasoningChain?> GetChainByIdAsync(Guid chainId)
    {
        return Task.FromResult(GetChainById(chainId));
    }

    /// <summary>
        /// Update chain
    /// </summary>
    public List<ReasoningChain> GetChainsByConversation(Guid conversationId)
    {
        return QueryAllShards(collection =>
        {
            return collection
                .Query()
                .Where(x => x.ConversationId == conversationId)
                .OrderByDescending(x => x.CreatedAt)
                .ToList();
        });
    }

    /// <summary>
        /// Get recent chains
    /// </summary>
    public Task<List<ReasoningChain>> GetChainsByConversationAsync(Guid conversationId)
    {
        return Task.FromResult(GetChainsByConversation(conversationId));
    }

    /// <summary>
        /// Clear all chains
    /// </summary>
    public List<ReasoningChain> GetRecentChains(int limit = 50)
    {
        return QueryAllShards(collection =>
        {
            return collection
                .Query()
                .OrderByDescending(x => x.CreatedAt)
                .ToList();
        }, limit);
    }

    /// <summary>
        /// Get chains by status
    /// </summary>
    public List<ReasoningChain> GetChainsByStatus(ChainStatus status)
    {
        return QueryAllShards(collection =>
        {
            return collection
                .Query()
                .Where(x => x.Status == status)
                .OrderByDescending(x => x.CreatedAt)
                .ToList();
        });
    }

    /// <summary>
        /// Update chain status
    /// </summary>
    public async Task UpdateChainStatusAsync(Guid chainId, ChainStatus status)
    {
        var chain = GetChainById(chainId);
        if (chain != null)
        {
            chain.Status = status;
            if (status == ChainStatus.Completed || status == ChainStatus.Failed)
            {
                chain.CompletedAt = DateTime.UtcNow;
            }
            await UpdateAsync(chain);
        }
    }

    /// <summary>
        /// Add step to chain
    /// </summary>
    public async Task AddStepToChainAsync(Guid chainId, ReasoningStep step)
    {
        var chain = GetChainById(chainId);
        if (chain != null)
        {
            chain.Steps.Add(step);
            await UpdateAsync(chain);
        }
    }

    /// <summary>
        /// Get reasoning statistics
    /// </summary>
    public Dictionary<ChainStatus, int> GetStatusStatistics()
    {
        var allChains = QueryAllShards(collection => collection.FindAll());
        
        return allChains
            .GroupBy(x => x.Status)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>
    /// Delete old completed chains (for cleanup)
    /// </summary>
    public async Task<int> DeleteOldCompletedChainsAsync(DateTime olderThan)
    {
        var collection = GetCollection();
        var deleted = collection.DeleteMany(x => 
            (x.Status == ChainStatus.Completed || x.Status == ChainStatus.Failed) 
            && x.CompletedAt.HasValue 
            && x.CompletedAt.Value < olderThan);
        
        await UpdateMetricsAsync(CollectionName);
        return deleted;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InsaitTextEditor.Models.Memory;
using InsaitTextEditor.Models;
using InsaitTextEditor.AI;
using InsaitTextEditor.Services.Database.Specialized;

namespace InsaitTextEditor.Services.Memory;

/// <summary>
/// Main service for working with global memory
/// </summary>
public class MemoryService
{
    private readonly MemoryDatabaseService _memoryDb;
    private readonly MemoryExtractor _extractor;
    private readonly MemoryQueryEngine _queryEngine;
    private bool _isInitialized = false;
    private readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);

    public MemoryService(MemoryDatabaseService memoryDb, LlamaSharpInferenceEngine inferenceEngine)
    {
        _memoryDb = memoryDb;
        _extractor = new MemoryExtractor(inferenceEngine);
        _queryEngine = new MemoryQueryEngine(_memoryDb);
    }

    /// <summary>
    /// Ensures the database is initialized before use
    /// </summary>
    private async Task EnsureInitializedAsync()
    {
        if (_isInitialized) return;
        
        await _initLock.WaitAsync();
        try
        {
            if (!_isInitialized)
            {
                await _memoryDb.InitializeAsync();
                _isInitialized = true;
                Console.WriteLine("[MemoryService] ✅ Database initialized");
            }
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Extracts facts from a message and saves them
    /// </summary>
    public async Task ProcessMessageAsync(ChatMessage message)
    {
        await EnsureInitializedAsync();
        
        if (message.Sender == "System") return;

        // Extract facts from user message
        var facts = await _extractor.ExtractFactsAsync(message.Content);
        
        foreach (var fact in facts)
        {
            fact.Source = "Chat";
            await _memoryDb.AddFactAsync(fact);
        }
    }

    /// <summary>
    /// Searches for relevant facts for a query
    /// </summary>
    public async Task<List<MemoryFact>> QueryMemoryAsync(string query, int maxResults = 5)
    {
        await EnsureInitializedAsync();
        
        var results = _memoryDb.SearchFacts(query).Take(maxResults).ToList();
        
        // Update LastAccessedAt for used facts
        foreach (var fact in results)
        {
            await _memoryDb.UpdateLastAccessedAsync(fact.Id);
        }
        
        return results;
    }

    /// <summary>
    /// Get all facts of a specific type
    /// </summary>
    public async Task<List<MemoryFact>> GetFactsByTypeAsync(FactType type)
    {
        await EnsureInitializedAsync();
        return _memoryDb.GetFactsByType(type);
    }

    /// <summary>
    /// Delete a fact
    /// </summary>
    public async Task DeleteFactAsync(Guid factId)
    {
        await EnsureInitializedAsync();
        await _memoryDb.DeactivateFactAsync(factId);
    }

    /// <summary>
    /// Get total count of facts
    /// </summary>
    public async Task<int> GetTotalFactsCountAsync()
    {
        await EnsureInitializedAsync();
        return _memoryDb.GetActiveFacts().Count;
    }

    /// <summary>
    /// Build context for a new conversation (most important facts)
    /// </summary>
    public async Task<string> BuildContextPromptAsync(int maxFacts = 10)
    {
        await EnsureInitializedAsync();
        
        var recentFacts = _memoryDb.GetActiveFacts().Take(maxFacts).ToList();
        
        if (!recentFacts.Any())
            return string.Empty;

        var contextLines = new List<string>
        {
            "=== CONTEXT FROM MEMORY ===",
            "Here's what I remember about you:",
            ""
        };

        foreach (var fact in recentFacts)
        {
            contextLines.Add($"• {fact.Content}");
        }

        contextLines.Add("");
        contextLines.Add("Use this context to provide personalized responses.");
        contextLines.Add("===========================");

        return string.Join("\n", contextLines);
    }

    /// <summary>
    /// Clear all memory (for GDPR compliance)
    /// </summary>
    public async Task ClearAllMemoryAsync()
    {
        var allFacts = _memoryDb.GetActiveFacts();
        foreach (var fact in allFacts)
        {
            await _memoryDb.DeactivateFactAsync(fact.Id);
        }
    }
}

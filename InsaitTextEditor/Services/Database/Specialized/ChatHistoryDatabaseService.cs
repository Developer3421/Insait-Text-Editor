using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InsaitTextEditor.Models;
using InsaitTextEditor.Services.Database.Core;
using LiteDB;

namespace InsaitTextEditor.Services.Database.Specialized;

/// <summary>
    /// Service for working with chat history database
/// </summary>
public class ChatHistoryDatabaseService : EncryptedDatabaseService<ChatMessage>
{
    protected override string CollectionName => "chat_messages";

    public ChatHistoryDatabaseService(
        DatabaseConfig config,
        DatabaseEncryptionManager encryptionManager)
        : base(config, encryptionManager, "ChatHistory")
    {
    }

    protected override void InitializeIndexes(LiteDatabase database)
    {
        var collection = database.GetCollection<ChatMessage>(CollectionName);
        
        /// Create indexes for fast search
        collection.EnsureIndex(x => x.Timestamp);
        collection.EnsureIndex(x => x.Sender);
    }

    public override async Task InitializeAsync()
    {
        var db = GetDatabase();
        InitializeIndexes(db);
        await UpdateMetricsAsync(CollectionName);
    }

    /// <summary>
        /// Add chat message
    /// </summary>
    public List<ChatMessage> GetRecentMessages(int limit = 100)
    {
        return QueryAllShards(collection =>
        {
            return collection
                .Query()
                .OrderByDescending(x => x.Timestamp)
                .ToList();
        }, limit);
    }

    /// <summary>
        /// Get recent messages
    /// </summary>
    public List<ChatMessage> SearchMessages(string searchText, int limit = 50)
    {
        searchText = searchText.ToLower();
        
        return QueryAllShards(collection =>
        {
            return collection
                .Query()
                .Where(x => x.Content.ToLower().Contains(searchText))
                .OrderByDescending(x => x.Timestamp)
                .ToList();
        }, limit);
    }

    /// <summary>
        /// Get all messages
    /// </summary>
    public List<ChatMessage> GetMessagesByPeriod(DateTime startDate, DateTime endDate)
    {
        return QueryAllShards(collection =>
        {
            return collection
                .Query()
                .Where(x => x.Timestamp >= startDate && x.Timestamp <= endDate)
                .OrderByDescending(x => x.Timestamp)
                .ToList();
        });
    }

    /// <summary>
        /// Clear history
    /// </summary>
    public Dictionary<string, int> GetMessageCountBySender()
    {
        var allMessages = QueryAllShards(collection => collection.FindAll());
        
        return allMessages
            .GroupBy(x => x.Sender)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>
        /// Find messages by content
    /// </summary>
    public async Task<int> AddMessageAsync(string sender, string content)
    {
        var message = new ChatMessage
        {
            Sender = sender,
            Content = content,
            Timestamp = DateTime.UtcNow
        };

        var id = await InsertAsync(message);
        return id.AsInt32;
    }

    /// <summary>
    /// Get all messages
    /// </summary>
    public List<ChatMessage> GetAllMessages()
    {
        return QueryAllShards(collection => collection.FindAll());
    }
}

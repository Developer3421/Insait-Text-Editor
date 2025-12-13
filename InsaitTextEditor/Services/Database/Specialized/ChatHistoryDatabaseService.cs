using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InsaitTextEditor.Models;
using InsaitTextEditor.Services.Database.Core;
using LiteDB;

namespace InsaitTextEditor.Services.Database.Specialized;

/// <summary>
/// Спеціалізований сервіс для історії чату з підтримкою шифрування та шардінгу
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
        
        // Створити індекси для швидкого пошуку
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
    /// Отримати останні повідомлення
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
    /// Пошук повідомлень за текстом
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
    /// Отримати повідомлення за період
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
    /// Отримати статистику по відправнику
    /// </summary>
    public Dictionary<string, int> GetMessageCountBySender()
    {
        var allMessages = QueryAllShards(collection => collection.FindAll());
        
        return allMessages
            .GroupBy(x => x.Sender)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>
    /// Додати повідомлення до чату
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
    /// Отримати всі повідомлення
    /// </summary>
    public List<ChatMessage> GetAllMessages()
    {
        return QueryAllShards(collection => collection.FindAll());
    }
}

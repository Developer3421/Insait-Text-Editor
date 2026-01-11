using System;
using System.Collections.Generic;
using System.Linq;
using InsaitTextEditor.Models;
using InsaitTextEditor.Services.Database.Specialized;
using LiteDB;

namespace InsaitTextEditor.Services
{
    public class ChatHistoryService : IDisposable
    {
        private readonly ChatHistoryDatabaseService _chatDb;
        private const string AgentCollectionName = "agent_messages";

        public ChatHistoryService(ChatHistoryDatabaseService chatDb)
        {
            _chatDb = chatDb;
            // DO NOT call InitializeAsync here - it will be called automatically on first access
        }

        public void AddMessage(ChatMessage message)
        {
            _chatDb.InsertAsync(message).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Add agent message with information about tools used
        /// </summary>
        public void AddAgentMessage(AgentMessage message)
        {
            // For agent messages use Query method to access collection
            _chatDb.Query(collection =>
            {
                // Get database through reflection or add method to base class
                var db = (LiteDatabase)collection.GetType()
                    .GetProperty("Database", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                    .GetValue(collection)!;
                
                if (db != null)
                {
                    var col = db.GetCollection<AgentMessage>(AgentCollectionName);
                    col.EnsureIndex(x => x.Timestamp);
                    col.Insert(message);
                }
                
                return Array.Empty<ChatMessage>();
            });
        }

        public IEnumerable<ChatMessage> GetRecent(int limit = 100)
        {
            return _chatDb.GetRecentMessages(limit);
        }

        /// <summary>
        /// Get recent agent messages with tool information
        /// </summary>
        public IEnumerable<AgentMessage> GetRecentAgentMessages(int limit = 100)
        {
            var results = new List<AgentMessage>();
            
            _chatDb.Query(collection =>
            {
                var db = (LiteDatabase)collection.GetType()
                    .GetProperty("Database", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                    .GetValue(collection)!;
                
                if (db != null)
                {
                    var col = db.GetCollection<AgentMessage>(AgentCollectionName);
                    results.AddRange(col.Query()
                        .OrderByDescending(x => x.Timestamp)
                        .Limit(limit)
                        .ToList());
                }
                
                return Array.Empty<ChatMessage>();
            });
            
            return results;
        }

        public void Clear()
        {
            _chatDb.ClearAsync().GetAwaiter().GetResult();
            
            // Clear agent messages too
            _chatDb.Query(collection =>
            {
                var db = (LiteDatabase)collection.GetType()
                    .GetProperty("Database", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                    .GetValue(collection)!;
                
                if (db != null)
                {
                    db.DropCollection(AgentCollectionName);
                }
                
                return Array.Empty<ChatMessage>();
            });
        }

        public void Dispose()
        {
            _chatDb.Dispose();
        }
    }
}
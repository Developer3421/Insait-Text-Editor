using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InsaitTextEditor.Models;
using InsaitTextEditor.Services.Database.Core;
using LiteDB;

namespace InsaitTextEditor.Services.Database.Specialized;

/// <summary>
    /// Service for working with documents database
/// </summary>
public class DocumentsDatabaseService : EncryptedDatabaseService<Workspace>
{
    protected override string CollectionName => "workspaces";

    public DocumentsDatabaseService(
        DatabaseConfig config,
        DatabaseEncryptionManager encryptionManager)
        : base(config, encryptionManager, "Documents")
    {
    }

    protected override void InitializeIndexes(LiteDatabase database)
    {
        var collection = database.GetCollection<Workspace>(CollectionName);
        
        /// Create indexes
        collection.EnsureIndex(x => x.Id);
    }

    public override async Task InitializeAsync()
    {
        var db = GetDatabase();
        InitializeIndexes(db);
        await UpdateMetricsAsync(CollectionName);
    }

    /// <summary>
        /// Save document
    /// </summary>
    public async Task SaveWorkspaceAsync(Workspace workspace)
    {
        var existing = FindById(new BsonValue(workspace.Id));
        if (existing != null)
        {
            await UpdateAsync(workspace);
        }
        else
        {
            await InsertAsync(workspace);
        }
    }

    /// <summary>
        /// Get document by ID
    /// </summary>
    public Workspace? GetWorkspace(Guid id)
    {
        /// Get all documents
        var result = QueryAllShards(collection =>
        {
            return collection
                .Query()
                .Where(x => x.Id == id)
                .Limit(1)
                .ToList();
        }, 1);

        return result.FirstOrDefault();
    }

    /// <summary>
        /// Search documents by title
    /// </summary>
    public List<Workspace> GetAllWorkspaces()
    {
        return QueryAllShards(collection => collection.FindAll());
    }

    /// <summary>
        /// Update document
    /// </summary>
    public async Task<bool> DeleteWorkspaceAsync(Guid id)
    {
        return await DeleteAsync(new BsonValue(id));
    }

    /// <summary>
        /// Delete document
    /// </summary>
    public long GetWorkspaceCount()
    {
        return Count();
    }

    /// <summary>
    /// Search documents by text
    /// </summary>
    public List<Workspace> SearchWorkspaces(string searchText)
    {
        searchText = searchText.ToLower();
        
        return QueryAllShards(collection =>
        {
            return collection
                .Query()
                .Where(x => x.Text.ToLower().Contains(searchText))
                .ToList();
        });
    }
}

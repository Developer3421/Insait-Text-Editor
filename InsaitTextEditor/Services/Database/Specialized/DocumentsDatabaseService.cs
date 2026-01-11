using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InsaitTextEditor.Models;
using InsaitTextEditor.Services.Database.Core;
using LiteDB;

namespace InsaitTextEditor.Services.Database.Specialized;

/// <summary>
/// Спеціалізований сервіс для документів користувача з підтримкою шифрування
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
        
        // Створити індекс по ID
        collection.EnsureIndex(x => x.Id);
    }

    public override async Task InitializeAsync()
    {
        var db = GetDatabase();
        InitializeIndexes(db);
        await UpdateMetricsAsync(CollectionName);
    }

    /// <summary>
    /// Зберегти workspace
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
    /// Отримати workspace по ID
    /// </summary>
    public Workspace? GetWorkspace(Guid id)
    {
        // Шукаємо по всіх шардах
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
    /// Отримати всі workspaces
    /// </summary>
    public List<Workspace> GetAllWorkspaces()
    {
        return QueryAllShards(collection => collection.FindAll());
    }

    /// <summary>
    /// Видалити workspace
    /// </summary>
    public async Task<bool> DeleteWorkspaceAsync(Guid id)
    {
        return await DeleteAsync(new BsonValue(id));
    }

    /// <summary>
    /// Отримати кількість документів
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InsaitTextEditor.Models;
using InsaitTextEditor.Services.Database.Core;
using LiteDB;

namespace InsaitTextEditor.Services.Database.Specialized;

/// <summary>
/// Class for storing settings with identifier
/// </summary>
public class SettingsRecord
{
    public string Id { get; set; } = "default";
    public EditorSettings Settings { get; set; } = new();
}

/// <summary>
/// Specialized service for settings with encryption support
/// Does not use rotation (single DB)
/// </summary>
public class SettingsDatabaseService : DatabaseServiceBase
{
    private const string CollectionName = "settings";

    public SettingsDatabaseService(
        DatabaseConfig config,
        DatabaseEncryptionManager encryptionManager)
        : base(config, encryptionManager, "Settings")
    {
    }

    protected override void InitializeIndexes(LiteDatabase database)
    {
        var collection = database.GetCollection<SettingsRecord>(CollectionName);
        
        // Check if default settings exist
        var defaultSettings = collection.FindById(new BsonValue("default"));
        if (defaultSettings == null)
        {
            // Create default settings
            defaultSettings = new SettingsRecord
            {
                Id = "default",
                Settings = new EditorSettings()
            };
            collection.Insert(defaultSettings);
        }
    }

    public override Task InitializeAsync()
    {
        var db = GetDatabase();
        InitializeIndexes(db);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Save settings
    /// </summary>
    public async Task SaveSettingsAsync(EditorSettings settings, string settingsId = "default")
    {
        var db = GetDatabase();
        var collection = db.GetCollection<SettingsRecord>(CollectionName);
        
        var record = new SettingsRecord
        {
            Id = settingsId,
            Settings = settings
        };

        var existing = collection.FindById(new BsonValue(settingsId));
        if (existing != null)
        {
            collection.Update(record);
        }
        else
        {
            collection.Insert(record);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Load settings
    /// </summary>
    public EditorSettings? LoadSettings(string settingsId = "default")
    {
        var db = GetDatabase();
        var collection = db.GetCollection<SettingsRecord>(CollectionName);
        var record = collection.FindById(new BsonValue(settingsId));
        return record?.Settings;
    }

    /// <summary>
    /// Get all settings
    /// </summary>
    public List<EditorSettings> GetAllSettings()
    {
        var db = GetDatabase();
        var collection = db.GetCollection<SettingsRecord>(CollectionName);
        return collection.FindAll().Select(r => r.Settings).ToList();
    }

    /// <summary>
    /// Delete settings
    /// </summary>
    public async Task<bool> DeleteSettingsAsync(string settingsId)
    {
        if (settingsId == "default")
            return false; // Cannot delete default settings

        var db = GetDatabase();
        var collection = db.GetCollection<SettingsRecord>(CollectionName);
        var result = collection.Delete(new BsonValue(settingsId));

        await Task.CompletedTask;
        return result;
    }

    /// <summary>
    /// Override GetDatabase to not use rotation
    /// </summary>
    protected override LiteDatabase GetDatabase()
    {
        // Automatically initialize on first access
        if (!_isInitialized)
        {
            _isInitialized = true;
            
            // For Settings use single file without rotation
            var settingsPath = System.IO.Path.Combine(
                Config.GetDatabasePath(DatabaseName), 
                "settings.litedb");
            
            CurrentDatabase = CreateDatabaseConnection(settingsPath);
            
            // Execute synchronous index initialization
            try
            {
                InitializeIndexes(CurrentDatabase);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SettingsDatabaseService] Initialization error: {ex.Message}");
            }
            
            return CurrentDatabase;
        }
        
        if (CurrentDatabase != null && !IsDisposed)
        {
            return CurrentDatabase;
        }

        // If database not created, create it
        var path = System.IO.Path.Combine(
            Config.GetDatabasePath(DatabaseName), 
            "settings.litedb");
        
        CurrentDatabase = CreateDatabaseConnection(path);
        return CurrentDatabase;
    }
}

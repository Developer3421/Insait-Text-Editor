using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InsaitTextEditor.Models;
using InsaitTextEditor.Services.Database.Core;
using LiteDB;

namespace InsaitTextEditor.Services.Database.Specialized;

/// <summary>
/// Клас для збереження налаштувань з ідентифікатором
/// </summary>
public class SettingsRecord
{
    public string Id { get; set; } = "default";
    public EditorSettings Settings { get; set; } = new();
}

/// <summary>
/// Спеціалізований сервіс для налаштувань з підтримкою шифрування
/// Не використовує ротацію (одна БД)
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
        
        // Перевірити чи є налаштування за замовчуванням
        var defaultSettings = collection.FindById(new BsonValue("default"));
        if (defaultSettings == null)
        {
            // Створити налаштування за замовчуванням
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
    /// Зберегти налаштування
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
    /// Завантажити налаштування
    /// </summary>
    public EditorSettings? LoadSettings(string settingsId = "default")
    {
        var db = GetDatabase();
        var collection = db.GetCollection<SettingsRecord>(CollectionName);
        var record = collection.FindById(new BsonValue(settingsId));
        return record?.Settings;
    }

    /// <summary>
    /// Отримати всі налаштування
    /// </summary>
    public List<EditorSettings> GetAllSettings()
    {
        var db = GetDatabase();
        var collection = db.GetCollection<SettingsRecord>(CollectionName);
        return collection.FindAll().Select(r => r.Settings).ToList();
    }

    /// <summary>
    /// Видалити налаштування
    /// </summary>
    public async Task<bool> DeleteSettingsAsync(string settingsId)
    {
        if (settingsId == "default")
            return false; // Не можна видалити налаштування за замовчуванням

        var db = GetDatabase();
        var collection = db.GetCollection<SettingsRecord>(CollectionName);
        var result = collection.Delete(new BsonValue(settingsId));

        await Task.CompletedTask;
        return result;
    }

    /// <summary>
    /// Перевизначаємо GetDatabase щоб не використовувати ротацію
    /// </summary>
    protected override LiteDatabase GetDatabase()
    {
        // Автоматично ініціалізувати при першому доступі
        if (!_isInitialized)
        {
            _isInitialized = true;
            
            // Для Settings використовуємо один файл без ротації
            var settingsPath = System.IO.Path.Combine(
                Config.GetDatabasePath(DatabaseName), 
                "settings.litedb");
            
            CurrentDatabase = CreateDatabaseConnection(settingsPath);
            
            // Виконати синхронну ініціалізацію індексів
            try
            {
                InitializeIndexes(CurrentDatabase);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SettingsDatabaseService] Помилка ініціалізації: {ex.Message}");
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

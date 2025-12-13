using System;
using System.IO;

namespace InsaitTextEditor.Services.Database.Core;

/// <summary>
/// Конфігурація для баз даних з шифруванням та шардінгом
/// </summary>
public class DatabaseConfig
{
    /// <summary>
    /// Базовий шлях до папки з зашифрованими базами даних
    /// </summary>
    public string EncryptedDataPath { get; set; } = Path.Combine(
        GetExecutableDirectory(), "Data", "Encrypted");

    /// <summary>
    /// Шлях до папки з ключами шифрування
    /// </summary>
    public string KeysPath { get; set; } = Path.Combine(
        GetExecutableDirectory(), "Data", "Keys");

    /// <summary>
    /// Отримує директорію, де знаходиться виконуваний файл
    /// </summary>
    private static string GetExecutableDirectory()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath))
        {
            return Path.GetDirectoryName(processPath) ?? AppContext.BaseDirectory;
        }
        return AppContext.BaseDirectory;
    }

    /// <summary>
    /// Максимальний розмір шарда в байтах (за замовчуванням 1 ГБ)
    /// </summary>
    public long MaxShardSizeBytes { get; set; } = 1_073_741_824; // 1 GB

    /// <summary>
    /// Буфер для ротації (коли залишилось 50 МБ - створити новий шард)
    /// </summary>
    public long RotationBufferBytes { get; set; } = 52_428_800; // 50 MB

    /// <summary>
    /// Тип з'єднання LiteDB
    /// </summary>
    public string ConnectionType { get; set; } = "Shared";

    /// <summary>
    /// Таймаут для операцій БД (секунди)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Чи використовувати шифрування
    /// </summary>
    public bool UseEncryption { get; set; } = true;

    /// <summary>
    /// Чи автоматично створювати БД при запуску
    /// </summary>
    public bool AutoCreateDatabase { get; set; } = true;

    /// <summary>
    /// Отримати шлях до папки конкретної БД
    /// </summary>
    public string GetDatabasePath(string databaseName)
    {
        return Path.Combine(EncryptedDataPath, databaseName);
    }

    /// <summary>
    /// Отримати шлях до файла метаданих для конкретної БД
    /// </summary>
    public string GetMetadataPath(string databaseName)
    {
        return Path.Combine(GetDatabasePath(databaseName), "metadata.json");
    }

    /// <summary>
    /// Отримати шлях до майстер-ключа
    /// </summary>
    public string GetMasterKeyPath()
    {
        return Path.Combine(KeysPath, "master.key");
    }

    /// <summary>
    /// Створити всі необхідні папки
    /// </summary>
    public void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(EncryptedDataPath);
        Directory.CreateDirectory(KeysPath);
        
        // Підпапки для зашифрованих даних
        string[] subfolders = { "ChatHistory", "Documents", "Memory", "Reasoning", "Settings" };
        foreach (var folder in subfolders)
        {
            Directory.CreateDirectory(Path.Combine(EncryptedDataPath, folder));
        }
    }
}


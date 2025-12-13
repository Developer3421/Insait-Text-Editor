using System;
using System.IO;

namespace InsaitTextEditor.Services.Database.Core;

/// <summary>
/// Сервіс для автоматичної ініціалізації структури папок бази даних
/// </summary>
public class DatabaseFolderInitializer
{
    private readonly string _baseDirectory;

    /// <summary>
    /// Створює новий екземпляр ініціалізатора папок БД
    /// </summary>
    /// <param name="baseDirectory">Базова директорія (за замовчуванням - директорія exe файлу)</param>
    public DatabaseFolderInitializer(string? baseDirectory = null)
    {
        // Використовуємо Environment.ProcessPath для отримання реального шляху до exe
        _baseDirectory = baseDirectory ?? GetExecutableDirectory();
    }

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
    /// Створює всі необхідні папки для бази даних
    /// </summary>
    /// <returns>True якщо всі папки створено успішно</returns>
    public bool EnsureAllDatabaseFoldersExist()
    {
        try
        {
            Console.WriteLine($"[DatabaseFolderInitializer] Ініціалізація папок БД у: {_baseDirectory}");

            // Створення папки для старого формату БД
            CreateDatabaseFolder();

            // Створення папок для нової системи з шифруванням
            CreateEncryptedDataFolders();
            CreateKeysFolder();

            Console.WriteLine("[DatabaseFolderInitializer] ✅ Всі папки БД створено успішно");
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"[DatabaseFolderInitializer] ❌ Недостатньо прав доступу: {ex.Message}");
            return false;
        }
        catch (IOException ex)
        {
            Console.WriteLine($"[DatabaseFolderInitializer] ❌ Помилка вводу-виводу: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseFolderInitializer] ❌ Несподівана помилка: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Створює папку Database для старого формату БД
    /// </summary>
    private void CreateDatabaseFolder()
    {
        var databasePath = Path.Combine(_baseDirectory, "Database");
        CreateFolderIfNotExists(databasePath, "Database (старий формат)");
    }

    /// <summary>
    /// Створює папки для зашифрованих даних
    /// </summary>
    private void CreateEncryptedDataFolders()
    {
        var encryptedBasePath = Path.Combine(_baseDirectory, "Data", "Encrypted");
        CreateFolderIfNotExists(encryptedBasePath, "Data/Encrypted");

        // Підпапки для різних типів даних
        string[] subfolders = { "ChatHistory", "Documents", "Memory", "Reasoning", "Settings" };
        
        foreach (var folder in subfolders)
        {
            var folderPath = Path.Combine(encryptedBasePath, folder);
            CreateFolderIfNotExists(folderPath, $"Encrypted/{folder}");
        }
    }

    /// <summary>
    /// Створює папку для ключів шифрування
    /// </summary>
    private void CreateKeysFolder()
    {
        var keysPath = Path.Combine(_baseDirectory, "Data", "Keys");
        CreateFolderIfNotExists(keysPath, "Data/Keys");
    }

    /// <summary>
    /// Створює папку, якщо вона не існує
    /// </summary>
    /// <param name="path">Шлях до папки</param>
    /// <param name="displayName">Відображуване ім'я для логування</param>
    private void CreateFolderIfNotExists(string path, string displayName)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            Console.WriteLine($"[DatabaseFolderInitializer] ✓ Створено папку: {displayName}");
        }
        else
        {
            Console.WriteLine($"[DatabaseFolderInitializer] ○ Папка вже існує: {displayName}");
        }
    }

    /// <summary>
    /// Перевіряє, чи є доступ на запис у базову директорію
    /// </summary>
    /// <returns>True якщо є доступ на запис</returns>
    public bool HasWriteAccess()
    {
        try
        {
            var testFile = Path.Combine(_baseDirectory, $".write_test_{Guid.NewGuid()}.tmp");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Отримує альтернативний шлях для БД у LocalAppData
    /// </summary>
    /// <returns>Шлях до LocalAppData\InsaitTextEditor</returns>
    public static string GetFallbackDataPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "InsaitTextEditor");
    }

    /// <summary>
    /// Отримує інформацію про створені папки
    /// </summary>
    /// <returns>Інформація про структуру папок</returns>
    public string GetFoldersInfo()
    {
        return $@"Структура папок БД:
├── {Path.Combine(_baseDirectory, "Database")}
└── {Path.Combine(_baseDirectory, "Data")}
    ├── Encrypted
    │   ├── ChatHistory
    │   ├── Documents
    │   ├── Memory
    │   ├── Reasoning
    │   └── Settings
    └── Keys";
    }
}


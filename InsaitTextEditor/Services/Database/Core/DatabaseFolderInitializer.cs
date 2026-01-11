using System;
using System.IO;

namespace InsaitTextEditor.Services.Database.Core;

/// <summary>
/// Service that automatically initializes the folder structure for databases
/// </summary>
public class DatabaseFolderInitializer
{
    private readonly string _baseDirectory;

    /// <summary>
    /// Creates a new instance of the database folder initializer
    /// </summary>
    /// <param name="baseDirectory">Base directory (default - the exe folder)</param>
    public DatabaseFolderInitializer(string? baseDirectory = null)
    {
        // Store-safe default: keep all mutable data under LocalAppData.
        // The MSIX installation folder is read-only.
        _baseDirectory = baseDirectory ?? GetWritableDataRoot();
    }

    /// <summary>
    /// Gets the directory where the executable is located
    /// </summary>
    private static string GetWritableDataRoot()
    {
        var overrideRoot = Environment.GetEnvironmentVariable("INSAIT_DATA_ROOT");
        if (!string.IsNullOrWhiteSpace(overrideRoot))
            return overrideRoot;

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "InsaitTextEditor");
    }

    /// <summary>
    /// Creates all necessary folders for the database
    /// </summary>
    /// <returns>True if all folders were created successfully</returns>
    public bool EnsureAllDatabaseFoldersExist()
    {
        try
        {
            Console.WriteLine($"[DatabaseFolderInitializer] Ініціалізація папок БД у: {_baseDirectory}");

            // Create folder for the legacy database format
            CreateDatabaseFolder();

            // Create folders for the new encrypted data system
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
    /// Creates the Database folder for the legacy DB format
    /// </summary>
    private void CreateDatabaseFolder()
    {
        var databasePath = Path.Combine(_baseDirectory, "Database");
        CreateFolderIfNotExists(databasePath, "Database (legacy format)");
    }

    /// <summary>
    /// Creates folders for encrypted data
    /// </summary>
    private void CreateEncryptedDataFolders()
    {
        var encryptedBasePath = Path.Combine(_baseDirectory, "Data", "Encrypted");
        CreateFolderIfNotExists(encryptedBasePath, "Data/Encrypted");

        // Subfolders for different data types
        string[] subfolders = { "ChatHistory", "Documents", "Memory", "Reasoning", "Settings" };
        
        foreach (var folder in subfolders)
        {
            var folderPath = Path.Combine(encryptedBasePath, folder);
            CreateFolderIfNotExists(folderPath, $"Encrypted/{folder}");
        }
    }

    /// <summary>
    /// Creates the keys folder for encryption keys
    /// </summary>
    private void CreateKeysFolder()
    {
        var keysPath = Path.Combine(_baseDirectory, "Data", "Keys");
        CreateFolderIfNotExists(keysPath, "Data/Keys");
    }

    /// <summary>
    /// Creates a folder if it does not exist
    /// </summary>
    /// <param name="path">Path to the folder</param>
    /// <param name="displayName">Display name for logging</param>
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
    /// Checks whether the base directory is writable
    /// </summary>
    /// <returns>True if write access is available</returns>
    public bool HasWriteAccess()
    {
        // This initializer always uses a writable root, but keep a cheap sanity check.
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
    /// Gets a fallback path for data under LocalAppData
    /// </summary>
    /// <returns>Path to LocalAppData\InsaitTextEditor</returns>
    public static string GetFallbackDataPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "InsaitTextEditor");
    }

    /// <summary>
    /// Returns information about created folders
    /// </summary>
    /// <returns>Information about the folder structure</returns>
    public string GetFoldersInfo()
    {
        return $@"DB folder structure:
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


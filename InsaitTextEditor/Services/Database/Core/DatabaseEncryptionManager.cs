using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace InsaitTextEditor.Services.Database.Core;

/// <summary>
/// Менеджер для управління ключами шифрування AES-256
/// Використовує Windows DPAPI для захисту ключів
/// </summary>
public class DatabaseEncryptionManager
{
    private readonly DatabaseConfig _config;
    private string? _cachedMasterKey;

    public DatabaseEncryptionManager(DatabaseConfig config)
    {
        _config = config;
        _config.EnsureDirectoriesExist();
    }

    /// <summary>
    /// Отримати або створити майстер-ключ
    /// </summary>
    public string GetOrCreateMasterKey()
    {
        if (!string.IsNullOrEmpty(_cachedMasterKey))
            return _cachedMasterKey;

        var keyPath = _config.GetMasterKeyPath();

        if (File.Exists(keyPath))
        {
            try
            {
                _cachedMasterKey = LoadMasterKey(keyPath);
            }
            catch (Exception ex) when (ex is CryptographicException || ex is UnauthorizedAccessException || ex is IOException)
            {
                // Store / sandbox / profile issues can make DPAPI or file access fail.
                // Instead of crashing at launch, regenerate a new key so the app can start.
                try
                {
                    File.Delete(keyPath);
                }
                catch
                {
                    // ignore; we'll attempt to overwrite below (may still fail)
                }

                _cachedMasterKey = GenerateAndSaveMasterKey(keyPath);
            }
        }
        else
        {
            _cachedMasterKey = GenerateAndSaveMasterKey(keyPath);
        }

        return _cachedMasterKey;
    }

    /// <summary>
    /// Генерувати новий AES-256 ключ
    /// </summary>
    private string GenerateAesKey()
    {
        using var aes = Aes.Create();
        aes.KeySize = 256; // AES-256
        aes.GenerateKey();
        return Convert.ToBase64String(aes.Key);
    }

    /// <summary>
    /// Генерувати та зберегти майстер-ключ
    /// </summary>
    private string GenerateAndSaveMasterKey(string keyPath)
    {
        var key = GenerateAesKey();
        var keyBytes = Encoding.UTF8.GetBytes(key);

        // Шифрування ключа через Windows DPAPI
        var encryptedKey = ProtectData(keyBytes);

        // Зберігаємо зашифрований ключ
        File.WriteAllBytes(keyPath, encryptedKey);

        return key;
    }

    /// <summary>
    /// Завантажити майстер-ключ з файлу
    /// </summary>
    private string LoadMasterKey(string keyPath)
    {
        var encryptedKey = File.ReadAllBytes(keyPath);
        var keyBytes = UnprotectData(encryptedKey);
        return Encoding.UTF8.GetString(keyBytes);
    }

    /// <summary>
    /// Отримати похідний ключ для конкретної БД
    /// </summary>
    public string GetDatabaseKey(string databaseName)
    {
        var masterKey = GetOrCreateMasterKey();
        
        // Використовуємо PBKDF2 для генерації похідного ключа
        var derivedKey = Rfc2898DeriveBytes.Pbkdf2(
            masterKey,
            Encoding.UTF8.GetBytes(databaseName),
            100000, // iterations
            HashAlgorithmName.SHA256,
            32 // 256 bits
        );

        return Convert.ToBase64String(derivedKey);
    }

    /// <summary>
    /// Захистити дані через Windows DPAPI
    /// </summary>
    private byte[] ProtectData(byte[] data)
    {
        try
        {
            return ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException || ex is CryptographicException)
        {
            // If DPAPI isn't available (or fails in restricted environments), fall back.
            // NOTE: This reduces security but keeps the app usable.
            return data;
        }
    }

    /// <summary>
    /// Розшифрувати дані через Windows DPAPI
    /// </summary>
    private byte[] UnprotectData(byte[] encryptedData)
    {
        try
        {
            return ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.CurrentUser);
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException || ex is CryptographicException)
        {
            // If DPAPI can't decrypt (different user/profile, corrupted key, sandbox), fall back.
            return encryptedData;
        }
    }

    /// <summary>
    /// Очистити кеш ключа
    /// </summary>
    public void ClearCache()
    {
        _cachedMasterKey = null;
    }
}

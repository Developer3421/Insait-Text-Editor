using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using InsaitTextEditor.Services;

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

        string keyPath;
        try
        {
            keyPath = _config.GetMasterKeyPath();
        }
        catch
        {
            _cachedMasterKey = GenerateAesKey();
            StartupDiagnostics.Warn("Master key path unavailable; using session-only master key (no persistence).");
            return _cachedMasterKey;
        }

        // Ensure key directory exists (best-effort)
        try
        {
            var dir = Path.GetDirectoryName(keyPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Error($"Failed to ensure key directory for '{keyPath}'.", ex);
        }

        if (File.Exists(keyPath))
        {
            try
            {
                _cachedMasterKey = LoadMasterKey(keyPath);
                return _cachedMasterKey;
            }
            catch (Exception ex) when (ex is CryptographicException || ex is UnauthorizedAccessException || ex is IOException)
            {
                StartupDiagnostics.Error($"Failed to load/decrypt master key at '{keyPath}'. Will regenerate.", ex);

                try
                {
                    var backup = keyPath + $".corrupt_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
                    File.Move(keyPath, backup);
                    StartupDiagnostics.Info($"Backed up corrupt master key to '{backup}'.");
                }
                catch (Exception moveEx)
                {
                    StartupDiagnostics.Error($"Failed to backup corrupt master key '{keyPath}', will try delete.", moveEx);
                    try { File.Delete(keyPath); } catch { /* ignore */ }
                }

                try
                {
                    _cachedMasterKey = GenerateAndSaveMasterKey(keyPath);
                    StartupDiagnostics.Info($"Generated new master key at '{keyPath}'.");
                    return _cachedMasterKey;
                }
                catch (Exception genEx)
                {
                    _cachedMasterKey = GenerateAesKey();
                    StartupDiagnostics.Error("Failed to persist new master key; using session-only master key (no persistence).", genEx);
                    return _cachedMasterKey;
                }
            }
        }

        // No key file: generate new.
        try
        {
            _cachedMasterKey = GenerateAndSaveMasterKey(keyPath);
            StartupDiagnostics.Info($"Created master key at '{keyPath}'.");
        }
        catch (Exception ex)
        {
            _cachedMasterKey = GenerateAesKey();
            StartupDiagnostics.Error("Failed to create master key on disk; using session-only master key (no persistence).", ex);
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

        // Encryption through DPAPI (best-effort)
        var encryptedKey = ProtectData(keyBytes);

        // Save encrypted key
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

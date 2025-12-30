using System;
using System.IO;
using InsaitTextEditor.Services.Database.Core;

namespace InsaitTextEditor.Services;

/// <summary>
/// Best-effort startup validation for Store certification and field diagnostics.
/// Never throws; logs results to StartupDiagnostics (startup.log).
/// </summary>
public static class StartupSelfCheck
{
    public static void Run(
        DatabaseConfig config,
        DatabaseEncryptionManager? encryptionManager,
        params (string Name, Func<bool> Check)[] checks)
    {
        try
        {
            StartupDiagnostics.Info("StartupSelfCheck: begin");

            // Log resolved paths (best-effort)
            try
            {
                StartupDiagnostics.Info($"DataRoot: {GetDataRootForDiagnostics()}");
                StartupDiagnostics.Info($"KeysPath: {config.KeysPath}");
                StartupDiagnostics.Info($"EncryptedDataPath: {config.EncryptedDataPath}");
            }
            catch { /* ignore */ }

            // Master key touch
            try
            {
                if (encryptionManager == null)
                {
                    StartupDiagnostics.Warn("StartupSelfCheck: EncryptionManager is null (skipping master key check). ");
                }
                else
                {
                    var _ = encryptionManager.GetOrCreateMasterKey();
                    var keyPath = SafeGetMasterKeyPath(config);
                    StartupDiagnostics.Info($"StartupSelfCheck: master key OK (path='{keyPath}')");
                }
            }
            catch (Exception ex)
            {
                StartupDiagnostics.Error("StartupSelfCheck: master key check FAILED", ex);
            }

            // Run custom checks
            foreach (var (name, check) in checks)
            {
                try
                {
                    var ok = check();
                    if (ok)
                        StartupDiagnostics.Info($"StartupSelfCheck: {name}: OK");
                    else
                        StartupDiagnostics.Warn($"StartupSelfCheck: {name}: FAIL");
                }
                catch (Exception ex)
                {
                    StartupDiagnostics.Error($"StartupSelfCheck: {name}: EXCEPTION", ex);
                }
            }

            StartupDiagnostics.Info("StartupSelfCheck: end");
        }
        catch
        {
            // never throw
        }
    }

    /// <summary>
    /// Convenience wrapper used by the app: validates all built-in DB services.
    /// </summary>
    public static void RunDefault(
        DatabaseConfig config,
        DatabaseEncryptionManager? encryptionManager,
        params (string Name, Action Touch)[] services)
    {
        var checks = new (string Name, Func<bool> Check)[services.Length];
        for (var i = 0; i < services.Length; i++)
        {
            var svc = services[i];
            checks[i] = (svc.Name, () =>
            {
                svc.Touch();
                return true;
            });
        }

        Run(config, encryptionManager, checks);
    }

    private static string GetDataRootForDiagnostics()
    {
        var overrideRoot = Environment.GetEnvironmentVariable("INSAIT_DATA_ROOT");
        if (!string.IsNullOrWhiteSpace(overrideRoot))
            return overrideRoot;

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "InsaitTextEditor");
    }

    private static string SafeGetMasterKeyPath(DatabaseConfig config)
    {
        try { return config.GetMasterKeyPath(); }
        catch { return "<unavailable>"; }
    }
}
